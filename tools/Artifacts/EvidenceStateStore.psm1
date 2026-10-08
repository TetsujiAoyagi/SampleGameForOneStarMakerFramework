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
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidenceContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'EvidencePaths.psm1')
Import-Module (Join-Path $PSScriptRoot '../Workflow/TaskEventStore.psm1')
function Assert-EvidenceGuard($Config,[string]$TaskId,$Guard){
    if($null -eq $Guard -or $Guard.RepositoryId -cne $Config.Data.repositoryId -or $Guard.TaskId -cne $TaskId){throw 'invalid-guard'}
    if($Guard.PSObject.Properties['Handle'] -and ($null -eq $Guard.Handle -or -not $Guard.Handle.CanRead)){throw 'invalid-guard'}
}
function New-EvidenceTask($Config,[string]$TaskId){return [pscustomobject][ordered]@{schemaVersion=2;deploymentId=$Config.Data.deploymentId;repositoryId=$Config.Data.repositoryId;taskId=$TaskId;generation=[long]0;appliedEventVersion=[long]0;status='unknown';taskEndedAt=$null;endReason=$null;deleteEligibleAt=$null;events=@();artifacts=@();staging=@();cleanupCursor=$null}}
function Read-EvidenceTask($Config,[string]$TaskId,$Guard){
    Assert-EvidenceBudget
    Assert-EvidenceGuard $Config $TaskId $Guard;$path=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Config $TaskId),'catalog.json')
    if(-not [IO.File]::Exists($path)){return $null};Assert-ArtifactAcl $path $false
    $v=(Read-ArtifactJson $path).Data
    Assert-Fields $v @('schemaVersion','deploymentId','repositoryId','taskId','generation','appliedEventVersion','status','taskEndedAt','endReason','deleteEligibleAt','events','artifacts','staging','cleanupCursor')
    if($v.schemaVersion -ne 2 -or $v.deploymentId -cne $Config.Data.deploymentId -or $v.repositoryId -cne $Config.Data.repositoryId -or $v.taskId -cne $TaskId -or $v.generation -lt 1 -or $v.appliedEventVersion -lt 0 -or $v.status -cnotin @('unknown','active','ended','pending-gap') -or $v.events -isnot [array] -or $v.artifacts -isnot [array] -or $v.staging -isnot [array]){throw 'corrupt-state'}
    $ids=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($a in $v.artifacts){Assert-EvidenceBudget
        Assert-Fields $a @('schemaVersion','artifactId','taskId','repositoryId','deploymentId','key','base','head','packageBytes','packageSha256','manifestSha256','entries','createdAt','state','appliedEventVersion','taskEndedAt','endReason','deleteEligibleAt','protections','ownedCopies','deleteIntent','operation','receipt','putIntent')
        Assert-Hex $a.artifactId 32;Assert-Hex $a.packageSha256 64;Assert-Hex $a.manifestSha256 64;Assert-Hex $a.base 40;Assert-Hex $a.head 40
        if(-not $ids.Add($a.artifactId) -or $a.schemaVersion -ne 2 -or $a.taskId -cne $TaskId -or $a.repositoryId -cne $Config.Data.repositoryId -or $a.deploymentId -cne $Config.Data.deploymentId -or $a.key -cne (Get-EvidenceKey $Config $TaskId $a.artifactId) -or $a.state -cnotin @('put-pending','ready','delete-pending','deleted','failed') -or $a.packageBytes -lt 1 -or $a.packageBytes -gt 256MB){throw 'corrupt-state'}
        $null=Assert-Utc $a.createdAt
        if($a.entries -isnot [array] -or $a.entries.Count -lt 1 -or $a.entries.Count -gt 4096 -or $a.protections -isnot [array] -or $a.ownedCopies -isnot [array]){throw 'corrupt-state'}
        foreach($entry in $a.entries){Assert-EvidenceBudget;Assert-Fields $entry @('path','bytes','sha256');Assert-Hex $entry.sha256 64;if($entry.bytes -lt 0 -or $entry.bytes -gt 256MB){throw 'corrupt-state'}}
        if($a.state -cin @('ready','deleted','delete-pending')){
            if($null -eq $a.receipt){throw 'receipt-missing'}
            $expected=[IO.Path]::Combine((Get-EvidenceTaskDirectory $Config $TaskId),'receipts',$a.artifactId+'.json')
            if($a.receipt.path -cne $expected){throw 'receipt-conflict'}
            $receipt=Read-ArtifactJson $expected;Assert-EvidenceBudget
            if($receipt.Sha256 -cne $a.receipt.sha256 -or $receipt.Data.taskId -cne $TaskId -or $receipt.Data.repositoryId -cne $Config.Data.repositoryId -or $receipt.Data.deploymentId -cne $Config.Data.deploymentId -or $receipt.Data.artifactId -cne $a.artifactId -or $receipt.Data.key -cne $a.key -or $receipt.Data.base -cne $a.base -or $receipt.Data.head -cne $a.head -or $receipt.Data.packageSha256 -cne $a.packageSha256 -or $receipt.Data.manifestSha256 -cne $a.manifestSha256 -or $receipt.Data.packageBytes -ne $a.packageBytes){throw 'receipt-conflict'}
        }
    }
    return $v
}
function Write-EvidenceTask($Config,[string]$TaskId,$State,[long]$ExpectedGeneration,$Guard){
    Assert-EvidenceBudget
    Assert-EvidenceGuard $Config $TaskId $Guard;$old=Read-EvidenceTask $Config $TaskId $Guard
    $actual=if($null -eq $old){0}else{$old.generation};if($actual -ne $ExpectedGeneration){throw 'state-conflict'}
    $State.generation=$ExpectedGeneration+1
    $null=Write-EvidenceAtomic ([IO.Path]::Combine((Get-EvidenceTaskDirectory $Config $TaskId),'catalog.json')) $State
    return $State
}
function Get-EvidenceEventHash($Event){return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Event -Compress -Depth 12)))).ToLowerInvariant()}
function Apply-EvidenceEvent($Task,$Event){
    if($Event -is [Collections.IDictionary]){$Event=ConvertTo-Json -InputObject $Event -Compress -Depth 12|ConvertFrom-Json -DateKind String}
    Assert-Fields $Event @('schemaVersion','repositoryId','taskId','eventId','version','previousVersion','kind','occurredAt','endReason','decisionId')
    if($Event.schemaVersion -ne 1 -or $Event.repositoryId -cne $Task.repositoryId -or $Event.taskId -cne $Task.taskId -or $Event.version -lt 1 -or $Event.previousVersion -ne $Event.version-1){throw 'event-conflict'}
    Assert-Hex $Event.eventId 32;$occurred=Assert-Utc $Event.occurredAt;$hash=Get-EvidenceEventHash $Event
    $old=@($Task.events|Where-Object version -EQ $Event.version)
    if($old.Count){if($old.Count -ne 1 -or $old[0].eventId -cne $Event.eventId -or $old[0].sha256 -cne $hash){throw 'event-conflict'};return 'duplicate'}
    if($Event.version -ne $Task.appliedEventVersion+1){$Task.status='pending-gap';return 'pending-gap'}
    if($Task.status -ceq 'pending-gap'){$Task.status=if($Task.taskEndedAt){'ended'}elseif($Task.appliedEventVersion -eq 0){'unknown'}else{'active'}}
    if(($Event.kind -ceq 'started' -and $Task.appliedEventVersion -ne 0) -or ($Event.kind -ceq 'ended' -and $Task.status -cne 'active') -or ($Event.kind -ceq 'resumed' -and $Task.status -cne 'ended') -or $Event.kind -cnotin @('started','ended','resumed')){throw 'event-conflict'}
    if($Task.status -ceq 'pending-gap'){$Task.status=if($Task.taskEndedAt){'ended'}elseif($Task.appliedEventVersion -eq 0){'unknown'}else{'active'}}
    if($Event.kind -ceq 'ended'){
        if($Event.endReason -cnotin @('completed','cancelled')){throw 'event-conflict'}
        $Task.status='ended';$Task.taskEndedAt=$Event.occurredAt;$Task.endReason=$Event.endReason;$Task.deleteEligibleAt=$occurred.AddSeconds(2592000).ToString('o')
    }else{if($null -ne $Event.endReason){throw 'event-conflict'};$Task.status='active';$Task.taskEndedAt=$null;$Task.endReason=$null;$Task.deleteEligibleAt=$null}
    $Task.appliedEventVersion=$Event.version;$Task.events+= [pscustomobject]@{eventId=$Event.eventId;version=$Event.version;sha256=$hash}
    foreach($a in $Task.artifacts){Assert-EvidenceBudget;$a.appliedEventVersion=$Task.appliedEventVersion;$a.taskEndedAt=$Task.taskEndedAt;$a.endReason=$Task.endReason;$a.deleteEligibleAt=$Task.deleteEligibleAt}
    return 'applied'
}
# Compare the stored projection with the same prefix of the authoritative event
# stream before duplicate delivery can write it again. Equal versions alone do not
# prove that the original end clock or any artifact's retention clock survived.
function Assert-EvidenceProjection($Task,$Projection){
    foreach($field in @('appliedEventVersion','status','taskEndedAt','endReason','deleteEligibleAt')){if($Task.$field -cne $Projection.$field){throw 'corrupt-projection'}}
    if($Task.events.Count -ne $Projection.events.Count){throw 'corrupt-projection'}
    for($i=0;$i -lt $Task.events.Count;$i++){Assert-EvidenceBudget
        foreach($field in @('eventId','version','sha256')){if($Task.events[$i].$field -cne $Projection.events[$i].$field){throw 'corrupt-projection'}}
    }
    foreach($artifact in $Task.artifacts){Assert-EvidenceBudget
        foreach($field in @('appliedEventVersion','taskEndedAt','endReason','deleteEligibleAt')){if($artifact.$field -cne $Projection.$field){throw 'corrupt-projection'}}
    }
}
function Sync-EvidenceTaskGuarded($Config,[string]$TaskId,$Guard){
    $scopes=@();if($script:Budget){$scopes=Set-EvidenceBudgetScopes @('Enter-WorkflowTaskGuard','Write-EvidenceAtomic') $script:Budget}
    try{Assert-EvidenceBudget
    Assert-EvidenceGuard $Config $TaskId $Guard;$source=Read-WorkflowTaskState $Config.Data.repositoryId $TaskId $Guard
    if($null -eq $source){throw 'task-not-registered'}
    $task=Read-EvidenceTask $Config $TaskId $Guard;if($null -eq $task){$task=New-EvidenceTask $Config $TaskId}
    $events=@($source.events|Sort-Object version);$projection=New-EvidenceTask $Config $TaskId;$prefixVerified=$false
    if($task.appliedEventVersion -eq 0){Assert-EvidenceProjection $task $projection;$prefixVerified=$true}
    foreach($event in $events){Assert-EvidenceBudget
        if((Apply-EvidenceEvent $projection $event) -cne 'applied'){throw 'corrupt-projection'}
        if($projection.appliedEventVersion -eq $task.appliedEventVersion){Assert-EvidenceProjection $task $projection;$prefixVerified=$true}
    }
    if(-not $prefixVerified -or $projection.appliedEventVersion -ne $source.version -or $projection.status -cne $source.status -or $projection.taskEndedAt -cne $source.taskEndedAt -or $projection.endReason -cne $source.endReason){throw 'corrupt-projection'}
    foreach($event in $events){Assert-EvidenceBudget
        $r=Apply-EvidenceEvent $task $event
        $task=Write-EvidenceTask $Config $TaskId $task $task.generation $Guard
        if($r -ceq 'pending-gap'){throw 'pending-gap'}
        $null=Complete-WorkflowTaskDelivery $Config.Data.repositoryId $TaskId $event.eventId $event.version $Guard
    }
    Assert-EvidenceProjection $task $projection
    return $task
    }finally{Restore-EvidenceBudgetScopes $scopes}
}
function Invoke-EvidenceSync([string]$ConfigPath,[string]$TaskId){
    $guard=$null;$previousBudget=$script:Budget;if(-not $script:Budget){$script:Budget=New-EvidenceLocalBudget 120000};$scopes=@()
    try{$scopes=Set-EvidenceBudgetScopes @('Enter-WorkflowTaskGuard','Write-EvidenceAtomic') $script:Budget;Assert-EvidenceBudget;$config=Read-EvidenceConfig $ConfigPath;$guard=Enter-WorkflowTaskGuard $config.Data.repositoryId $TaskId;$t=Sync-EvidenceTaskGuarded $config $TaskId $guard;return [pscustomobject]@{status='passed';reasonCode='delivered';version=$t.appliedEventVersion}}
    catch{return [pscustomobject]@{status='pending';reasonCode='delivery-pending'}}finally{if($guard){$guard.Dispose()};Restore-EvidenceBudgetScopes $scopes;$script:Budget=$previousBudget}
}
Export-ModuleMember -Function Assert-EvidenceGuard,New-EvidenceTask,Read-EvidenceTask,Write-EvidenceTask,Apply-EvidenceEvent,Sync-EvidenceTaskGuarded,Invoke-EvidenceSync
