param(
    [Parameter(Position=0,Mandatory=$true)][ValidateSet('start','end','resume','status')][string] $Command,
    [Parameter(Mandatory=$true)][string] $Task,
    [ValidateSet('completed','cancelled')][string] $Reason,
    [long] $ExpectedVersion=-1,
    [string] $Decision
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
Import-Module (Join-Path $PSScriptRoot 'Workflow/TaskEventStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'Workflow/TaskLifecycle.psm1')
Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceStateStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'Artifacts/EvidenceCleanup.psm1')

try {
    # One canonical Git origin keeps task identity stable across worktrees. The old
    # Evidence reset uses its frozen supplied identity and never rewrites old keys.
    $remote=(& git -C ([IO.Path]::Combine($PSScriptRoot,'..')) remote get-url origin 2>$null)
    if ($LASTEXITCODE -ne 0) { throw 'repository-unavailable' }
    if ($remote -match '^git@github\.com:([^/]+)/([^/]+?)(?:\.git)?/?$' -or $remote -match '^https://(?:[^@/]+@)?github\.com/([^/]+)/([^/]+?)(?:\.git)?/?$') {
        $canonical="github.com/$($Matches[1])/$($Matches[2])".ToLowerInvariant()
    } else { throw 'repository-unavailable' }
    $repo=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))).ToLowerInvariant()
    $configs=@(Get-EvidenceConfigurations $repo)
    $deletedArtifacts=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $guard=Enter-WorkflowTaskGuard $repo $Task
    try {
        if ($Command -eq 'status') { $result=Get-WorkflowTaskStatus $repo $Task $guard }
        else {
            if ($Command -eq 'start') {
                if ($ExpectedVersion -ne -1 -or $Decision -or $Reason) { throw 'unsupported-command' }
                $state=Read-WorkflowTaskState $repo $Task $guard
                if ($state -and $state.version -gt 0) {
                    if ($state.status -ne 'active') { throw 'resume-required' }
                    $kind='started';$ExpectedVersion=0;$Decision='task-start'
                } else { $kind='started';$ExpectedVersion=0;$Decision='task-start' }
            } else {
                if ($ExpectedVersion -lt 1 -or -not $Decision -or ($Command -eq 'resume' -and $Reason) -or ($Command -eq 'end' -and -not $Reason)) { throw 'unsupported-command' }
                $kind=if($Command -eq 'end'){'ended'}else{'resumed'}
            }
            if (-not (Get-Variable result -ErrorAction SilentlyContinue)) {
                # Storage resolves any ambiguous deletion before Workflow commits. Core
                # transition and store modules have no Storage dependency.
                foreach($path in $configs) {
                    $config=Read-EvidenceConfig $path 'osm'
                    $deleted=@(Assert-EvidenceTransitionSafe $config $Task $guard)
                    if($Command -eq 'resume'){foreach($artifactId in $deleted){$null=$deletedArtifacts.Add($artifactId)}}
                }
                $result=Invoke-WorkflowTaskTransition $repo $Task $kind $ExpectedVersion $Decision $Reason $guard
            }
        }
    } finally { $guard.Dispose() }
    $pending=$false
    if ($Command -ne 'status') {
        foreach($path in $configs) { try { $sync=Invoke-EvidenceSync $path $Task;if($sync.status -ne 'passed'){$pending=$true} }catch{$pending=$true} }
        if ($configs.Count -eq 0) { $pending=$true }
    }
    # Resume can commit after an earlier cleanup without restoring expired payloads.
    # Keep the tombstone outcome visible even when event delivery is still pending.
    [ordered]@{status=$(if($pending){'recorded/delivery-pending'}elseif($deletedArtifacts.Count){'payload-deleted'}else{'ok'});reasonCode=$(if($deletedArtifacts.Count){'payload-deleted'}else{$null});deletedArtifacts=@($deletedArtifacts | Sort-Object);taskId=$Task;repositoryId=$repo;result=$result} | ConvertTo-Json -Depth 20
    if($pending){exit 2}
} catch {
    [ordered]@{status='failed';reasonCode=$(if($_.Exception.Message -match '^[a-z][a-z0-9/-]+$'){$_.Exception.Message}else{'workflow-unavailable'})} | ConvertTo-Json -Compress
    exit 1
}
