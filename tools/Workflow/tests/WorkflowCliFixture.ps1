param([string]$Entry,[string]$CallsPath,[ValidateSet('complete','pending')][string]$Mode='complete')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
# Substitute command boundaries in this fresh process before dot-sourcing the exact
# public entry. No production module is imported and no test hook must propagate.
function Record([string]$Name){[IO.File]::AppendAllText($CallsPath,$Name+"`n")}
function Import-Module([string]$Name){
    if([IO.Path]::GetFileName($Name) -cnotin @('TaskEventStore.psm1','TaskLifecycle.psm1','EvidenceContract.psm1','EvidenceStateStore.psm1','EvidenceCleanup.psm1')){throw 'unexpected-import'}
    Record ('import:'+ [IO.Path]::GetFileName($Name))
}
function git {Record 'git';$global:LASTEXITCODE=0;'https://github.com/offline-fixture/evidence-cli.git'}
function Get-EvidenceConfigurations([string]$RepositoryId){Record 'configs';@('fixture-one','fixture-two')}
function Enter-WorkflowTaskGuard([string]$RepositoryId,[string]$TaskId){Record 'guard';$g=[pscustomobject]@{RepositoryId=$RepositoryId;TaskId=$TaskId};$g|Add-Member ScriptMethod Dispose {Record 'dispose'};$g}
function Read-EvidenceConfig([string]$Path){Record 'config';@{Data=@{repositoryId='fixture'};Path=$Path}}
function Assert-EvidenceTransitionSafe($Config,[string]$TaskId,$Guard){Record 'preflight';@(('a'*32),('b'*32))}
function Invoke-WorkflowTaskTransition([string]$RepositoryId,[string]$TaskId,[string]$Kind,[long]$ExpectedVersion,[string]$Decision,[string]$Reason,$Guard){
    if($TaskId -cne 'fixture-resume' -or $Kind -cne 'resumed' -or $ExpectedVersion -ne 2 -or $Decision -cne 'fixture-decision' -or $Reason){throw 'transition-arguments'}
    Record 'transition';[ordered]@{schemaVersion=1;version=3;kind='resumed';taskId=$TaskId;occurredAt='2026-01-02T00:00:00+00:00'}
}
function Invoke-EvidenceSync([string]$Path,[string]$TaskId){Record 'sync';@{status=if($Mode -ceq 'pending'){'pending'}else{'passed'}}}
. $Entry resume -Task fixture-resume -ExpectedVersion 2 -Decision fixture-decision
$entryExit=$LASTEXITCODE
if(@(Get-Module -All | Where-Object Name -cin @('TaskEventStore','TaskLifecycle','EvidenceContract','EvidenceStateStore','EvidenceCleanup')).Count){throw 'production-module-loaded'}
exit $entryExit
