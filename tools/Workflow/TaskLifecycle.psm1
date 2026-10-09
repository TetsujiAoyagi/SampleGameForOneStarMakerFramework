Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'TaskEventStore.psm1')
$script:Clock = { [DateTimeOffset]::UtcNow }
$script:Fault = $null

function New-WorkflowState([string] $RepositoryId,[string] $TaskId) {
    return [ordered]@{schemaVersion=1;repositoryId=$RepositoryId;taskId=$TaskId;generation=-1;version=0;status='active';taskEndedAt=$null;endReason=$null;events=@();outbox=@();decisions=@();receipts=@()}
}
function Invoke-WorkflowTaskTransition([string] $RepositoryId,[string] $TaskId,[string] $Kind,[long] $ExpectedVersion,[string] $DecisionId,[string] $Reason,$Guard) {
    Assert-WorkflowGuard $RepositoryId $TaskId $Guard
    if ($Kind -notin @('started','ended','resumed') -or $DecisionId -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,127}$' -or $ExpectedVersion -lt 0) { throw 'invalid-decision' }
    if (($Kind -eq 'ended' -and $Reason -notin @('completed','cancelled')) -or ($Kind -ne 'ended' -and $Reason)) { throw 'invalid-decision' }
    $state=Read-WorkflowTaskState $RepositoryId $TaskId $Guard
    if ($null -eq $state) { $state=New-WorkflowState $RepositoryId $TaskId }
    $decision=@($state.decisions | Where-Object decisionId -CEQ $DecisionId)
    if ($decision.Count -gt 1) { throw 'decision-conflict' }
    if ($decision.Count -eq 1) {
        $decision=$decision[0]
        if ($decision.kind -cne $Kind -or $decision.endReason -cne $(if($Reason){$Reason}else{$null}) -or $decision.expectedVersion -ne $ExpectedVersion) { throw 'decision-conflict' }
        if ($null -ne $decision.eventId) {
            $event=@($state.events | Where-Object eventId -CEQ $decision.eventId)[0]
            return Complete-WorkflowFinalization $RepositoryId $TaskId $state $event $Guard
        }
    } else {
        $unfinished=@($state.decisions | Where-Object {$id=$_.decisionId;@($state.receipts | Where-Object decisionId -CEQ $id).Count -eq 0})
        if($unfinished.Count){throw 'pending-finalization'}
        if ($state.version -ne $ExpectedVersion) { throw 'workflow-version-conflict' }
        if (($Kind -eq 'started' -and $state.version -ne 0) -or ($Kind -eq 'ended' -and ($state.version -eq 0 -or $state.status -ne 'active')) -or ($Kind -eq 'resumed' -and $state.status -ne 'ended')) { throw 'invalid-task-transition' }
        # A durable instruction record is recoverable; it does not itself end the task.
        $decision=[ordered]@{decisionId=$DecisionId;kind=$Kind;endReason=$(if($Reason){$Reason}else{$null});expectedVersion=$ExpectedVersion;instructionRecordId=$DecisionId;instructionAt=$null;finalizedAt=$null;eventId=$null}
        $state.decisions=@($state.decisions)+@($decision)
        $state=Write-WorkflowTaskState $RepositoryId $TaskId $state $state.generation $Guard
        if ($script:Fault) { & $script:Fault 'after-decision' }
    }
    if ($state.version -ne $ExpectedVersion) { throw 'workflow-version-conflict' }
    $now=if ($decision.finalizedAt) { $decision.finalizedAt } else { (& $script:Clock).ToUniversalTime().ToString('o') }
    $event=[ordered]@{schemaVersion=1;repositoryId=$RepositoryId;taskId=$TaskId;eventId=[Guid]::NewGuid().ToString('N');version=$state.version+1;previousVersion=$state.version;kind=$Kind;occurredAt=$now;endReason=$(if($Reason){$Reason}else{$null});decisionId=$DecisionId}
    $decision.finalizedAt=$now;$decision.eventId=$event.eventId
    $state.events=@($state.events)+@($event);$state.outbox=@($state.outbox)+@([ordered]@{eventId=$event.eventId;version=$event.version;delivered=$false})
    $state.version=$event.version;$state.status=if($Kind -eq 'ended'){'ended'}else{'active'}
    $state.taskEndedAt=if($Kind -eq 'ended'){$now}else{$null};$state.endReason=$event.endReason
    $state=Write-WorkflowTaskState $RepositoryId $TaskId $state $state.generation $Guard
    if ($script:Fault) { & $script:Fault 'after-event' }
    return Complete-WorkflowFinalization $RepositoryId $TaskId $state $event $Guard
}
function Complete-WorkflowFinalization([string] $RepositoryId,[string] $TaskId,$State,$Event,$Guard) {
    $receipt=@($State.receipts | Where-Object decisionId -CEQ $Event.decisionId)
    if ($receipt.Count -eq 0) {
        $State.receipts=@($State.receipts)+@([ordered]@{decisionId=$Event.decisionId;eventId=$Event.eventId;version=$Event.version;finalizedAt=$Event.occurredAt})
        $null=Write-WorkflowTaskState $RepositoryId $TaskId $State $State.generation $Guard
    }
    if ($script:Fault) { & $script:Fault 'after-receipt' }
    return $Event
}
function Get-WorkflowTaskStatus([string] $RepositoryId,[string] $TaskId,$Guard) {
    $state=Read-WorkflowTaskState $RepositoryId $TaskId $Guard
    if ($null -eq $state) { return [ordered]@{status='unregistered';version=0;pendingFinalization=@();deliveryPending=0} }
    $pending=@($state.decisions | Where-Object { $id=$_.decisionId;@($state.receipts | Where-Object decisionId -CEQ $id).Count -eq 0 } | ForEach-Object { $_.decisionId })
    return [ordered]@{status=$(if($pending.Count){'pending-finalization'}elseif($state.status -eq 'active'){'active/end-not-recorded'}else{'ended'});version=$state.version;pendingFinalization=$pending;deliveryPending=@($state.outbox | Where-Object delivered -EQ $false).Count;taskEndedAt=$state.taskEndedAt;endReason=$state.endReason}
}
# Trusted external records enter through this workflow adapter only. The recorded end UTC,
# unlike a transport delivery time, is retained during recovery.
function Import-WorkflowFinalization([string] $RepositoryId,[string] $TaskId,$Record,$Guard) {
    $fields=@('decisionId','kind','endReason','expectedVersion','instructionAt','finalizedAt')
    if (@($Record.Keys).Count -ne $fields.Count) { throw 'invalid-trusted-decision' }
    foreach($field in $fields){if(-not $Record.Contains($field)){throw 'invalid-trusted-decision'}}
    $utc=[DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse($Record.finalizedAt,[ref]$utc) -or $utc.Offset -ne [TimeSpan]::Zero) { throw 'invalid-trusted-decision' }
    Assert-WorkflowGuard $RepositoryId $TaskId $Guard
    if ($Record.kind -notin @('ended','resumed') -or $Record.decisionId -cnotmatch '^[a-zA-Z0-9][a-zA-Z0-9._:/-]{0,127}$') { throw 'invalid-trusted-decision' }
    $state=Read-WorkflowTaskState $RepositoryId $TaskId $Guard
    if ($null -eq $state) { throw 'unregistered-task' }
    $prior=@($state.decisions | Where-Object decisionId -CEQ $Record.decisionId)
    if ($prior.Count) {
        if ($prior[0].instructionAt -cne $Record.instructionAt -or $prior[0].finalizedAt -cne $utc.ToString('o')) { throw 'decision-conflict' }
    } else {
        $unfinished=@($state.decisions | Where-Object {$id=$_.decisionId;@($state.receipts | Where-Object decisionId -CEQ $id).Count -eq 0})
        if($unfinished.Count){throw 'pending-finalization'}
        if ($state.version -ne $Record.expectedVersion -or ($Record.kind -eq 'ended' -and $state.status -ne 'active') -or ($Record.kind -eq 'resumed' -and $state.status -ne 'ended')) { throw 'invalid-task-transition' }
        $state.decisions=@($state.decisions)+@([ordered]@{decisionId=$Record.decisionId;kind=$Record.kind;endReason=$Record.endReason;expectedVersion=$Record.expectedVersion;instructionRecordId=$Record.decisionId;instructionAt=$Record.instructionAt;finalizedAt=$utc.ToString('o');eventId=$null})
        $null=Write-WorkflowTaskState $RepositoryId $TaskId $state $state.generation $Guard
    }
    return Invoke-WorkflowTaskTransition $RepositoryId $TaskId $Record.kind $Record.expectedVersion $Record.decisionId $Record.endReason $Guard
}
Export-ModuleMember -Function Invoke-WorkflowTaskTransition,Get-WorkflowTaskStatus,Import-WorkflowFinalization
