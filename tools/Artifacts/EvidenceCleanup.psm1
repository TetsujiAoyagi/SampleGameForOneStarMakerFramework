Set-StrictMode -Version Latest

# This is an invocation-local deadline, never a persisted policy clock or CLI input.
$script:Budget=$null
$script:ElapsedClock={ [Diagnostics.Stopwatch]::GetTimestamp() }
function Assert-EvidenceBudget { if($script:Budget -and (& $script:Budget.Remaining) -le 0){throw 'evidence-deadline'} }

function New-EvidenceLocalBudget([long]$Limit,$Parent=$null){
    $clock=$script:ElapsedClock;$started=[long](& $clock);$frequency=[Diagnostics.Stopwatch]::Frequency
    $hard={ $left=$Limit-[long](([long](& $clock)-$started)*1000.0/$frequency);if($Parent){$left=[Math]::Min($left,[long](& $Parent.HardRemaining))};return [Math]::Max(0,$left) }.GetNewClosure()
    # Reserve one second inside each phase for the final small job journal. Sync
    # never borrows cleanup time; a caller's remaining deadline can only shrink.
    $remaining={ return [Math]::Max(0,([long](& $hard)-1000)) }.GetNewClosure()
    return [pscustomobject]@{Remaining=$remaining;HardRemaining=$hard}
}
function Set-EvidenceBudgetScopes([string[]]$Names,$Budget){
    $paths=@{'Invoke-EvidenceSync'='EvidenceStateStore.psm1';'Invoke-EvidenceCleanup'='EvidenceCleanup.psm1';'Sync-EvidenceTaskGuarded'='EvidenceStateStore.psm1';'Enter-WorkflowTaskGuard'='../Workflow/TaskEventStore.psm1';'Write-EvidenceAtomic'='EvidencePaths.psm1';'Get-Remaining'='ArtifactApplication.psm1'}
    $seen=[Collections.Generic.HashSet[object]]::new();$owners=[Collections.Generic.List[object]]::new()
    foreach($name in $Names){
        $owner=(Get-Command $name -ErrorAction Stop).Module;$expected=[IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,$paths[$name]))
        if(-not $owner -or $owner.Path -ine $expected){throw 'budget-owner-unavailable'}
        if($seen.Add($owner)){$owners.Add($owner)}
    }
    $scopes=[Collections.Generic.List[object]]::new()
    try{foreach($owner in $owners){$previous=& $owner {$script:Budget};$scopes.Add([pscustomobject]@{Owner=$owner;Previous=$previous});& $owner {param($b)$script:Budget=$b} $Budget}}
    catch{Restore-EvidenceBudgetScopes $scopes.ToArray();throw}
    return ,$scopes.ToArray()
}
function Restore-EvidenceBudgetScopes($Scopes){
    for($i=@($Scopes).Count-1;$i -ge 0;$i--){& $Scopes[$i].Owner {param($b)$script:Budget=$b} $Scopes[$i].Previous}
}
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidencePaths.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/TaskEventStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceRetentionPolicy.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceStateStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1')
$script:Clock={ [DateTimeOffset]::UtcNow }
$script:FaultHook=$null
$script:ProcessHook=$null
function Get-EvidenceProcessIdentity { $p=[Diagnostics.Process]::GetCurrentProcess();try{return [pscustomobject]@{pid=$p.Id;startedAt=$p.StartTime.ToUniversalTime().ToString('o')}}finally{$p.Dispose()} }
function Test-EvidenceProcessStopped($Identity){
    if($script:ProcessHook){return [bool](& $script:ProcessHook $Identity)}
    if($null -eq $Identity -or $Identity.pid -lt 1){return $false}
    try{$p=[Diagnostics.Process]::GetProcessById($Identity.pid);try{return $p.HasExited -or $p.StartTime.ToUniversalTime().ToString('o') -cne $Identity.startedAt}finally{$p.Dispose()}}catch [ArgumentException]{return $true}catch{return $false}
}
function Assert-EvidenceOperationStopped($Operation){
    if(-not (Test-EvidenceProcessStopped $Operation.process)){throw 'operation-pending'}
    if($Operation.childMarker -and [IO.File]::Exists($Operation.childMarker)){
        $child=(Read-ArtifactJson $Operation.childMarker).Data
        if(-not $child.exited -or -not $child.pipesClosed){if(-not (Test-EvidenceProcessStopped $child.process)){throw 'operation-pending'}}
    }
}
function Assert-EvidenceTreeNoReparse([string]$Root){
    Assert-EvidenceBudget
    $pending=[Collections.Generic.Queue[string]]::new();$pending.Enqueue($Root);$count=0
    while($pending.Count){
        Assert-EvidenceBudget;$path=$pending.Dequeue();Assert-NoArtifactReparse $path
        foreach($child in [IO.Directory]::EnumerateFileSystemEntries($path,'*',[IO.SearchOption]::TopDirectoryOnly)){
            Assert-EvidenceBudget;Assert-NoArtifactReparse $child;if(++$count -gt 32768){throw 'staging-limit'}
            if([IO.Directory]::Exists($child)){$pending.Enqueue($child)}
        }
    }
}
# Hash and remove only registered storage copies, cooperatively between bounded
# reads/tree entries. An expired deadline retains the existing partial ownership.
function Get-EvidenceOwnedHash([string]$Path){
    $f=[IO.FileStream]::new($Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read);$hash=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
    try{$buffer=[byte[]]::new(65536);while($true){Assert-EvidenceBudget;$n=$f.Read($buffer,0,$buffer.Length);if(-not $n){break};$hash.AppendData($buffer,0,$n)};return [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()}finally{$f.Dispose();$hash.Dispose()}
}
function Remove-EvidenceStagingTree([string]$Root){
    Assert-EvidenceTreeNoReparse $Root
    $directories=[Collections.Generic.List[string]]::new();$pending=[Collections.Generic.Queue[string]]::new();$pending.Enqueue($Root);$count=0
    while($pending.Count){Assert-EvidenceBudget;$path=$pending.Dequeue();$directories.Add($path);foreach($child in [IO.Directory]::EnumerateFileSystemEntries($path,'*',[IO.SearchOption]::TopDirectoryOnly)){Assert-EvidenceBudget;if(++$count -gt 32768){throw 'staging-limit'};Assert-NoArtifactReparse $child;if([IO.Directory]::Exists($child)){$pending.Enqueue($child)}else{[IO.File]::Delete($child)}}}
    for($i=$directories.Count-1;$i -ge 0;$i--){Assert-EvidenceBudget;Assert-NoArtifactReparse $directories[$i];[IO.Directory]::Delete($directories[$i])}
}
function Remove-EvidenceOwnedCopies($Artifact){
    $remaining=[Collections.Generic.List[object]]::new()
    foreach($copy in $Artifact.ownedCopies){
        try{Assert-EvidenceBudget
            if($copy.origin -cne 'storage-copy' -or -not [IO.Path]::IsPathFullyQualified($copy.path) -or -not (Test-ArtifactWithin $copy.path (Get-ArtifactRoot))){throw 'invalid-owned-copy'}
            Assert-NoArtifactReparse $copy.path
            if([IO.File]::Exists($copy.path)){
                if(([IO.FileInfo]::new($copy.path)).Length -ne $copy.bytes -or (Get-EvidenceOwnedHash $copy.path) -cne $copy.sha256){throw 'changed-copy'}
                Assert-EvidenceBudget;Assert-ArtifactAcl $copy.path $false;[IO.File]::Delete($copy.path)
                $parent=[IO.Path]::GetDirectoryName($copy.path);$root=Get-ArtifactRoot
                while($parent -cne $root -and (Test-ArtifactWithin $parent $root) -and [IO.Directory]::Exists($parent) -and -not [IO.Directory]::EnumerateFileSystemEntries($parent).GetEnumerator().MoveNext()){Assert-EvidenceBudget;Assert-NoArtifactReparse $parent;[IO.Directory]::Delete($parent);$parent=[IO.Path]::GetDirectoryName($parent)}
            }
        }catch{$remaining.Add($copy)}
    }
    $Artifact.ownedCopies=@($remaining.ToArray());return $remaining.Count -eq 0
}
# Recovery GETs can leave complete or partial bytes even when their remote outcome is
# unknown. Register the existing staging ownership before requesting any bytes so a
# later worker can clear the stopped operation and apply the ordinary seven-day rule.
function New-EvidenceRecoveryStage($Config,$Task,$Artifact,$Guard){
    Assert-EvidenceBudget
    $path=New-ArtifactOperation
    $stage=[pscustomobject]@{path=$path;createdAt=(& $script:Clock).ToString('o');origin='storage-copy';artifactId=$Artifact.artifactId;adopted=$false;adoptionPending=$false;operation=[pscustomobject]@{process=Get-EvidenceProcessIdentity;childMarker=[IO.Path]::Combine($path,'readback-process.json');path=$path};deleteIntent=$null;protections=@()}
    $Task.staging+=$stage;$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard
    return $stage
}
function Resolve-EvidenceOperationGuarded($Config,$Task,$Artifact,$Guard){
    if($Artifact.operation){Assert-EvidenceOperationStopped $Artifact.operation;$Artifact.operation=$null;$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard}
}
function Resolve-EvidenceDeleteGuarded($Config,$Task,$Artifact,$Guard,[Diagnostics.Stopwatch]$Timer){
    if(-not $Artifact.deleteIntent){return $true}
    if(-not (Test-EvidenceProcessStopped $Artifact.deleteIntent.process)){throw 'unresolved-delete'}
    $generation=(Get-CredentialStatus 'osm').Generation
    $stage=New-EvidenceRecoveryStage $Config $Task $Artifact $Guard;$destination=[IO.Path]::Combine($stage.path,'reconcile.bin')
    try{
        $get=Invoke-ArtifactNetwork $generation (New-ArtifactRequest $Config 'get' $Artifact.key $null $destination $Timer)
        if(Test-ArtifactMissing $get $generation){$Artifact.state='deleted';$Artifact.deleteIntent=$null;$Artifact.operation=$null;$null=Remove-EvidenceOwnedCopies $Artifact;$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard;return $true}
        if((Test-ArtifactSuccess $get 'get' $generation) -and $get.Bytes -eq $Artifact.packageBytes -and $get.Sha256 -ceq $Artifact.packageSha256){
            # A future cleanup obtains a new intent after reevaluating policy. A
            # timeout never implies DELETE success, and this GET remains staging-owned.
            $Artifact.state='ready';$Artifact.deleteIntent=$null;$Artifact.operation=$null;$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard;return $true
        }
        throw 'unresolved-delete'
    }finally{
        # This synchronous confirmation GET launches no readback child.
        $stage.operation=$null;$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard
    }
}
function Assert-EvidenceTransitionSafe($Config,[string]$TaskId,$Guard){
    $task=Read-EvidenceTask $Config $TaskId $Guard;if($null -eq $task){return @()}
    $task=Sync-EvidenceTaskGuarded $Config $TaskId $Guard
    $timer=[Diagnostics.Stopwatch]::StartNew()
    foreach($a in $task.artifacts){
        if($a.deleteIntent){$null=Resolve-EvidenceDeleteGuarded $Config $task $a $Guard $timer}
        if($a.state -ceq 'put-pending'){Resolve-EvidencePutGuarded $Config $task $a $Guard $timer}
        elseif($a.operation){Resolve-EvidenceOperationGuarded $Config $task $a $Guard}
    }
    return @($task.artifacts|Where-Object state -CEQ 'deleted'|ForEach-Object artifactId)
}
function Invoke-EvidenceDeleteGuarded($Config,$Task,$Artifact,$Guard,[Diagnostics.Stopwatch]$Timer){
    Assert-EvidenceBudget
    $now=& $script:Clock;$policy=Get-EvidenceRetentionDecision $Task $Artifact $now
    if(-not $policy.eligible){return $policy.reasonCode}
    $generation=(Get-CredentialStatus 'osm').Generation
    $Artifact.deleteIntent=[pscustomobject]@{key=$Artifact.key;generation=$Task.generation;packageSha256=$Artifact.packageSha256;process=Get-EvidenceProcessIdentity;createdAt=$now.ToString('o')}
    $Artifact.state='delete-pending';$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard
    if($script:FaultHook){& $script:FaultHook 'after-intent' $Artifact}
    Assert-EvidenceBudget
    $delete=Invoke-ArtifactNetwork $generation (New-ArtifactRequest $Config 'delete' $Artifact.key $null $null $Timer)
    if(-not (Test-ArtifactSuccess $delete 'delete' $generation)){return 'delete-pending'}
    if($script:FaultHook){& $script:FaultHook 'after-remote' $Artifact}
    $stage=New-EvidenceRecoveryStage $Config $Task $Artifact $Guard
    try{$get=Invoke-ArtifactNetwork $generation (New-ArtifactRequest $Config 'get' $Artifact.key $null ([IO.Path]::Combine($stage.path,'absence.bin')) $Timer)}
    finally{$stage.operation=$null;$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard}
    if(-not (Test-ArtifactMissing $get $generation)){return 'delete-pending'}
    $Artifact.state='deleted';$Artifact.deleteIntent=$null;$Artifact.operation=$null
    $complete=Remove-EvidenceOwnedCopies $Artifact
    $null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard
    return $(if($complete){'deleted'}else{'local-pending'})
}
function Invoke-EvidenceCleanup([string]$ConfigPath,[bool]$DryRun=$false,[string[]]$TaskIds=@(),[int]$MaxObjects=100){
    $result=[ordered]@{status='passed';reasonCode='complete';deleted=0;skipped=0;blocked=0;remaining=0;items=@()};$timer=[Diagnostics.Stopwatch]::StartNew();$seen=0
    $previousBudget=$script:Budget;if(-not $script:Budget){$script:Budget=New-EvidenceLocalBudget 480000};$scopes=@()
    try{
        $scopes=Set-EvidenceBudgetScopes @('Sync-EvidenceTaskGuarded','Enter-WorkflowTaskGuard','Write-EvidenceAtomic','Get-Remaining') $script:Budget;Assert-EvidenceBudget
        $config=Read-EvidenceConfig $ConfigPath;if(-not $TaskIds.Count){$TaskIds=@(Get-WorkflowTasks $config.Data.repositoryId)}
        foreach($id in $TaskIds){
            $guard=$null
            try{
                if((& $script:Budget.Remaining) -le 0){$result.remaining++;continue}
                $guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $id;$task=Sync-EvidenceTaskGuarded $config $id $guard
                # Artifacts and staging share one finite cursor. Persisting it before
                # each attempt lets a blocked artifact or staging tree yield the next
                # job's budget instead of starving later expired staging forever.
                $work=@($task.artifacts|ForEach-Object{[pscustomobject]@{cursor=$_.artifactId;artifact=$_;stage=$null}})+@($task.staging|ForEach-Object{
                    $key=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($_.path))).ToLowerInvariant()
                    [pscustomobject]@{cursor='staging:'+$key;artifact=$null;stage=$_}
                })
                $ordered=@($work|Sort-Object cursor);if($task.cleanupCursor){$ordered=@($ordered|Where-Object {$_.cursor -cgt $task.cleanupCursor})+@($ordered|Where-Object {$_.cursor -cle $task.cleanupCursor})}
                foreach($item in $ordered){
                    if($seen -ge $MaxObjects -or (& $script:Budget.Remaining) -le 0){$result.remaining++;continue};$seen++
                    if(-not $DryRun){$task.cleanupCursor=$item.cursor;$null=Write-EvidenceTask $config $id $task $task.generation $guard}
                    if($item.stage){
                        $stage=$item.stage
                        $observation=[pscustomobject]@{taskId=$id;artifactId=$stage.artifactId;reasonCode='unconfirmed'};$result.items+=$observation
                        if($stage.artifactId -and @($task.artifacts|Where-Object {$_.artifactId -ceq $stage.artifactId -and ($_.state -ceq 'put-pending' -or $_.deleteIntent)}).Count){$result.skipped++;$observation.reasonCode='operation-pending';continue}
                        if($stage.operation -and -not $DryRun){try{Assert-EvidenceOperationStopped $stage.operation;$stage.operation=$null}catch{$result.blocked++;$observation.reasonCode='operation-pending';continue}}
                        $policy=Get-EvidenceStagingDecision $stage (& $script:Clock);$observation.reasonCode=$policy.reasonCode
                        if(-not $policy.eligible){$result.skipped++;continue}
                        if(-not $DryRun){
                            try{
                                if(-not [IO.Path]::IsPathFullyQualified($stage.path) -or -not (Test-ArtifactWithin $stage.path (Get-ArtifactRoot)) -or $stage.path -ceq (Get-ArtifactRoot)){throw 'invalid-staging'}
                                Assert-NoArtifactReparse $stage.path
                                if([IO.Directory]::Exists($stage.path)){
                                    Remove-EvidenceStagingTree $stage.path
                                }
                                $task.staging=@($task.staging|Where-Object path -CNE $stage.path);$null=Write-EvidenceTask $config $id $task $task.generation $guard;$result.deleted++;$observation.reasonCode='deleted'
                            }catch{$result.blocked++;$observation.reasonCode='local-pending'}
                        }else{$result.skipped++}
                        continue
                    }
                    $a=$item.artifact
                    if($a.state -ceq 'put-pending' -and -not $DryRun){Resolve-EvidencePutGuarded $config $task $a $guard $timer}
                    if($a.deleteIntent -and -not $DryRun){$null=Resolve-EvidenceDeleteGuarded $config $task $a $guard $timer}
                    if($a.operation -and -not $DryRun){Resolve-EvidenceOperationGuarded $config $task $a $guard}
                    if($a.state -ceq 'deleted'){$reason=if($DryRun -or (Remove-EvidenceOwnedCopies $a)){'payload-deleted'}else{'local-pending'};if(-not $DryRun){$null=Write-EvidenceTask $config $id $task $task.generation $guard}}
                    else{$policy=Get-EvidenceRetentionDecision $task $a (& $script:Clock);$reason=$policy.reasonCode;if($policy.eligible -and -not $DryRun){$reason=Invoke-EvidenceDeleteGuarded $config $task $a $guard $timer}}
                    if($reason -ceq 'deleted'){$result.deleted++}elseif($reason -cin @('delete-pending','local-pending','unknown-state','unknown-use','delivery-pending')){$result.blocked++}else{$result.skipped++}
                    $result.items+= [pscustomobject]@{taskId=$id;artifactId=$a.artifactId;reasonCode=$reason}
                }
            }catch{$result.blocked++;$result.items+= [pscustomobject]@{taskId=$id;artifactId=$null;reasonCode='unconfirmed'}}finally{if($guard){$guard.Dispose()}}
        }
    }catch{$result.blocked++}finally{Restore-EvidenceBudgetScopes $scopes;$script:Budget=$previousBudget}
    if($result.blocked -or $result.remaining){$result.status='pending';$result.reasonCode='cleanup-partial'}
    return [pscustomobject]$result
}
Export-ModuleMember -Function Get-EvidenceProcessIdentity,Assert-EvidenceOperationStopped,Assert-EvidenceTransitionSafe,Invoke-EvidenceCleanup


