Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'WindowsStorePaths.psm1')

# A caller may supply a monotonic remaining-time callback for this invocation.
# Ordinary callers leave it null and retain the existing thirty-second guard limit.
$script:Budget=$null
function Assert-WorkflowBudget { if($script:Budget -and (& $script:Budget.Remaining) -le 0){throw 'workflow-deadline'} }
$script:TestRoot = $null
$script:Fault = $null
$script:GuardWaitHook=$null

function Assert-WorkflowIdentity([string] $RepositoryId, [string] $TaskId) {
    if ($RepositoryId -cnotmatch '^[a-f0-9]{64}$' -or $TaskId -cnotmatch '^[a-z0-9][a-z0-9-]{0,63}$') { throw 'invalid-task-identity' }
}
function Assert-WorkflowPath([string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $part = [IO.Path]::GetPathRoot($full)
    Assert-WorkflowBudget
    foreach ($segment in $full.Substring($part.Length).Split([IO.Path]::DirectorySeparatorChar,[StringSplitOptions]::RemoveEmptyEntries)) {
        Assert-WorkflowBudget;$part = [IO.Path]::Combine($part,$segment)
        if (([IO.File]::Exists($part) -or [IO.Directory]::Exists($part)) -and
            ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint)) { throw 'workflow-path-unavailable' }
    }
}
function Set-WorkflowPrivateAcl([string] $Path,[bool] $Directory) {
    $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = if ($Directory) { [Security.AccessControl.DirectorySecurity]::new() } else { [Security.AccessControl.FileSecurity]::new() }
    $acl.SetOwner($owner); $acl.SetAccessRuleProtection($true,$false)
    foreach ($sid in @($owner,[Security.Principal.SecurityIdentifier]::new('S-1-5-18'),[Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
        $inherit = if ($Directory) { [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit } else { [Security.AccessControl.InheritanceFlags]::None }
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,[Security.AccessControl.FileSystemRights]::FullControl,$inherit,[Security.AccessControl.PropagationFlags]::None,[Security.AccessControl.AccessControlType]::Allow))
    }
    if ($Directory) { [IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($Path),$acl) }
    else { [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($Path),$acl) }
}
function Assert-WorkflowPrivateAcl([string] $Path,[bool] $Directory) {
    Assert-WorkflowPath $Path
    $acl = if ($Directory) { [IO.FileSystemAclExtensions]::GetAccessControl([IO.DirectoryInfo]::new($Path)) } else { [IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($Path)) }
    $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    if (-not $acl.AreAccessRulesProtected -or $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -cne $owner) { throw 'workflow-path-unavailable' }
    $seen = @{}
    foreach ($rule in $acl.GetAccessRules($true,$true,[Security.Principal.SecurityIdentifier])) {
        if ($rule.IsInherited -or $rule.IdentityReference.Value -notin @($owner,'S-1-5-18','S-1-5-32-544') -or $rule.AccessControlType -ne 'Allow' -or (($rule.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) -ne [Security.AccessControl.FileSystemRights]::FullControl)) { throw 'workflow-path-unavailable' }
        $seen[$rule.IdentityReference.Value]=$true
    }
    if ($seen.Count -ne 3) { throw 'workflow-path-unavailable' }
}
function Get-WorkflowRoot {
    $context=[AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1')
    if($context){
        $binding=ConvertFrom-Json -InputObject $context -AsHashtable -DateKind String
        $root=Assert-WindowsStorePathIdentity $binding.roles.workflow.physicalPath $binding.roles.workflow
        Assert-WorkflowPrivateAcl $root $true
        return $root
    }
    if ($script:TestRoot) { return [IO.Path]::GetFullPath($script:TestRoot) }
    $root=[IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'),'OneStarMaker','Workflow')
    if([IO.Directory]::Exists($root)){$root=(Get-WindowsStorePathIdentity $root).physicalPath;Assert-WorkflowPrivateAcl $root $true}
    return $root
}
function Get-WorkflowTaskPath([string] $RepositoryId,[string] $TaskId) {
    Assert-WorkflowIdentity $RepositoryId $TaskId
    return [IO.Path]::Combine((Get-WorkflowRoot),$RepositoryId,'tasks',$TaskId)
}
function Initialize-WorkflowDirectory([string] $Path) {
    Assert-WorkflowBudget
    Assert-WorkflowPath $Path
    if (-not [IO.Directory]::Exists($Path)) {
        if([AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1')){throw 'workflow-path-unavailable'}
        [IO.Directory]::CreateDirectory($Path)|Out-Null; Set-WorkflowPrivateAcl $Path $true
    }
    Assert-WorkflowPrivateAcl $Path $true
}
function Enter-WorkflowTaskGuard([string] $RepositoryId,[string] $TaskId) {
    $path = Get-WorkflowTaskPath $RepositoryId $TaskId
    Initialize-WorkflowDirectory (Get-WorkflowRoot)
    Initialize-WorkflowDirectory ([IO.Path]::GetDirectoryName([IO.Path]::GetDirectoryName($path)))
    Initialize-WorkflowDirectory ([IO.Path]::GetDirectoryName($path))
    Initialize-WorkflowDirectory $path
    # Initial creation can turn a KnownFolder alias into its native root. The
    # guard and every subsequent state accessor must retain that final path.
    $path = Get-WorkflowTaskPath $RepositoryId $TaskId
    $lock = [IO.Path]::Combine($path,'task.lock')
    Assert-WorkflowPath $lock
    $bound=[bool][AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1')
    if($bound){Assert-WorkflowPrivateAcl $lock $false}
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $wait=[Threading.ManualResetEvent]::new($false)
    try {
        do {
            Assert-WorkflowBudget
            try {
                $mode=if($bound){[IO.FileMode]::Open}else{[IO.FileMode]::OpenOrCreate}
                $stream=[IO.FileStream]::new($lock,$mode,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
                if(-not $bound){Set-WorkflowPrivateAcl $lock $false}
                Assert-WorkflowPrivateAcl $lock $false
                $guard=[pscustomobject]@{RepositoryId=$RepositoryId;TaskId=$TaskId;Handle=$stream;Path=$path}
                $guard | Add-Member ScriptMethod Dispose { $this.Handle.Dispose() }
                return $guard
            } catch [IO.IOException] {
                if ($watch.Elapsed.TotalSeconds -ge 30) { throw 'workflow-busy' }
                $allowance=if($script:Budget){[int][Math]::Min(25,[long](& $script:Budget.Remaining))}else{25};if($allowance -lt 1){throw 'workflow-deadline'}
                if($script:GuardWaitHook){& $script:GuardWaitHook $allowance}else{$null=$wait.WaitOne($allowance)}
            }
        } while ($true)
    } finally { $wait.Dispose() }
}
function Assert-WorkflowGuard([string] $RepositoryId,[string] $TaskId,$Guard) {
    Assert-WorkflowBudget
    Assert-WorkflowIdentity $RepositoryId $TaskId
    if ($null -eq $Guard -or $Guard.RepositoryId -cne $RepositoryId -or $Guard.TaskId -cne $TaskId -or $Guard.Handle.SafeFileHandle.IsClosed -or -not $Guard.Handle.CanWrite -or $Guard.Handle.Name -cne [IO.Path]::Combine($Guard.Path,'task.lock') -or $Guard.Path -cne (Get-WorkflowTaskPath $RepositoryId $TaskId)) { throw 'workflow-guard-required' }
}
function Assert-WorkflowFields($Value,[string[]] $Fields) {
    if ($Value -isnot [Collections.IDictionary]) { throw 'workflow-state-corrupt' }
    $actual=@($Value.Keys)
    if ($actual.Count -ne $Fields.Count) { throw 'workflow-state-corrupt' }
    foreach ($field in $Fields) { if (-not $Value.Contains($field)) { throw 'workflow-state-corrupt' } }
}
function Assert-WorkflowState($State,[string] $RepositoryId,[string] $TaskId) {
    Assert-WorkflowFields $State @('schemaVersion','repositoryId','taskId','generation','version','status','taskEndedAt','endReason','events','outbox','decisions','receipts')
    if ($State.schemaVersion -ne 1 -or $State.repositoryId -cne $RepositoryId -or $State.taskId -cne $TaskId -or $State.generation -isnot [long] -and $State.generation -isnot [int] -or $State.generation -lt 0 -or $State.version -ne @($State.events).Count -or $State.status -notin @('active','ended')) { throw 'workflow-state-corrupt' }
    $ids=@{}; $version=0; $endedAt=$null; $reason=$null; $status='active'
    foreach ($event in $State.events) {Assert-WorkflowBudget;
        Assert-WorkflowFields $event @('schemaVersion','repositoryId','taskId','eventId','version','previousVersion','kind','occurredAt','endReason','decisionId')
        $version++
        if ($event.schemaVersion -ne 1 -or $event.repositoryId -cne $RepositoryId -or $event.taskId -cne $TaskId -or $event.eventId -cnotmatch '^[a-f0-9]{32}$' -or $ids.ContainsKey($event.eventId) -or $event.version -ne $version -or $event.previousVersion -ne $version-1 -or $event.kind -notin @('started','ended','resumed') -or $event.decisionId -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,127}$') { throw 'workflow-state-corrupt' }
        $ids[$event.eventId]=$true
        $utc=[DateTimeOffset]::MinValue
        if (-not [DateTimeOffset]::TryParse($event.occurredAt,[ref] $utc) -or $utc.Offset -ne [TimeSpan]::Zero) { throw 'workflow-state-corrupt' }
        if ($version -eq 1) { if ($event.kind -cne 'started') { throw 'workflow-state-corrupt' } }
        elseif ($event.kind -eq 'started' -or ($event.kind -eq 'ended' -and $status -eq 'ended') -or ($event.kind -eq 'resumed' -and $status -ne 'ended')) { throw 'workflow-state-corrupt' }
        if ($event.kind -eq 'ended') {
            if ($event.endReason -notin @('completed','cancelled')) { throw 'workflow-state-corrupt' }
            $status='ended';$endedAt=$event.occurredAt;$reason=$event.endReason
        } else {
            if ($null -ne $event.endReason) { throw 'workflow-state-corrupt' }
            $status='active';$endedAt=$null;$reason=$null
        }
    }
    if ($State.status -cne $status -or $State.taskEndedAt -cne $endedAt -or $State.endReason -cne $reason -or @($State.outbox).Count -ne $version) { throw 'workflow-state-corrupt' }
    for ($index=0;$index -lt $version;$index++) {Assert-WorkflowBudget;
        $item=$State.outbox[$index];Assert-WorkflowFields $item @('eventId','version','delivered')
        if ($item.eventId -cne $State.events[$index].eventId -or $item.version -ne $index+1 -or $item.delivered -isnot [bool]) { throw 'workflow-state-corrupt' }
    }
    $decisionIds=@{}
    foreach ($decision in $State.decisions) {Assert-WorkflowBudget;
        Assert-WorkflowFields $decision @('decisionId','kind','endReason','expectedVersion','instructionRecordId','instructionAt','finalizedAt','eventId')
        if ($decision.decisionId -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,127}$' -or $decisionIds.ContainsKey($decision.decisionId) -or $decision.kind -notin @('started','ended','resumed') -or $decision.expectedVersion -lt 0 -or $decision.instructionRecordId -cne $decision.decisionId) { throw 'workflow-state-corrupt' }
        $decisionIds[$decision.decisionId]=$true
        if ($null -ne $decision.eventId) {
            $match=@($State.events | Where-Object decisionId -CEQ $decision.decisionId)
            if ($match.Count -ne 1 -or $match[0].eventId -cne $decision.eventId -or $match[0].occurredAt -cne $decision.finalizedAt -or $match[0].kind -cne $decision.kind -or $match[0].endReason -cne $decision.endReason) { throw 'workflow-state-corrupt' }
        }
    }
    foreach ($event in $State.events) {Assert-WorkflowBudget; if (-not $decisionIds.ContainsKey($event.decisionId)) { throw 'workflow-state-corrupt' } }
    $receiptIds=@{}
    foreach ($receipt in $State.receipts) {Assert-WorkflowBudget;
        Assert-WorkflowFields $receipt @('decisionId','eventId','version','finalizedAt')
        $match=@($State.events | Where-Object decisionId -CEQ $receipt.decisionId)
        if ($receiptIds.ContainsKey($receipt.decisionId) -or $match.Count -ne 1 -or $match[0].eventId -cne $receipt.eventId -or $match[0].version -ne $receipt.version -or $match[0].occurredAt -cne $receipt.finalizedAt) { throw 'workflow-state-corrupt' }
        $receiptIds[$receipt.decisionId]=$true
    }
}
function Assert-WorkflowJsonProperties($Element) {
    if($Element.ValueKind -eq [Text.Json.JsonValueKind]::Object){
        $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($property in $Element.EnumerateObject()){Assert-WorkflowBudget;if(-not $seen.Add($property.Name)){throw 'workflow-state-corrupt'};Assert-WorkflowJsonProperties $property.Value}
    }elseif($Element.ValueKind -eq [Text.Json.JsonValueKind]::Array){foreach($item in $Element.EnumerateArray()){Assert-WorkflowBudget;Assert-WorkflowJsonProperties $item}}
}
function Read-WorkflowTaskState([string] $RepositoryId,[string] $TaskId,$Guard) {
    Assert-WorkflowGuard $RepositoryId $TaskId $Guard
    $path=[IO.Path]::Combine($Guard.Path,'state.json')
    Assert-WorkflowPath $path
    if (-not [IO.File]::Exists($path)) { return $null }
    Assert-WorkflowPrivateAcl $path $false
    if ([IO.FileInfo]::new($path).Length -gt 16MB) { throw 'workflow-state-corrupt' }
    Assert-WorkflowBudget;$raw=[IO.File]::ReadAllText($path);Assert-WorkflowBudget
    $document=$null
    try{$document=[Text.Json.JsonDocument]::Parse($raw);Assert-WorkflowJsonProperties $document.RootElement}catch{throw 'workflow-state-corrupt'}finally{if($document){$document.Dispose()}}
    try { $state=ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($path)) -AsHashtable -DateKind String -Depth 30 } catch { throw 'workflow-state-corrupt' }
    Assert-WorkflowBudget;Assert-WorkflowState $state $RepositoryId $TaskId
    return $state
}
function Write-WorkflowTaskState([string] $RepositoryId,[string] $TaskId,$State,[long] $ExpectedGeneration,$Guard) {
    Assert-WorkflowGuard $RepositoryId $TaskId $Guard
    $current=Read-WorkflowTaskState $RepositoryId $TaskId $Guard
    $generation=if ($null -eq $current) { -1 } else { $current.generation }
    if ($generation -ne $ExpectedGeneration) { throw 'workflow-generation-conflict' }
    $State.generation=$ExpectedGeneration+1
    Assert-WorkflowState $State $RepositoryId $TaskId
    # Existing immutable event bytes and decisions cannot be silently rewritten by CAS.
    if ($null -ne $current) {
        if (@($State.events).Count -lt @($current.events).Count) { throw 'workflow-event-conflict' }
        for ($i=0;$i -lt @($current.events).Count;$i++) {Assert-WorkflowBudget;
            if (($State.events[$i] | ConvertTo-Json -Compress -Depth 20) -cne ($current.events[$i] | ConvertTo-Json -Compress -Depth 20)) { throw 'workflow-event-conflict' }
        }        foreach($prior in $current.decisions){Assert-WorkflowBudget;
            $next=@($State.decisions | Where-Object decisionId -CEQ $prior.decisionId)
            if($next.Count -ne 1){throw 'workflow-decision-conflict'}
            foreach($field in @('kind','endReason','expectedVersion','instructionRecordId','instructionAt')){
                if($next[0][$field] -cne $prior[$field]){throw 'workflow-decision-conflict'}
            }
            foreach($field in @('finalizedAt','eventId')){if($null -ne $prior[$field] -and $next[0][$field] -cne $prior[$field]){throw 'workflow-decision-conflict'}}
        }
        foreach($prior in $current.receipts){Assert-WorkflowBudget;
            $next=@($State.receipts | Where-Object decisionId -CEQ $prior.decisionId)
            if($next.Count -ne 1 -or ($next[0] | ConvertTo-Json -Compress) -cne ($prior | ConvertTo-Json -Compress)){throw 'workflow-receipt-conflict'}
        }
    }
    $path=[IO.Path]::Combine($Guard.Path,'state.json');$temp=[IO.Path]::Combine($Guard.Path,[Guid]::NewGuid().ToString('N')+'.tmp')
    Assert-WorkflowBudget
    $bytes=[Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $State -Depth 30 -Compress))
    if ($bytes.Length -gt 16MB) { throw 'workflow-state-full' }
    $stream=$null
    try {
        $stream=[IO.FileStream]::new($temp,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        Set-WorkflowPrivateAcl $temp $false
        Assert-WorkflowBudget;$stream.Write($bytes,0,$bytes.Length);$stream.Flush($true);Assert-WorkflowBudget;$stream.Dispose();$stream=$null
        if ($script:Fault) { & $script:Fault 'before-state-commit' }
        Assert-WorkflowPath $path
        Assert-WorkflowBudget;[IO.File]::Move($temp,$path,$true);Assert-WorkflowBudget
        Assert-WorkflowPrivateAcl $path $false
    } finally { if ($stream) { $stream.Dispose() };if ([IO.File]::Exists($temp)) { [IO.File]::Delete($temp) } }
    return $State
}
function Complete-WorkflowTaskDelivery([string] $RepositoryId,[string] $TaskId,[string] $EventId,[long] $Version,$Guard) {
    $state=Read-WorkflowTaskState $RepositoryId $TaskId $Guard
    $items=@($state.outbox | Where-Object { $_.eventId -ceq $EventId -and $_.version -eq $Version })
    if ($items.Count -ne 1) { throw 'workflow-ack-conflict' }
    if (-not $items[0].delivered) { $items[0].delivered=$true;$null=Write-WorkflowTaskState $RepositoryId $TaskId $state $state.generation $Guard }
}
function Get-WorkflowTasks([string] $RepositoryId) {
    Assert-WorkflowIdentity $RepositoryId 'identity-check'
    $path=[IO.Path]::Combine((Get-WorkflowRoot),$RepositoryId,'tasks')
    Assert-WorkflowPath $path
    if (-not [IO.Directory]::Exists($path)) { return @() }
    Assert-WorkflowPrivateAcl $path $true
    return @([IO.Directory]::EnumerateDirectories($path) | ForEach-Object {
        Assert-WorkflowBudget;$id=[IO.Path]::GetFileName($_);Assert-WorkflowIdentity $RepositoryId $id;Assert-WorkflowPath $_;$id
    } | Sort-Object)
}
Export-ModuleMember -Function Enter-WorkflowTaskGuard,Assert-WorkflowGuard,Read-WorkflowTaskState,Write-WorkflowTaskState,Complete-WorkflowTaskDelivery,Get-WorkflowTasks
