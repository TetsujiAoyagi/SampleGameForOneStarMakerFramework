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
function Add-EvidenceOwnedFile($Artifact,[string]$Path,[long]$Bytes,[string]$Sha256){$Artifact.ownedCopies+=[pscustomobject]@{path=[IO.Path]::GetFullPath($Path);bytes=$Bytes;sha256=$Sha256;origin='storage-copy'}}
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
# Publish and stage share everything up to a locally verified package: selection,
# guard, preflight, source adoption, snapshot, CreateVerified and Extract. The
# artifact record is returned unwritten so the caller alone picks its first state.
# State lives on $Ctx so the caller's catch can release what was registered.
function Initialize-EvidencePackage($Ctx,[string]$ConfigPath,[string]$SelectionPath,[bool]$NoNetwork,[Diagnostics.Stopwatch]$Timer){
        $config=Read-EvidenceConfig $ConfigPath;$Ctx.Config=$config;Import-ArtifactPackage
        # Only the file/JSON/field-set read before the guard is mapped; root/files checks stay after the guard.
        try{$selection=(Read-ArtifactJson $SelectionPath).Data;Assert-Fields $selection @('schemaVersion','purpose','taskId','root','files','base','head')}catch{throw 'invalid-selection'}
        Assert-EvidenceTaskId $selection.taskId
        $guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $selection.taskId;$Ctx.Guard=$guard;$null=Assert-EvidenceTransitionSafe $config $selection.taskId $guard -Residue $Ctx.Staging -NoNetwork:$NoNetwork;$task=Sync-EvidenceTaskGuarded $config $selection.taskId $guard;$Ctx.Task=$task
        if($task.status -cne 'active'){throw 'resume-required'}
        $sourceStage=$null
        foreach($candidate in $task.staging){
            # Preflight tolerates a missing leftover directory, but it can never be a source.
            $lenient=$candidate.adopted -and -not $candidate.operation;$missing=(-not $lenient) -and (Get-EvidenceStagingPathState $task $candidate) -ceq 'missing'
            if($candidate.path -ceq [IO.Path]::GetFullPath($selection.root) -or (Resolve-ArtifactStoredPath $candidate.path -AllowMissing:($lenient -or $missing)) -ceq [IO.Path]::GetFullPath($selection.root)){
                if($missing -or $candidate.origin -cne 'storage-copy' -or $candidate.adoptionPending -or $candidate.deleteIntent -or -not (Test-ArtifactWithin (Resolve-ArtifactStoredPath $candidate.path) (Get-ArtifactRoot))){throw 'invalid-source'}
                if($candidate.operation){Assert-EvidenceOperationStopped $candidate.operation;$candidate.operation=$null}
                $sourceStage=$candidate
            }
        }
        if($sourceStage){$selection.root=Resolve-ArtifactStoredPath $sourceStage.path;$Ctx.SourceStage=$sourceStage}
        Assert-EvidenceSelection $selection $(if($sourceStage){Resolve-ArtifactStoredPath $sourceStage.path}else{$null})
        if($sourceStage){$sourceStage.adoptionPending=$true;$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard}
        $operation=New-ArtifactOperation;$Ctx.Operation=$operation
        $stage=[pscustomobject]@{path=$operation;createdAt=(& $script:Clock).ToString('o');origin='storage-copy';artifactId=$null;adopted=$false;adoptionPending=$false;operation=New-EvidenceOperation $operation;deleteIntent=$null;protections=@()};$Ctx.Stage=$stage;$task.staging+=$stage;$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard
        $files=Copy-EvidenceSnapshot $selection $operation $Timer;$id=[Guid]::NewGuid().ToString('N')
        $package=[OneStarMaker.Artifacts.Packaging.PackageIO]::CreateVerified($operation,[IO.Path]::Combine($operation,'snapshot'),$selection.base,$selection.head,$config.Data.repositoryId,$id,$files,(Get-Remaining $Timer 600000),$selection.taskId)
        $verified=[OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($package.Path,$operation,$package.Sha256,$package.ManifestSha256,$selection.base,$selection.head,$config.Data.repositoryId,$id,(Get-Remaining $Timer 600000),'evidence',$selection.taskId)
        $artifact=[pscustomobject][ordered]@{schemaVersion=2;artifactId=$id;taskId=$selection.taskId;repositoryId=$config.Data.repositoryId;deploymentId=$config.Data.deploymentId;key=Get-EvidenceKey $config $selection.taskId $id;base=$selection.base;head=$selection.head;packageBytes=$package.Bytes;packageSha256=$package.Sha256;manifestSha256=$package.ManifestSha256;entries=@($files|ForEach-Object{[pscustomobject]@{path=$_.Path;bytes=$_.Bytes;sha256=$_.Sha256}});createdAt=(& $script:Clock).ToString('o');state='put-pending';appliedEventVersion=$task.appliedEventVersion;taskEndedAt=$task.taskEndedAt;endReason=$task.endReason;deleteEligibleAt=$task.deleteEligibleAt;protections=@();ownedCopies=@();deleteIntent=$null;operation=New-EvidenceOperation $operation;receipt=$null;putIntent=$null}
        Add-EvidenceOwnedFile $artifact $package.Path $package.Bytes $package.Sha256
        foreach($entry in $files){Add-EvidenceOwnedFile $artifact ([IO.Path]::Combine($operation,'snapshot',$entry.Path)) $entry.Bytes $entry.Sha256;Add-EvidenceOwnedFile $artifact ([IO.Path]::Combine($verified.Path,$entry.Path)) $entry.Bytes $entry.Sha256}
        $stage.artifactId=$id
        return [pscustomobject]@{Artifact=$artifact;Package=$package}
}
function New-EvidencePackageContext {return [pscustomobject]@{Config=$null;Guard=$null;Task=$null;Stage=$null;SourceStage=$null;Operation=$null;Artifact=$null;Staging=[pscustomobject]@{Observations=[Collections.Generic.List[object]]::new();Witnesses=0}}}
# A stopped publish or stage keeps its operation marker unless the readback child
# is known to have exited, so a later preflight can tell a live writer from residue.
function Clear-EvidencePackageContext($Ctx){
    if(-not ($Ctx.Task -and $Ctx.Guard)){return}
    try{$released=$true;$artifact=$Ctx.Artifact
        if($artifact -and $artifact.operation){try{$marker=Resolve-ArtifactStoredPath $artifact.operation.childMarker -AllowMissing;if([IO.File]::Exists($marker)){$child=(Read-ArtifactJson $marker).Data;$released=$child.exited -and $child.pipesClosed}}catch{$released=$false}}
        if($released){if($artifact){$artifact.operation=$null};if($Ctx.Stage){$Ctx.Stage.operation=$null}}
        if($Ctx.SourceStage){$Ctx.SourceStage.adoptionPending=$false};$null=Write-EvidenceTask $Ctx.Config $Ctx.Task.taskId $Ctx.Task $Ctx.Task.generation $Ctx.Guard
    }catch{if($script:FailureHook){& $script:FailureHook $_}}
}
function Invoke-EvidencePublish([string]$ConfigPath,[string]$SelectionPath){
    $result=[ordered]@{status='failed';reasonCode='unconfirmed';reference=$null;packageSha256=$null;receipt=$null;residue=@{localPath=$null;remote='pre-put'};stagingMissing=0};$timer=[Diagnostics.Stopwatch]::StartNew()
    # Created outside the try so a failed publish still reports what preflight saw.
    $ctx=New-EvidencePackageContext
    try{
        $prepared=Initialize-EvidencePackage $ctx $ConfigPath $SelectionPath $false $timer
        $config=$ctx.Config;$guard=$ctx.Guard;$task=$ctx.Task;$stage=$ctx.Stage;$sourceStage=$ctx.SourceStage;$operation=$ctx.Operation;$result.residue.localPath=$operation
        $artifact=$prepared.Artifact;$package=$prepared.Package;$id=$artifact.artifactId;$ctx.Artifact=$artifact
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
        if($_.Exception.Message -cin @('unsupported-schema','invalid-evidence-config','unknown-deployment','resume-required','invalid-selection','invalid-source','task-not-registered','unresolved-delete','unresolved-put')){$result.reasonCode=$_.Exception.Message}
        $result.residue.localPath=$ctx.Operation;Clear-EvidencePackageContext $ctx
    }finally{if($ctx.Guard){$ctx.Guard.Dispose()}}
    $result.stagingMissing=$ctx.Staging.Observations.Count
    return [pscustomobject]$result
}
# Local staging stops where publish would start sending: the verified package stays
# in its staging as the only copy, and nothing reaches credentials or the network.
function Invoke-EvidenceStage([string]$ConfigPath,[string]$SelectionPath){
    $result=[ordered]@{status='failed';reasonCode='unconfirmed';reference=$null;packageSha256=$null;locality=$null;stagingMissing=0};$timer=[Diagnostics.Stopwatch]::StartNew();$ctx=New-EvidencePackageContext;$written=$false
    try{
        $prepared=Initialize-EvidencePackage $ctx $ConfigPath $SelectionPath $true $timer
        $task=$ctx.Task;$operation=$ctx.Operation;$artifact=$prepared.Artifact;$ctx.Artifact=$artifact
        # No putIntent or receipt: their absence is what proves the artifact unsent.
        $artifact.state='staged';$artifact.operation=$null;$artifact|Add-Member -NotePropertyName origin -NotePropertyValue 'stage'
        $artifact.ownedCopies=@();foreach($file in [IO.Directory]::EnumerateFiles($operation,'*',[IO.SearchOption]::AllDirectories)){Add-EvidenceOwnedFile $artifact $file ([IO.FileInfo]::new($file)).Length ((Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant())}
        $ctx.Stage.adopted=$true;$ctx.Stage.operation=$null;if($ctx.SourceStage){$ctx.SourceStage.adoptionPending=$false}
        $task.artifacts+=$artifact;Protect-ArtifactTree $operation;$null=Write-EvidenceTask $ctx.Config $task.taskId $task $task.generation $ctx.Guard;$written=$true
        $result.status='passed';$result.reasonCode='complete';$result.reference=New-EvidenceReference $ctx.Config $artifact;$result.packageSha256=$artifact.packageSha256;$result.locality='local'
    }catch{if($script:FailureHook){& $script:FailureHook $_}
        if($_.Exception.Message -cin @('unsupported-schema','invalid-evidence-config','unknown-deployment','resume-required','invalid-selection','invalid-source','task-not-registered','unresolved-delete','unresolved-put','changed-copy')){$result.reasonCode=$_.Exception.Message}
        # Until the single catalog write lands, the artifact and its adoption exist
        # only in memory. Drop both so the staging falls back to the seven-day rule.
        if(-not $written -and $ctx.Artifact){$ctx.Task.artifacts=@($ctx.Task.artifacts|Where-Object artifactId -CNE $ctx.Artifact.artifactId);$ctx.Stage.adopted=$false}
        if(-not $written){$ctx.Artifact=$null;Clear-EvidencePackageContext $ctx}
    }finally{if($ctx.Guard){$ctx.Guard.Dispose()}}
    $result.stagingMissing=$ctx.Staging.Observations.Count
    return [pscustomobject]$result
}
function Invoke-EvidenceFetch([string]$ConfigPath,[string]$ReferenceText,[string]$ExpectedHash){
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';outputPath=$null;residue=@{localPath=$null};stagingMissing=0};$guard=$null;$a=$null;$task=$null;$local=$false;$fromLocal=$false;$timer=[Diagnostics.Stopwatch]::StartNew()
    $staging=[pscustomobject]@{Observations=[Collections.Generic.List[object]]::new();Witnesses=0}
    try{
        Assert-Hex $ExpectedHash 64;$config=Read-EvidenceConfig $ConfigPath;$ref=Read-EvidenceReference $ReferenceText $config
        $guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $ref.taskId
        # The catalog state before preflight picks the preflight, so a staged artifact
        # never takes the preflight that may resolve unfinished work over the network.
        $peek=Read-EvidenceTask $config $ref.taskId $guard;$local=$null -ne $peek -and @($peek.artifacts|Where-Object {$_.artifactId -ceq $ref.artifactId -and $_.state -ceq 'staged'}).Count -eq 1
        $null=Assert-EvidenceTransitionSafe $config $ref.taskId $guard -Residue $staging -NoNetwork:$local -RecoverArtifactId $ref.artifactId;$task=Sync-EvidenceTaskGuarded $config $ref.taskId $guard
        $found=@($task.artifacts|Where-Object artifactId -CEQ $ref.artifactId);if($found.Count -ne 1){throw 'catalog-missing'};$a=$found[0]
        # The state after preflight picks the source: a promotion that preflight
        # just resolved is fetched from wherever its recovery left the package.
        # A failure of either source is final; there is no fallback to the other.
        $fromLocal=$a.state -ceq 'staged'
        if($a.state -ceq 'deleted'){throw 'expired'};if($a.state -cnotin @('staged','ready') -or $a.packageSha256 -cne $ExpectedHash -or $a.key -cne $ref.key -or $a.base -cne $ref.base -or $a.head -cne $ref.head -or $a.manifestSha256 -cne $ref.manifestSha256 -or $a.packageBytes -ne $ref.packageBytes){throw 'identity-mismatch'}
        $op=New-ArtifactOperation;$r.residue.localPath=$op;$a.operation=New-EvidenceOperation $op;$stage=[pscustomobject]@{path=$op;createdAt=(& $script:Clock).ToString('o');origin='storage-copy';artifactId=$a.artifactId;adopted=$false;adoptionPending=$false;operation=$a.operation;deleteIntent=$null;protections=@()};$task.staging+=$stage;$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard
        $archive=[IO.Path]::Combine($op,'download.zip')
        if($fromLocal){
            # The staged package is the owned copy whose bytes and hash are the
            # package's own; preflight has just verified it. Copy it and verify the
            # copy exactly as a download, and never fall back to the remote store.
            $zip=@($a.ownedCopies|Where-Object {$_.sha256 -ceq $a.packageSha256 -and $_.bytes -eq $a.packageBytes})
            if($zip.Count -lt 1){throw 'changed-copy'}
            [IO.File]::Copy((Resolve-ArtifactStoredPath $zip[0].path),$archive)
            if(([IO.FileInfo]::new($archive)).Length -ne $ref.packageBytes -or (Get-FileHash -LiteralPath $archive).Hash.ToLowerInvariant() -cne $ExpectedHash){throw 'identity-mismatch'}
        }else{
            $gen=(Get-CredentialStatus 'osm').Generation;$download=Invoke-ArtifactNetwork $gen (New-ArtifactRequest $config 'get' $a.key $null $archive $timer)
            if(-not (Test-ArtifactSuccess $download 'get' $gen) -or $download.Bytes -ne $ref.packageBytes -or $download.Sha256 -cne $ExpectedHash){throw 'download-unconfirmed'}
        }
        Import-ArtifactPackage;$package=[OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($archive,$op,$ExpectedHash,$ref.manifestSha256,$ref.base,$ref.head,$ref.repositoryId,$ref.artifactId,(Get-Remaining $timer 600000),'evidence',$ref.taskId)
        Add-EvidenceOwnedFile $a $archive $package.Bytes $package.Sha256;foreach($e in $package.Files){Add-EvidenceOwnedFile $a ([IO.Path]::Combine($package.Path,$e.Path)) $e.Bytes $e.Sha256}
        Protect-ArtifactTree $op;$a.operation=$null;$stage.adopted=$true;$stage.operation=$null;$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard;$r.outputPath=$package.Path;$r.status='passed';$r.reasonCode='complete'
    }catch{if($script:FailureHook){& $script:FailureHook $_}
        # The remote branch keeps its published reason codes; only a fetch that took
        # the local preflight or the local source names the local refusals, so an
        # existing caller never sees a new code. A preflight that failed before the
        # source was chosen answers as its own kind did before.
        if($_.Exception.Message -cin @('unsupported-schema','invalid-evidence-config','unknown-deployment','expired','identity-mismatch','catalog-missing') -or (($local -or $fromLocal) -and $_.Exception.Message -cin @('changed-copy','unresolved-put','unresolved-delete'))){$r.reasonCode=$_.Exception.Message}}
    finally{if($a -and $task -and $guard){try{$a.operation=$null;if(Get-Variable stage -ErrorAction SilentlyContinue){$stage.operation=$null};$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard}catch{if($script:FailureHook){& $script:FailureHook $_}$r.status='failed';$r.outputPath=$null}};if($guard){$guard.Dispose()}}
    $r.stagingMissing=$staging.Observations.Count
    return [pscustomobject]$r
}
Export-ModuleMember -Function Invoke-EvidencePublish,Invoke-EvidenceStage,Invoke-EvidenceFetch
function Invoke-EvidenceInspect([string]$ConfigPath,[string]$TaskId){
    $guard=$null
    try{$config=Read-EvidenceConfig $ConfigPath;$guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $TaskId;$task=Sync-EvidenceTaskGuarded $config $TaskId $guard
        $items=@($task.artifacts|ForEach-Object{[pscustomobject]@{artifactId=$_.artifactId;state=$_.state;taskEndedAt=$_.taskEndedAt;deleteEligibleAt=$_.deleteEligibleAt;reference=if($_.state -cin @('ready','staged')){New-EvidenceReference $config $_}else{$null};packageSha256=if($_.state -cin @('ready','staged')){$_.packageSha256}else{$null};locality=switch -CaseSensitive ($_.state){'staged'{'local'}'ready'{'promoted'}default{$null}};policy=Get-EvidenceRetentionDecision $task $_ (& $script:Clock)}})
        return [pscustomobject]@{status='passed';reasonCode=if($task.status -ceq 'active'){'active/end-not-recorded'}else{$task.status};taskId=$TaskId;version=$task.appliedEventVersion;artifacts=$items}
    }catch{if($script:FailureHook){& $script:FailureHook $_}return [pscustomobject]@{status='failed';reasonCode=if($_.Exception.Message -cin @('invalid-evidence-config','unknown-deployment')){$_.Exception.Message}else{'unconfirmed'}}}finally{if($guard){$guard.Dispose()}}
}
function Invoke-EvidenceUse([string]$ConfigPath,[string]$ReferenceText,[string]$Consumer,[string]$Until,[string]$Reason){
    $guard=$null;$staging=[pscustomobject]@{Observations=[Collections.Generic.List[object]]::new();Witnesses=0}
    try{
        if($Consumer -cnotmatch '\A[A-Za-z0-9][A-Za-z0-9-]{0,63}\z' -or [string]::IsNullOrWhiteSpace($Reason) -or $Reason.Length -gt 512 -or $Reason -cmatch '[\x00-\x1f]'){throw 'invalid-use'}
        $untilTime=Assert-Utc $Until;$now=& $script:Clock;if($untilTime -le $now){throw 'invalid-use'}
        $config=Read-EvidenceConfig $ConfigPath;$ref=Read-EvidenceReference $ReferenceText $config;$guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $ref.taskId
        $null=Assert-EvidenceTransitionSafe $config $ref.taskId $guard -Residue $staging;$task=Sync-EvidenceTaskGuarded $config $ref.taskId $guard
        $found=@($task.artifacts|Where-Object artifactId -CEQ $ref.artifactId);if($found.Count -ne 1 -or $found[0].state -cnotin @('ready','staged')){throw 'payload-deleted'}
        $a=$found[0];if($a.key -cne $ref.key -or $a.base -cne $ref.base -or $a.head -cne $ref.head -or $a.manifestSha256 -cne $ref.manifestSha256 -or $a.packageBytes -ne $ref.packageBytes){throw 'identity-mismatch'}
        $id=[Guid]::NewGuid().ToString('N');$a.protections+=[pscustomobject]@{id=$id;owner=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value;consumer=$Consumer;reason=$Reason;createdAt=$now.ToString('o');until=$Until;releasedAt=$null}
        $null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard;return [pscustomobject]@{status='passed';reasonCode='in-use';protection=$id;until=$Until;stagingMissing=$staging.Observations.Count}
    }catch{if($script:FailureHook){& $script:FailureHook $_}return [pscustomobject]@{status='failed';reasonCode=if($_.Exception.Message -cin @('payload-deleted','invalid-use','identity-mismatch','invalid-evidence-config','unknown-deployment')){$_.Exception.Message}else{'unconfirmed'};stagingMissing=$staging.Observations.Count}}finally{if($guard){$guard.Dispose()}}
}
function Invoke-EvidenceRelease([string]$ConfigPath,[string]$ReferenceText,[string]$Protection){
    $guard=$null
    try{Assert-Hex $Protection 32;$config=Read-EvidenceConfig $ConfigPath;$ref=Read-EvidenceReference $ReferenceText $config;$guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $ref.taskId;$task=Sync-EvidenceTaskGuarded $config $ref.taskId $guard
        $found=@($task.artifacts|Where-Object artifactId -CEQ $ref.artifactId);if($found.Count -ne 1){throw 'unknown-protection'}
        $a=$found[0];if($a.key -cne $ref.key -or $a.base -cne $ref.base -or $a.head -cne $ref.head -or $a.manifestSha256 -cne $ref.manifestSha256 -or $a.packageBytes -ne $ref.packageBytes){throw 'identity-mismatch'}
        $p=@($a.protections|Where-Object id -CEQ $Protection);if($p.Count -ne 1){throw 'unknown-protection'};if(-not $p[0].releasedAt){$p[0].releasedAt=(& $script:Clock).ToString('o');$null=Write-EvidenceTask $config $task.taskId $task $task.generation $guard}
        return [pscustomobject]@{status='passed';reasonCode='released'}
    }catch{if($script:FailureHook){& $script:FailureHook $_}return [pscustomobject]@{status='failed';reasonCode=if($_.Exception.Message -cin @('invalid-evidence-config','unknown-deployment')){$_.Exception.Message}else{'unconfirmed'}}}finally{if($guard){$guard.Dispose()}}
}
# A promotion reuses an intent file only when an earlier promote stopped after
# writing it but before recording it. Anything that is not exactly the intent
# this artifact would get is unexplained residue, so nothing is sent beside it.
function Confirm-EvidencePromoteIntent([string]$Path,$Artifact){
    try{$item=Read-ArtifactJson $Path;$v=$item.Data;Assert-Fields $v @('schemaVersion','artifactId','key','packageBytes','packageSha256','createdAt','process')}catch{throw 'unresolved-put'}
    if($v.schemaVersion -isnot [long] -or $v.schemaVersion -ne 2 -or $v.artifactId -isnot [string] -or $v.artifactId -cne $Artifact.artifactId -or $v.key -isnot [string] -or $v.key -cne $Artifact.key -or ($v.packageBytes -isnot [long] -and $v.packageBytes -isnot [int]) -or $v.packageBytes -ne $Artifact.packageBytes -or $v.packageSha256 -isnot [string] -or $v.packageSha256 -cne $Artifact.packageSha256){throw 'unresolved-put'}
    return $item.Sha256
}
# Sends a staged package as it is: the ZIP already registered in ownedCopies,
# checked under the same read lock that the PUT runs inside, so the bytes sent
# are the bytes the reference and packageSha256 describe. Nothing is snapshotted
# or packaged again. The catalog is written twice: write A records the intent,
# put-pending and the readback staging before the PUT; write B records the
# receipt and ready only after a separate readback process has matched. A stop
# between them leaves put-pending for the recovery (match -> ready, 404 -> staged
# with the intent kept). One call sends at most once; a later call may send the
# same bytes to the same key again only after a recovery observed 404, which is
# safe because a PUT replaces the whole object with identical bytes. Publish
# keeps its own rule of never resending, since each publish is a new snapshot.
# Kept apart from publish so that both can later move into one send module.
function Send-EvidenceStagedPackage($Config,$Task,$Artifact,$Guard,[Diagnostics.Stopwatch]$Timer,$Track){
    if($script:FaultHook){& $script:FaultHook 'promote-before-lock' $Artifact}
    $zip=@($Artifact.ownedCopies|Where-Object {$_.sha256 -ceq $Artifact.packageSha256 -and $_.bytes -eq $Artifact.packageBytes})
    if($zip.Count -lt 1){throw 'identity-mismatch'}
    $zipPath=Resolve-ArtifactStoredPath $zip[0].path
    $lock=[IO.FileStream]::new($zipPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try{
        if($lock.Length -ne $Artifact.packageBytes -or [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($lock)).ToLowerInvariant() -cne $Artifact.packageSha256){throw 'identity-mismatch'}
        $intentPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Config $Task.taskId),'intents',$Artifact.artifactId+'.json')
        # A recorded intent is never rewritten: a resend reuses the exact file
        # whose hash the catalog holds, with its original createdAt and process.
        if($Artifact.putIntent -and ((Resolve-EvidenceStoredPath $Artifact.putIntent.path) -cne $intentPath -or -not [IO.File]::Exists($intentPath))){throw 'unresolved-put'}
        if([IO.File]::Exists($intentPath)){$intentSha=Confirm-EvidencePromoteIntent $intentPath $Artifact;if($Artifact.putIntent -and $intentSha -cne $Artifact.putIntent.sha256){throw 'unresolved-put'}}
        else{$intentSha=Write-EvidenceAtomic $intentPath ([pscustomobject]@{schemaVersion=2;artifactId=$Artifact.artifactId;key=$Artifact.key;packageBytes=$Artifact.packageBytes;packageSha256=$Artifact.packageSha256;createdAt=$Artifact.createdAt;process=Get-EvidenceProcessIdentity}) -Immutable}
        if($script:FaultHook){& $script:FaultHook 'promote-after-intent-file' $Artifact}
        $generation=(Get-CredentialStatus 'osm').Generation
        # The directory exists before the catalog names it, so a stop in between
        # leaves only an unregistered empty directory, as publish does.
        $operation=New-ArtifactOperation;$owner=New-EvidenceOperation $operation
        $stage=[pscustomobject]@{path=$operation;createdAt=(& $script:Clock).ToString('o');origin='storage-copy';artifactId=$Artifact.artifactId;adopted=$false;adoptionPending=$false;operation=$owner;deleteIntent=$null;protections=@()}
        $Task.staging+=$stage;if(-not $Artifact.putIntent){$Artifact.putIntent=[pscustomobject]@{path=$intentPath;sha256=$intentSha}};$Artifact.state='put-pending';$Artifact.operation=$owner;$Track.StagePath=$operation
        $null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard
        if($script:FaultHook){& $script:FaultHook 'promote-after-put-pending' $Artifact}
        $Track.Sent=$true
        $put=Invoke-ArtifactNetwork $generation (New-ArtifactRequest $Config 'put' $Artifact.key $zipPath $null $Timer)
        if(-not (Test-ArtifactSuccess $put 'put' $generation)){throw 'put-unconfirmed'}
        $read=Invoke-ArtifactReadback $operation $Config $generation $Artifact.key $Artifact.packageBytes $Artifact.packageSha256 $Timer
        if(-not (Test-ArtifactReadback $read $generation $Artifact.key $Artifact.packageBytes $Artifact.packageSha256)){throw 'readback-unconfirmed'}
    }finally{$lock.Dispose()}
    $receipt=[pscustomobject]@{schemaVersion=2;deploymentId=$Config.Data.deploymentId;repositoryId=$Config.Data.repositoryId;taskId=$Task.taskId;artifactId=$Artifact.artifactId;key=$Artifact.key;base=$Artifact.base;head=$Artifact.head;packageBytes=$Artifact.packageBytes;packageSha256=$Artifact.packageSha256;manifestSha256=$Artifact.manifestSha256;entries=$Artifact.entries;putIntentSha256=$intentSha;readback=$read;completedAt=(& $script:Clock).ToString('o')}
    $receiptPath=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Config $Task.taskId),'receipts',$Artifact.artifactId+'.json');$receiptSha=Write-EvidenceAtomic $receiptPath $receipt -Immutable
    if($script:FaultHook){& $script:FaultHook 'promote-after-receipt-file' $Artifact}
    # ownedCopies only grows: the staged ZIP and snapshot stay registered, so the
    # artifact's retention still covers every copy it had before promotion. Only
    # the staging registered by write A is adopted; no other staging is touched.
    $Artifact.receipt=[pscustomobject]@{path=$receiptPath;sha256=$receiptSha};$Artifact.state='ready';$Artifact.operation=$null;$stage.adopted=$true;$stage.operation=$null
    Protect-ArtifactTree $operation
    foreach($file in [IO.Directory]::EnumerateFiles($operation,'*',[IO.SearchOption]::AllDirectories)){Add-EvidenceOwnedFile $Artifact $file ([IO.FileInfo]::new($file)).Length ((Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant())}
    $null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard
}
function Set-EvidencePromoted($Result,[string]$ReferenceText,$Artifact,[string]$ReasonCode){
    # The receipt is named by its hash only; its path stays out of CLI output.
    $Result.status='passed';$Result.reasonCode=$ReasonCode;$Result.reference=$ReferenceText;$Result.packageSha256=$Artifact.packageSha256;$Result.locality='promoted';$Result.receipt=[pscustomobject]@{sha256=$Artifact.receipt.sha256};$Result.residue.remote='ready'
}
# Puts a staged artifact on the remote store under the same reference and
# packageSha256. Promotion uses the ordinary network preflight, so an unfinished
# put or delete in the task is resolved by the existing recovery first.
function Invoke-EvidencePromote([string]$ConfigPath,[string]$ReferenceText,[string]$ExpectedHash){
    $result=[ordered]@{status='failed';reasonCode='unconfirmed';reference=$null;packageSha256=$null;locality=$null;receipt=$null;residue=@{remote='pre-put'};stagingMissing=0}
    $guard=$null;$config=$null;$ref=$null;$track=$null;$timer=[Diagnostics.Stopwatch]::StartNew()
    $staging=[pscustomobject]@{Observations=[Collections.Generic.List[object]]::new();Witnesses=0}
    try{
        Assert-Hex $ExpectedHash 64;$config=Read-EvidenceConfig $ConfigPath;$ref=Read-EvidenceReference $ReferenceText $config
        $guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $ref.taskId
        $null=Assert-EvidenceTransitionSafe $config $ref.taskId $guard -Residue $staging -RecoverArtifactId $ref.artifactId;$task=Sync-EvidenceTaskGuarded $config $ref.taskId $guard
        $found=@($task.artifacts|Where-Object artifactId -CEQ $ref.artifactId);if($found.Count -ne 1){throw 'catalog-missing'};$a=$found[0]
        if($a.state -ceq 'deleted'){throw 'expired'}
        # Identity is settled before any branch, so even an already promoted answer
        # names only the artifact the reference and expected hash describe.
        if($a.packageSha256 -cne $ExpectedHash -or $a.key -cne $ref.key -or $a.base -cne $ref.base -or $a.head -cne $ref.head -or $a.manifestSha256 -cne $ref.manifestSha256 -or $a.packageBytes -ne $ref.packageBytes){throw 'identity-mismatch'}
        $origin=Get-EvidenceArtifactOrigin $a
        # Sends nothing and changes no state, so an ended task answers the same.
        if($a.state -ceq 'ready' -and $origin -ceq 'stage'){Set-EvidencePromoted $result $ReferenceText $a 'already-promoted'}
        # The state after preflight decides: preflight may just have returned this
        # artifact's put-pending or failed promotion to staged on a recovery 404.
        # A staged artifact is sent again only as the same bytes under the same
        # intent (Send-EvidenceStagedPackage checks the intent); put-pending,
        # failed or a receipt is never sent.
        elseif($a.state -cne 'staged' -or $origin -cne 'stage' -or $null -ne $a.receipt){throw 'invalid-source'}
        elseif($task.status -cne 'active'){throw 'resume-required'}
        else{
            $track=[pscustomobject]@{StagePath=$null;Sent=$false}
            Send-EvidenceStagedPackage $config $task $a $guard $timer $track
            Set-EvidencePromoted $result $ReferenceText $a 'complete'
        }
    }catch{if($script:FailureHook){& $script:FailureHook $_}
        if($_.Exception.Message -cin @('unsupported-schema','invalid-reference','invalid-evidence-config','unknown-deployment','task-not-registered','catalog-missing','expired','identity-mismatch','invalid-source','resume-required','unresolved-put','unresolved-delete','changed-copy','operation-pending')){$result.reasonCode=$_.Exception.Message}
        if($track){Complete-EvidencePromoteFailure $config $ref.taskId $ref.artifactId $guard $track $result $ReferenceText}
    }finally{if($guard){$guard.Dispose()}}
    $result.stagingMissing=$staging.Observations.Count
    return [pscustomobject]$result
}
# After a failed send the catalog on disk is the truth: the in-memory task may
# carry an unpersisted state, receipt or a generation already advanced by a write
# that never landed, so none of it is written back. A persisted put-pending only
# drops the operation marker of this promote, and only when no readback child can
# still be running; otherwise the next preflight decides with the same check.
function Complete-EvidencePromoteFailure($Config,[string]$TaskId,[string]$ArtifactId,$Guard,$Track,$Result,[string]$ReferenceText){
    try{
        $disk=Read-EvidenceTask $Config $TaskId $Guard;if($null -eq $disk){throw 'catalog-missing'}
        $a=@($disk.artifacts|Where-Object artifactId -CEQ $ArtifactId)[0]
        if($a.state -ceq 'ready'){Set-EvidencePromoted $Result $ReferenceText $a 'complete';return}
        if($a.state -cne 'put-pending'){return}
        $Result.status='failed';$Result.reasonCode='unconfirmed';$Result.residue.remote=if($Track.Sent){'may-have-been-sent'}else{'pre-put'}
        if(-not $a.operation){return}
        $released=$true
        try{$marker=Resolve-ArtifactStoredPath $a.operation.childMarker -AllowMissing;if([IO.File]::Exists($marker)){$child=(Read-ArtifactJson $marker).Data;$released=$child.exited -and $child.pipesClosed}}catch{$released=$false}
        if(-not $released){return}
        $a.operation=$null;foreach($s in @($disk.staging|Where-Object path -CEQ $Track.StagePath)){$s.operation=$null}
        $null=Write-EvidenceTask $Config $TaskId $disk $disk.generation $Guard
    }catch{if($script:FailureHook){& $script:FailureHook $_};$Result.status='failed';$Result.reasonCode='unconfirmed';$Result.reference=$null;$Result.packageSha256=$null;$Result.locality=$null;$Result.receipt=$null}
}
Export-ModuleMember -Function Invoke-EvidencePublish,Invoke-EvidenceStage,Invoke-EvidenceFetch,Invoke-EvidenceInspect,Invoke-EvidenceUse,Invoke-EvidenceRelease,Invoke-EvidencePromote