function Resolve-EvidencePutGuarded($Config,$Task,$Artifact,$Guard,[Diagnostics.Stopwatch]$Timer){
    if($Artifact.state -cne 'put-pending'){return}
    if($Artifact.operation){Assert-EvidenceOperationStopped $Artifact.operation}
    if(-not $Artifact.putIntent){$Artifact.state='failed';$Artifact.operation=$null;$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard;return}
    $intent=Read-ArtifactJson $Artifact.putIntent.path
    if($intent.Sha256 -cne $Artifact.putIntent.sha256 -or $intent.Data.key -cne $Artifact.key -or $intent.Data.packageSha256 -cne $Artifact.packageSha256 -or $intent.Data.artifactId -cne $Artifact.artifactId){throw 'unresolved-put'}
    $gen=(Get-CredentialStatus 'osm').Generation;$stage=New-EvidenceRecoveryStage $Config $Task $Artifact $Guard;$op=$stage.path;$archive=[IO.Path]::Combine($op,'recovery.zip')
    $Artifact.operation=$stage.operation;$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard
    $get=Invoke-ArtifactNetwork $gen (New-ArtifactRequest $Config 'get' $Artifact.key $null $archive $Timer)
    if(Test-ArtifactMissing $get $gen){$stage.operation=$null;$Artifact.state='failed';$Artifact.operation=$null;$null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard;return}
    if(-not (Test-ArtifactSuccess $get 'get' $gen) -or $get.Bytes -ne $Artifact.packageBytes -or $get.Sha256 -cne $Artifact.packageSha256){throw 'unresolved-put'}
    Import-ArtifactPackage
    $package=[OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($archive,$op,$Artifact.packageSha256,$Artifact.manifestSha256,$Artifact.base,$Artifact.head,$Artifact.repositoryId,$Artifact.artifactId,(Get-Remaining $Timer 600000),'evidence',$Artifact.taskId)
    if($package.Files.Count -ne $Artifact.entries.Count){throw 'unresolved-put'}
    foreach($e in $package.Files){Assert-EvidenceBudget;$found=@($Artifact.entries|Where-Object path -CEQ $e.Path);if($found.Count -ne 1 -or $found[0].bytes -ne $e.Bytes -or $found[0].sha256 -cne $e.Sha256){throw 'unresolved-put'}}
    $read=Invoke-ArtifactReadback $op $Config $gen $Artifact.key $Artifact.packageBytes $Artifact.packageSha256 $Timer
    if(-not (Test-ArtifactReadback $read $gen $Artifact.key $Artifact.packageBytes $Artifact.packageSha256)){throw 'unresolved-put'}
    $path=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Config $Task.taskId),'receipts',$Artifact.artifactId+'.json')
    if([IO.File]::Exists($path)){$receipt=Read-ArtifactJson $path;if($receipt.Data.key -cne $Artifact.key -or $receipt.Data.packageSha256 -cne $Artifact.packageSha256 -or $receipt.Data.manifestSha256 -cne $Artifact.manifestSha256){throw 'receipt-conflict'};$hash=$receipt.Sha256}
    else{$receipt=[pscustomobject]@{schemaVersion=2;deploymentId=$Config.Data.deploymentId;repositoryId=$Config.Data.repositoryId;taskId=$Task.taskId;artifactId=$Artifact.artifactId;key=$Artifact.key;base=$Artifact.base;head=$Artifact.head;packageBytes=$Artifact.packageBytes;packageSha256=$Artifact.packageSha256;manifestSha256=$Artifact.manifestSha256;entries=$Artifact.entries;putIntentSha256=$Artifact.putIntent.sha256;readback=$read;completedAt=(& $script:Clock).ToString('o')};$hash=Write-EvidenceAtomic $path $receipt -Immutable}
    $Artifact.receipt=[pscustomobject]@{path=$path;sha256=$hash};$Artifact.state='ready';$Artifact.operation=$null
    Assert-EvidenceTreeNoReparse $op
    foreach($f in [IO.Directory]::EnumerateFiles($op,'*',[IO.SearchOption]::AllDirectories)){Assert-EvidenceBudget;$Artifact.ownedCopies+=[pscustomobject]@{path=$f;bytes=([IO.FileInfo]::new($f)).Length;sha256=Get-EvidenceOwnedHash $f;origin='storage-copy'}}
    Protect-ArtifactTree $op;$stage.adopted=$true;$stage.operation=$null
    foreach($s in $Task.staging){if($s.artifactId -ceq $Artifact.artifactId){$s.adopted=$true;$s.operation=$null}}
    $null=Write-EvidenceTask $Config $Task.taskId $Task $Task.generation $Guard
}
