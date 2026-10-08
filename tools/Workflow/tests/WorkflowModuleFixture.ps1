param([Parameter(Mandatory=$true)][string]$Entry,[Parameter(Mandatory=$true)][string]$Root,[ValidateSet('contract','cli')][string]$Mode)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$WarningPreference='SilentlyContinue'
# Unlike WorkflowCliFixture's orchestration mocks, this fresh-process fixture imports
# the actual modules. Only private filesystem roots are injected; git, transition,
# registry, preflight and delivery implementations are all real, without transport.
if(-not [IO.Path]::GetFullPath($Root).StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()),[StringComparison]::OrdinalIgnoreCase)){throw 'fixture-root-outside-temp'}
$tools=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Entry))
$contractPath=[IO.Path]::Combine($tools,'Artifacts','EvidenceContract.psm1')
$deployment=[IO.Path]::Combine($Root,'deployments')
function Assert([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Write-Json([string]$Path,$Value){
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))|Out-Null
    [IO.File]::WriteAllText($Path,(ConvertTo-Json -InputObject $Value -Depth 10 -Compress),[Text.UTF8Encoding]::new($false))
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}
function Register-Config([string]$RepositoryId){
    $id='c'*32;$configPath=[IO.Path]::Combine($deployment,$id+'.config.json')
    $hash=Write-Json $configPath ([ordered]@{schemaVersion=2;profile='osm';endpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com';bucket='osm-artifacts';repositoryId=$RepositoryId;prefix='development/evidence/v2/';policy='task-end-30d-v1';deploymentId=$id})
    $null=Write-Json ([IO.Path]::Combine($deployment,$id+'.json')) ([ordered]@{schemaVersion=2;deploymentId=$id;repositoryId=$RepositoryId;configSha256=$hash;baselineRecordId='offline-baseline';baselineSha256='a'*64;ownerObservationId='offline-owner';ownerObservationSha256='b'*64})
    return $configPath
}
if($Mode -eq 'contract'){
    Assert (@(Get-Module -All | Where-Object Name -cin @('ArtifactAcl','ArtifactCommands','ArtifactPaths','EvidenceContract')).Count -eq 0) 'fixture was not fresh'
    # This is intentionally the sole explicit import before exercising registry discovery.
    $contract=Import-Module $contractPath -PassThru
    & $contract {param($r)$script:DeploymentRoot=$r} $deployment
    $repo='d'*64
    Assert (@(Get-EvidenceConfigurations $repo).Count -eq 0) 'missing registry was not empty'
    $config=Register-Config $repo
    $found=@(Get-EvidenceConfigurations $repo)
    Assert ($found.Count -eq 1 -and $found[0] -ceq $config) 'real config discovery failed'
    Assert (@(Get-EvidenceConfigurations ('e'*64)).Count -eq 0) 'foreign repository selected'
    [ordered]@{mode=$Mode;status='passed';explicitImports=@($contractPath);registryCount=$found.Count;configSha256=(Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash.ToLowerInvariant()}|ConvertTo-Json -Depth 10 -Compress
    exit 0
}
$loaded=@()
foreach($relative in @('Workflow/TaskEventStore.psm1','Workflow/TaskLifecycle.psm1','Artifacts/EvidenceContract.psm1','Artifacts/EvidenceStateStore.psm1','Artifacts/EvidenceCleanup.psm1')){
    $loaded+=@(Import-Module ([IO.Path]::Combine($tools,$relative)) -PassThru)
}
$workflow=[IO.Path]::Combine($Root,'workflow');$evidence=[IO.Path]::Combine($Root,'evidence-v2')
$modules=@(Get-Module -All)
foreach($module in $modules){
    switch($module.Name){
        'TaskEventStore'{& $module {param($r)$script:TestRoot=$r} $workflow}
        'EvidenceContract'{& $module {param($r)$script:DeploymentRoot=$r} $deployment}
        'EvidencePaths'{& $module {param($r)$script:TestRoot=$r} $evidence}
    }
}
foreach($store in @($modules|Where-Object Name -CEQ 'TaskEventStore')){Assert ((& $store {Get-WorkflowRoot}) -ceq $workflow) 'real workflow root not isolated'}
foreach($paths in @($modules|Where-Object Name -CEQ 'EvidencePaths')){Assert ((& $paths {Get-EvidenceRoot}) -ceq $evidence) 'real evidence root not isolated'}
$task='real-module-boundary'
$firstText= & $Entry start -Task $task
$firstExit=$LASTEXITCODE;$first=($firstText -join [Environment]::NewLine)|ConvertFrom-Json -DateKind String
Assert ($firstExit -eq 2 -and $first.status -ceq 'recorded/delivery-pending' -and $first.result.kind -ceq 'started' -and $first.result.version -eq 1) ('real start did not durably record pending delivery: exit='+$firstExit+' result='+($firstText -join ' '))
$statusText= & $Entry status -Task $task
$statusExit=$LASTEXITCODE;$status=($statusText -join [Environment]::NewLine)|ConvertFrom-Json -DateKind String
Assert ($statusExit -eq 0 -and $status.result.status -ceq 'active/end-not-recorded' -and $status.result.deliveryPending -eq 1 -and $status.result.version -eq 1) 'real status failed'
$config=Register-Config $first.repositoryId
$repeatText= & $Entry start -Task $task
$repeatExit=$LASTEXITCODE;$repeat=($repeatText -join [Environment]::NewLine)|ConvertFrom-Json -DateKind String
Assert ($repeatExit -eq 0 -and $repeat.status -ceq 'ok' -and $repeat.result.eventId -ceq $first.result.eventId -and $repeat.result.occurredAt -ceq $first.result.occurredAt -and $repeat.result.version -eq 1) 'real start retry changed event or failed actual delivery'
$ackText= & $Entry status -Task $task
$ackExit=$LASTEXITCODE;$ack=($ackText -join [Environment]::NewLine)|ConvertFrom-Json -DateKind String
Assert ($ackExit -eq 0 -and $ack.result.deliveryPending -eq 0 -and $ack.result.version -eq 1 -and $ack.result.status -ceq 'active/end-not-recorded') 'actual acknowledgement missing'
$endText= & $Entry end -Task $task -Reason completed -ExpectedVersion 1 -Decision fixture-trusted-end
$endExit=$LASTEXITCODE;$ended=($endText -join [Environment]::NewLine)|ConvertFrom-Json -DateKind String
Assert ($endExit -eq 0 -and $ended.status -ceq 'ok' -and $ended.result.kind -ceq 'ended' -and $ended.result.endReason -ceq 'completed' -and $ended.result.version -eq 2) 'real end failed'
$resumeText= & $Entry resume -Task $task -ExpectedVersion 2 -Decision fixture-trusted-resume
$resumeExit=$LASTEXITCODE;$resumed=($resumeText -join [Environment]::NewLine)|ConvertFrom-Json -DateKind String
Assert ($resumeExit -eq 0 -and $resumed.status -ceq 'ok' -and $resumed.result.kind -ceq 'resumed' -and $null -eq $resumed.result.endReason -and $resumed.result.version -eq 3) 'real resume failed'
$finalText= & $Entry status -Task $task
$finalExit=$LASTEXITCODE;$final=($finalText -join [Environment]::NewLine)|ConvertFrom-Json -DateKind String
Assert ($finalExit -eq 0 -and $final.result.deliveryPending -eq 0 -and $final.result.version -eq 3 -and $null -eq $final.result.taskEndedAt -and $final.result.status -ceq 'active/end-not-recorded') 'actual resumed state missing'
Assert ([IO.Directory]::Exists([IO.Path]::Combine($workflow,$first.repositoryId,'tasks',$task)) -and [IO.Directory]::Exists($evidence)) 'actual store outputs missing'
[ordered]@{mode=$Mode;status='passed';explicitImports=@($loaded.Path);startExit=$firstExit;statusExit=$statusExit;retryExit=$repeatExit;endExit=$endExit;resumeExit=$resumeExit;finalStatusExit=$finalExit;eventId=$first.result.eventId;occurredAt=$first.result.occurredAt;version=$final.result.version;deliveryPending=$final.result.deliveryPending;repositoryId=$first.repositoryId;configSha256=(Get-FileHash -LiteralPath $config -Algorithm SHA256).Hash.ToLowerInvariant()}|ConvertTo-Json -Depth 10 -Compress
exit 0
