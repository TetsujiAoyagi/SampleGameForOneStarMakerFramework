param([string]$ResultPath='',[string]$Case='*')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../EvidenceRetentionPolicy.psm1')
Import-Module (Join-Path $PSScriptRoot '../EvidenceStateStore.psm1')
$registered=@('event-idempotent','event-gap','event-conflict','resume-stale-end','at-30-days','before-30-days','after-30-days','active-100-days','use-expiry','use-release','unknown-use','operation-pending','staging-7-days','staging-adopted')
$selected=if($Case -ceq '*'){$registered}else{@($Case.Split(','))};$executed=[Collections.Generic.List[string]]::new();$failed=[Collections.Generic.List[string]]::new()
foreach($caseName in $selected){$executed.Add($caseName);try{
    Assert-EvidenceTest ($caseName -cin $registered) 'unknown-case';$at=[DateTimeOffset]::Parse('2026-01-01T00:00:00.0000000+00:00');$config=@{Data=@{deploymentId='d'*32;repositoryId='a'*64}};$t=New-EvidenceTask $config 'fixture'
    $events=@(1..4|ForEach-Object{$v=$_;[pscustomobject]@{schemaVersion=1;repositoryId='a'*64;taskId='fixture';eventId=($v.ToString('x').PadLeft(32,'0'));version=$v;previousVersion=$v-1;kind=@('started','ended','resumed','ended')[$v-1];occurredAt=$at.AddHours($v-1).ToString('o');endReason=if($v -cin @(2,4)){'completed'}else{$null};decisionId='event-'+$v}})
    $null=Apply-EvidenceEvent $t $events[0];$null=Apply-EvidenceEvent $t $events[1];$deadline=[DateTimeOffset]::Parse($t.deleteEligibleAt)
    $a=[pscustomobject]@{state='ready';appliedEventVersion=2;taskEndedAt=$t.taskEndedAt;deleteEligibleAt=$t.deleteEligibleAt;deleteIntent=$null;operation=$null;protections=@()}
    switch($caseName){
        'event-idempotent'{$old=$t.taskEndedAt;$r=Apply-EvidenceEvent $t $events[1];Assert-EvidenceTest ($r -ceq 'duplicate' -and $t.taskEndedAt -ceq $old -and $t.appliedEventVersion -eq 2) 'redelivery reset'}
        'event-gap'{$r=Apply-EvidenceEvent $t $events[3];Assert-EvidenceTest ($r -ceq 'pending-gap' -and -not (Get-EvidenceRetentionDecision $t $a $deadline).eligible) 'gap allowed';$null=Apply-EvidenceEvent $t $events[2];$null=Apply-EvidenceEvent $t $events[3];Assert-EvidenceTest ($t.status -ceq 'ended' -and $t.appliedEventVersion -eq 4) 'gap recovery'}
        'event-conflict'{$events[1].decisionId='other';$rejected=$false;try{$null=Apply-EvidenceEvent $t $events[1]}catch{$rejected=$true};Assert-EvidenceTest $rejected 'event conflict accepted'}
        'resume-stale-end'{$null=Apply-EvidenceEvent $t $events[2];$null=Apply-EvidenceEvent $t $events[1];Assert-EvidenceTest ($t.status -ceq 'active' -and $null -eq $t.taskEndedAt) 'stale end';$null=Apply-EvidenceEvent $t $events[3];Assert-EvidenceTest ($t.taskEndedAt -ceq $events[3].occurredAt) 're-end date'}
        'at-30-days'{Assert-EvidenceTest (Get-EvidenceRetentionDecision $t $a $deadline).eligible 'deadline exclusive'}
        'before-30-days'{Assert-EvidenceTest (-not (Get-EvidenceRetentionDecision $t $a ($deadline.AddTicks(-1))).eligible) 'early delete'}
        'after-30-days'{Assert-EvidenceTest (Get-EvidenceRetentionDecision $t $a ($deadline.AddTicks(1))).eligible 'late retain'}
        'active-100-days'{$t.status='active';$t.taskEndedAt=$null;Assert-EvidenceTest (-not (Get-EvidenceRetentionDecision $t $a ($at.AddDays(100))).eligible) 'active age'}
        'use-expiry'{$a.protections=@([pscustomobject]@{owner='owner';consumer='test';reason='review';createdAt=$at.ToString('o');until=$deadline.AddHours(1).ToString('o');releasedAt=$null});Assert-EvidenceTest (-not (Get-EvidenceRetentionDecision $t $a $deadline).eligible) 'active use';Assert-EvidenceTest (Get-EvidenceRetentionDecision $t $a ($deadline.AddHours(1))).eligible 'use never expires'}
        'use-release'{$a.protections=@([pscustomobject]@{owner='owner';consumer='test';reason='review';createdAt=$at.ToString('o');until=$deadline.AddHours(1).ToString('o');releasedAt=$at.AddDays(10).ToString('o')});Assert-EvidenceTest (Get-EvidenceRetentionDecision $t $a $deadline).eligible 'release ignored'}
        'unknown-use'{$a.protections=@([pscustomobject]@{owner='owner';consumer='test';reason='review';createdAt=$at.ToString('o');until='bad';releasedAt=$null});Assert-EvidenceTest (-not (Get-EvidenceRetentionDecision $t $a $deadline).eligible) 'unknown use deleted'}
        'operation-pending'{$a.operation=@{process=@{pid=1}};Assert-EvidenceTest (-not (Get-EvidenceRetentionDecision $t $a $deadline).eligible) 'operation deleted'}
        'staging-7-days'{$s=[pscustomobject]@{origin='storage-copy';createdAt=$at.ToString('o');adopted=$false;adoptionPending=$false;operation=$null;deleteIntent=$null;protections=@()};Assert-EvidenceTest (-not (Get-EvidenceStagingDecision $s ($at.AddDays(7).AddTicks(-1))).eligible) 'staging early';Assert-EvidenceTest (Get-EvidenceStagingDecision $s ($at.AddDays(7))).eligible 'staging late'}
        'staging-adopted'{$s=[pscustomobject]@{origin='storage-copy';createdAt=$at.ToString('o');adopted=$true;adoptionPending=$false;operation=$null;deleteIntent=$null;protections=@()};Assert-EvidenceTest (-not (Get-EvidenceStagingDecision $s ($at.AddDays(100))).eligible) 'adopted deleted'}
    }
}catch{$failed.Add($caseName);[Console]::Error.WriteLine($caseName+': '+$_.Exception.Message)}}
Complete-EvidenceTestSuite $ResultPath $registered $selected $executed $failed
[Console]::WriteLine("EvidenceRetention $($executed.Count), failed $($failed.Count)")
