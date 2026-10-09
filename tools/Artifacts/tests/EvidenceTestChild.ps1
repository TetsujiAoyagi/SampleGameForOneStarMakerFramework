param([string]$Root,[string]$ConfigPath,[string]$Reference,[string]$Sha256,[string]$Action='fetch',[string]$Signal='', [string]$Now='')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'EvidenceTestSupport.ps1')
Import-Module (Join-Path $PSScriptRoot '../EvidenceApplication.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceContract.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceStateStore.psm1');Import-Module (Join-Path $PSScriptRoot '../EvidenceCleanup.psm1');Import-Module (Join-Path $PSScriptRoot '../../Workflow/TaskLifecycle.psm1');Import-Module (Join-Path $PSScriptRoot '../../Workflow/TaskEventStore.psm1')
Set-EvidenceTestValue TaskEventStore TestRoot ([IO.Path]::Combine($Root,'workflow'));Set-EvidenceTestValue EvidencePaths TestRoot ([IO.Path]::Combine($Root,'catalog'));Set-EvidenceTestValue ArtifactPaths TestRoot ([IO.Path]::Combine($Root,'transfers'));Set-EvidenceTestValue EvidenceContract DeploymentRoot ([IO.Path]::Combine($Root,'deployments'))
foreach($m in @(Get-EvidenceTestModules EvidenceApplication)+@(Get-EvidenceTestModules EvidenceCleanup)){& $m {function script:Get-CredentialStatus {param($p)[pscustomobject]@{Generation='f'*32}}}}
if($Now){$at=[DateTimeOffset]::Parse($Now);Set-EvidenceTestValue EvidenceCleanup Clock {$at}.GetNewClosure();Set-EvidenceTestValue EvidenceApplication Clock {$at}.GetNewClosure()}
$remote=[IO.Path]::Combine($Root,'remote.zip')
$hook={param($gen,$req)if($req.Operation -cne 'get'){throw 'unexpected-child-network'};$data=[IO.File]::ReadAllBytes($remote);[IO.File]::WriteAllBytes($req.DestinationPath,$data);[pscustomobject]@{Operation='get';Generation=$gen;HttpStatus=200;StatusClass='success';S3Code=$null;Bytes=[long]$data.Length;Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant();StatusObserved=$true;TimedOut=$false;Redirected=$false}}.GetNewClosure();Set-EvidenceTestValue ArtifactApplication TransportHook $hook
if($Signal){
    # Prove the other process owns the actual shared file guard before releasing the
    # test barrier. The subsequent real operation must wait for that same guard.
    $lock=[IO.Path]::Combine($Root,'workflow',('a'*64),'tasks','fixture','task.lock');$busy=$false
    try{$f=[IO.FileStream]::new($lock,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None);$f.Dispose()}catch [IO.IOException]{$busy=$true}
    if(-not $busy){throw 'race-guard-not-held'}
    $event=[Threading.EventWaitHandle]::OpenExisting($Signal);try{$null=$event.Set()}finally{$event.Dispose()}
}
switch($Action){
    'fetch'{& (Join-Path $PSScriptRoot '../../artifacts.ps1') fetch --profile osm --config $ConfigPath --reference $Reference --sha256 $Sha256;exit $LASTEXITCODE}
    'use'{& (Join-Path $PSScriptRoot '../../artifacts.ps1') evidence use --profile osm --config $ConfigPath --reference $Reference --consumer child --until ([DateTimeOffset]::UtcNow.AddHours(1).ToString('o')) --reason review;exit $LASTEXITCODE}
    'cleanup'{$r=Invoke-EvidenceCleanup $ConfigPath;$r|ConvertTo-Json -Compress -Depth 10;if($r.status -cne 'passed'){exit 1}}
    'resume'{$c=Read-EvidenceConfig $ConfigPath;$g=Enter-WorkflowTaskGuard $c.Data.repositoryId fixture;try{$deleted=@(Assert-EvidenceTransitionSafe $c fixture $g);$e=Invoke-WorkflowTaskTransition $c.Data.repositoryId fixture resumed 2 child-resume '' $g;[pscustomobject]@{version=$e.version;deleted=$deleted}|ConvertTo-Json -Compress}finally{$g.Dispose()}}
}
