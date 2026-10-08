Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidencePaths.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/TaskEventStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceStateStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceCleanup.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceRetentionPolicy.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1')
$script:Clock={ [DateTimeOffset]::UtcNow }
$script:FaultHook=$null
$script:FailureHook=$null
function New-EvidenceOperation([string]$Path){return [pscustomobject]@{process=Get-EvidenceProcessIdentity;childMarker=[IO.Path]::Combine($Path,'readback-process.json');path=$Path}}
function Add-EvidenceOwnedFile($Artifact,[string]$Path,[long]$Bytes,[string]$Sha256){$Artifact.ownedCopies+=[pscustomobject]@{path=$Path;bytes=$Bytes;sha256=$Sha256;origin='storage-copy'}}
function Copy-EvidenceSnapshot($Selection,[string]$Operation,[Diagnostics.Stopwatch]$Timer){
    $snapshot=[IO.Path]::Combine($Operation,'snapshot');[IO.Directory]::CreateDirectory($snapshot)|Out-Null;$files=[Collections.Generic.List[OneStarMaker.Artifacts.Packaging.PackageFile]]::new();$total=[long]0
    foreach($name in $Selection.files){
        $source=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Selection.root,$name);$target=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($snapshot,$name);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
        $reader=[IO.FileStream]::new($source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        $writer=$null;$hash=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
        try{
            $writer=[IO.FileStream]::new($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None);$buffer=[byte[]]::new(65536);$bytes=[long]0
            while(($n=$reader.Read($buffer,0,$buffer.Length)) -gt 0){$null=Get-Remaining $Timer 600000;$bytes+=$n;$total+=$n;if($bytes -gt 256MB -or $total -gt 1GB){throw 'source-limit'};$writer.Write($buffer,0,$n);$hash.AppendData($buffer,0,$n)}
            $writer.Flush($true);$entry=[OneStarMaker.Artifacts.Packaging.PackageFile]::new();$entry.Path=$name;$entry.Bytes=$bytes;$entry.Sha256=[Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant();$files.Add($entry)
        }finally{$reader.Dispose();if($writer){$writer.Dispose()};$hash.Dispose()}
    }
    return $files.ToArray()
}
function Invoke-EvidencePublish([string]$ConfigPath,[string]$SelectionPath){
    $result=[ordered]@{status='failed';reasonCode='unconfirmed';reference=$null;packageSha256=$null;receipt=$null;residue=@{localPath=$null;remote='pre-put'}};$guard=$null;$artifact=$null;$task=$null;$timer=[Diagnostics.Stopwatch]::StartNew()
    try{
        $config=Read-EvidenceConfig $ConfigPath;Import-ArtifactPackage;$selection=(Read-ArtifactJson $SelectionPath).Data;Assert-Fields $selection @('schemaVersion','purpose','taskId','root','files','base','head');Assert-EvidenceTaskId $selection.taskId
        $guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $selection.taskId;$null=Assert-EvidenceTransitionSafe $config $selection.taskId $guard;$task=Sync-EvidenceTaskGuarded $config $selection.taskId $guard
        if($task.status -cne 'active'){throw 'resume-required'}
        $sourceStage=$null
        foreach($candidate in $task.staging){if($candidate.path -ceq [IO.Path]::GetFullPath($selection.root)){
            if($candidate.origin -cne 'storage-copy' -or $candidate.adoptionPending -or $candidate.deleteIntent -or -not (Test-ArtifactWithin $candidate.path (Get-ArtifactRoot))){throw 'invalid-source'}
            if($candidate.operation){Assert-EvidenceOperationStopped $candidate.operation;$candidate.operation=$null}
            $sourceStage=$candidate
        }}
        Assert-EvidenceSelection $selection $(if($sourceStage){$sourceStage.path}else{$null})
        if($sourceStage){$sourceStage.adoptionPending=$true;$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard}
        $operation=New-ArtifactOperation;$result.residue.localPath=$operation
        $stage=[pscustomobject]@{path=$operation;createdAt=(& $script:Clock).ToString('o');origin='storage-copy';artifactId=$null;adopted=$false;adoptionPending=$false;operation=New-EvidenceOperation $operation;deleteIntent=$null;protections=@()};$task.staging+=$stage;$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard
        $files=Copy-EvidenceSnapshot $selection $operation $timer;$id=[Guid]::NewGuid().ToString('N')
        $package=[OneStarMaker.Artifacts.Packaging.PackageIO]::CreateVerified($operation,[IO.Path]::Combine($operation,'snapshot'),$selection.base,$selection.head,$config.Data.repositoryId,$id,$files,(Get-Remaining $timer 600000),$selection.taskId)
        $verified=[OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($package.Path,$operation,$package.Sha256,$package.ManifestSha256,$selection.base,$selection.head,$config.Data.repositoryId,$id,(Get-Remaining $timer 600000),'evidence',$selection.taskId)
        $artifact=[pscustomobject][ordered]@{schemaVersion=2;artifactId=$id;taskId=$selection.taskId;repositoryId=$config.Data.repositoryId;deploymentId=$config.Data.deploymentId;key=Get-EvidenceKey $config $selection.taskId $id;base=$selection.base;head=$selection.head;packageBytes=$package.Bytes;packageSha256=$package.Sha256;manifestSha256=$package.ManifestSha256;entries=@($files|ForEach-Object{[pscustomobject]@{path=$_.Path;bytes=$_.Bytes;sha256=$_.Sha256}});createdAt=(& $script:Clock).ToString('o');state='put-pending';appliedEventVersion=$task.appliedEventVersion;taskEndedAt=$task.taskEndedAt;endReason=$task.endReason;deleteEligibleAt=$task.deleteEligibleAt;protections=@();ownedCopies=@();deleteIntent=$null;operation=New-EvidenceOperation $operation;receipt=$null;putIntent=$null}
        Add-EvidenceOwnedFile $artifact $package.Path $package.Bytes $package.Sha256
        foreach($entry in $files){Add-EvidenceOwnedFile $artifact ([IO.Path]::Combine($operation,'snapshot',$entry.Path)) $entry.Bytes $entry.Sha256;Add-EvidenceOwnedFile $artifact ([IO.Path]::Combine($verified.Path,$entry.Path)) $entry.Bytes $entry.Sha256}
        $stage.artifactId=$id
        $task.artifacts+=$artifact;Protect-ArtifactTree $operation;$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard
        $intent=[pscustomobject]@{schemaVersion=2;artifactId=$id;key=$artifact.key;packageBytes=$package.Bytes;packageSha256=$package.Sha256;createdAt=$artifact.createdAt;process=Get-EvidenceProcessIdentity}
        $intentPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $config $task.taskId),'intents',$id+'.json');$intentSha=Write-EvidenceAtomic $intentPath $intent -Immutable;$artifact.putIntent=[pscustomobject]@{path=$intentPath;sha256=$intentSha};$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard
        if($script:FaultHook){& $script:FaultHook 'after-put-intent' $artifact}
        $generation=(Get-CredentialStatus 'osm').Generation;$result.residue.remote='may-have-been-sent'
        $lock=[IO.FileStream]::new($package.Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        try{
            $put=Invoke-ArtifactNetwork $generation (New-ArtifactRequest $config 'put' $artifact.key $package.Path $null $timer)
            # A lost response is resolved by readback, never by a second PUT.
            if(-not (Test-ArtifactSuccess $put 'put' $generation)){throw 'put-unconfirmed'}
            $read=Invoke-ArtifactReadback $operation $config $generation $artifact.key $package.Bytes $package.Sha256 $timer
            if(-not (Test-ArtifactReadback $read $generation $artifact.key $package.Bytes $package.Sha256)){throw 'readback-unconfirmed'}
        }finally{$lock.Dispose()}
        $receipt=[pscustomobject]@{schemaVersion=2;deploymentId=$config.Data.deploymentId;repositoryId=$config.Data.repositoryId;taskId=$task.taskId;artifactId=$id;key=$artifact.key;base=$artifact.base;head=$artifact.head;packageBytes=$package.Bytes;packageSha256=$package.Sha256;manifestSha256=$package.ManifestSha256;entries=$artifact.entries;putIntentSha256=$intentSha;readback=$read;completedAt=(& $script:Clock).ToString('o')}
        $receiptPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $config $task.taskId),'receipts',$id+'.json');$receiptSha=Write-EvidenceAtomic $receiptPath $receipt -Immutable
        if($script:FaultHook){& $script:FaultHook 'after-receipt' $artifact}
        $artifact.receipt=[pscustomobject]@{path=$receiptPath;sha256=$receiptSha};$artifact.state='ready';$artifact.operation=$null;$stage.adopted=$true;$stage.operation=$null;if($sourceStage){$sourceStage.adoptionPending=$false}
        $artifact.ownedCopies=@();foreach($file in [IO.Directory]::EnumerateFiles($operation,'*',[IO.SearchOption]::AllDirectories)){Add-EvidenceOwnedFile $artifact $file ([IO.FileInfo]::new($file)).Length ((Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant())}
        $null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard
        $result.status='passed';$result.reasonCode='complete';$result.reference=New-EvidenceReference $config $artifact;$result.packageSha256=$package.Sha256;$result.receipt=$artifact.receipt;$result.residue.remote='ready'
    }catch{if($script:FailureHook){& $script:FailureHook $_}
        if($_.Exception.Message -cin @('unsupported-schema','unknown-deployment','resume-required','invalid-selection','invalid-source','task-not-registered','unresolved-delete','unresolved-put')){$result.reasonCode=$_.Exception.Message}
        if($task -and $guard){try{$released=$true;if($artifact -and $artifact.operation -and [IO.File]::Exists($artifact.operation.childMarker)){try{$child=(Read-ArtifactJson $artifact.operation.childMarker).Data;$released=$child.exited -and $child.pipesClosed}catch{$released=$false}};if($released){if($artifact){$artifact.operation=$null};if(Get-Variable stage -ErrorAction SilentlyContinue){$stage.operation=$null}};if((Get-Variable sourceStage -ErrorAction SilentlyContinue) -and $sourceStage){$sourceStage.adoptionPending=$false};$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard}catch{if($script:FailureHook){& $script:FailureHook $_}}}
    }finally{if($guard){$guard.Dispose()}}
    return [pscustomobject]$result
}
function Invoke-EvidenceFetch([string]$ConfigPath,[string]$ReferenceText,[string]$ExpectedHash){
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';outputPath=$null;residue=@{localPath=$null}};$guard=$null;$a=$null;$task=$null;$timer=[Diagnostics.Stopwatch]::StartNew()
    try{
        Assert-Hex $ExpectedHash 64;$config=Read-EvidenceConfig $ConfigPath;$ref=Read-EvidenceReference $ReferenceText $config
        $guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $ref.taskId;$null=Assert-EvidenceTransitionSafe $config $ref.taskId $guard;$task=Sync-EvidenceTaskGuarded $config $ref.taskId $guard
        $found=@($task.artifacts|Where-Object artifactId -CEQ $ref.artifactId);if($found.Count -ne 1){throw 'catalog-missing'};$a=$found[0]
        if($a.state -ceq 'deleted'){throw 'expired'};if($a.state -cne 'ready' -or $a.packageSha256 -cne $ExpectedHash -or $a.key -cne $ref.key -or $a.base -cne $ref.base -or $a.head -cne $ref.head -or $a.manifestSha256 -cne $ref.manifestSha256 -or $a.packageBytes -ne $ref.packageBytes){throw 'identity-mismatch'}
        $op=New-ArtifactOperation;$r.residue.localPath=$op;$a.operation=New-EvidenceOperation $op;$stage=[pscustomobject]@{path=$op;createdAt=(& $script:Clock).ToString('o');origin='storage-copy';artifactId=$a.artifactId;adopted=$false;adoptionPending=$false;operation=$a.operation;deleteIntent=$null;protections=@()};$task.staging+=$stage;$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard
        $gen=(Get-CredentialStatus 'osm').Generation;$archive=[IO.Path]::Combine($op,'download.zip');$download=Invoke-ArtifactNetwork $gen (New-ArtifactRequest $config 'get' $a.key $null $archive $timer)
        if(-not (Test-ArtifactSuccess $download 'get' $gen) -or $download.Bytes -ne $ref.packageBytes -or $download.Sha256 -cne $ExpectedHash){throw 'download-unconfirmed'}
        Import-ArtifactPackage;$package=[OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($archive,$op,$ExpectedHash,$ref.manifestSha256,$ref.base,$ref.head,$ref.repositoryId,$ref.artifactId,(Get-Remaining $timer 600000),'evidence',$ref.taskId)
        Add-EvidenceOwnedFile $a $archive $package.Bytes $package.Sha256;foreach($e in $package.Files){Add-EvidenceOwnedFile $a ([IO.Path]::Combine($package.Path,$e.Path)) $e.Bytes $e.Sha256}
        Protect-ArtifactTree $op;$a.operation=$null;$stage.adopted=$true;$stage.operation=$null;$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard;$r.outputPath=$package.Path;$r.status='passed';$r.reasonCode='complete'
    }catch{if($script:FailureHook){& $script:FailureHook $_}if($_.Exception.Message -cin @('unsupported-schema','unknown-deployment','expired','identity-mismatch','catalog-missing')){$r.reasonCode=$_.Exception.Message}}
    finally{if($a -and $task -and $guard){try{$a.operation=$null;if(Get-Variable stage -ErrorAction SilentlyContinue){$stage.operation=$null};$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard}catch{if($script:FailureHook){& $script:FailureHook $_}$r.status='failed';$r.outputPath=$null}};if($guard){$guard.Dispose()}}
    return [pscustomobject]$r
}
Export-ModuleMember -Function Invoke-EvidencePublish,Invoke-EvidenceFetch
function Invoke-EvidenceInspect([string]$ConfigPath,[string]$TaskId){
    $guard=$null
    try{$config=Read-EvidenceConfig $ConfigPath;$guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $TaskId;$task=Sync-EvidenceTaskGuarded $config $TaskId $guard
        $items=@($task.artifacts|ForEach-Object{[pscustomobject]@{artifactId=$_.artifactId;state=$_.state;taskEndedAt=$_.taskEndedAt;deleteEligibleAt=$_.deleteEligibleAt;reference=if($_.state -ceq 'ready'){New-EvidenceReference $config $_}else{$null};packageSha256=if($_.state -ceq 'ready'){$_.packageSha256}else{$null};policy=Get-EvidenceRetentionDecision $task $_ (& $script:Clock)}})
        return [pscustomobject]@{status='passed';reasonCode=if($task.status -ceq 'active'){'active/end-not-recorded'}else{$task.status};taskId=$TaskId;version=$task.appliedEventVersion;artifacts=$items}
    }catch{if($script:FailureHook){& $script:FailureHook $_}return [pscustomobject]@{status='failed';reasonCode='unconfirmed'}}finally{if($guard){$guard.Dispose()}}
}
function Invoke-EvidenceUse([string]$ConfigPath,[string]$ReferenceText,[string]$Consumer,[string]$Until,[string]$Reason){
    $guard=$null
    try{
        if($Consumer -cnotmatch '\A[A-Za-z0-9][A-Za-z0-9-]{0,63}\z' -or [string]::IsNullOrWhiteSpace($Reason) -or $Reason.Length -gt 512 -or $Reason -cmatch '[\x00-\x1f]'){throw 'invalid-use'}
        $untilTime=Assert-Utc $Until;$now=& $script:Clock;if($untilTime -le $now){throw 'invalid-use'}
        $config=Read-EvidenceConfig $ConfigPath;$ref=Read-EvidenceReference $ReferenceText $config;$guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $ref.taskId
        $null=Assert-EvidenceTransitionSafe $config $ref.taskId $guard;$task=Sync-EvidenceTaskGuarded $config $ref.taskId $guard
        $found=@($task.artifacts|Where-Object artifactId -CEQ $ref.artifactId);if($found.Count -ne 1 -or $found[0].state -cne 'ready'){throw 'payload-deleted'}
        $a=$found[0];if($a.key -cne $ref.key -or $a.base -cne $ref.base -or $a.head -cne $ref.head -or $a.manifestSha256 -cne $ref.manifestSha256 -or $a.packageBytes -ne $ref.packageBytes){throw 'identity-mismatch'}
        $id=[Guid]::NewGuid().ToString('N');$a.protections+=[pscustomobject]@{id=$id;owner=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;consumer=$Consumer;reason=$Reason;createdAt=$now.ToString('o');until=$Until;releasedAt=$null}
        $null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard;return [pscustomobject]@{status='passed';reasonCode='in-use';protection=$id;until=$Until}
    }catch{if($script:FailureHook){& $script:FailureHook $_}return [pscustomobject]@{status='failed';reasonCode=if($_.Exception.Message -cin @('payload-deleted','invalid-use','identity-mismatch')){$_.Exception.Message}else{'unconfirmed'}}}finally{if($guard){$guard.Dispose()}}
}
function Invoke-EvidenceRelease([string]$ConfigPath,[string]$ReferenceText,[string]$Protection){
    $guard=$null
    try{Assert-Hex $Protection 32;$config=Read-EvidenceConfig $ConfigPath;$ref=Read-EvidenceReference $ReferenceText $config;$guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $ref.taskId;$task=Sync-EvidenceTaskGuarded $config $ref.taskId $guard
        $found=@($task.artifacts|Where-Object artifactId -CEQ $ref.artifactId);if($found.Count -ne 1){throw 'unknown-protection'}
        $a=$found[0];if($a.key -cne $ref.key -or $a.base -cne $ref.base -or $a.head -cne $ref.head -or $a.manifestSha256 -cne $ref.manifestSha256 -or $a.packageBytes -ne $ref.packageBytes){throw 'identity-mismatch'}
        $p=@($a.protections|Where-Object id -CEQ $Protection);if($p.Count -ne 1){throw 'unknown-protection'};if(-not $p[0].releasedAt){$p[0].releasedAt=(& $script:Clock).ToString('o');$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard}
        return [pscustomobject]@{status='passed';reasonCode='released'}
    }catch{if($script:FailureHook){& $script:FailureHook $_}return [pscustomobject]@{status='failed';reasonCode='unconfirmed'}}finally{if($guard){$guard.Dispose()}}
}
Export-ModuleMember -Function Invoke-EvidencePublish,Invoke-EvidenceFetch,Invoke-EvidenceInspect,Invoke-EvidenceUse,Invoke-EvidenceRelease
