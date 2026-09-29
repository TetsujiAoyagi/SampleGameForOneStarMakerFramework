param([string]$ResultPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../RecordStore.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../GatePolicy.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../Adapters/LocalChecks.psm1') -Force
$registered = @('repository identity and explicit task', 'CLI status and missing task', 'forged A3 snapshot rejected', 'immutable record rejects corruption', 'run interruption remains identifiable', 'handoff does not select on failed save', 'current revision and restore', 'missing current restores previous', 'actual Git diff detects Unity', 'history catches removed evidence', 'gate requires exact run', 'case names and duplicates', 'broad discovery run is reusable', 'reference ownership and expiry', 'restore preserves active references', 'close checks revision under lock', 'blind input omits findings', 'child process result', 'close persists current references')
$selected = [Collections.Generic.List[string]]::new()
$executed = [Collections.Generic.List[string]]::new()
$failed = [Collections.Generic.List[string]]::new()
function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Reject([scriptblock]$Action, [string]$Message) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert $rejected $Message
}
function Run([string]$Name, [scriptblock]$Body) {
    $selected.Add($Name)
    try { & $Body; $executed.Add($Name) } catch { $executed.Add($Name); $failed.Add("$Name`: $($_.Exception.Message)") }
}
$root = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'osm-harness-test-' + [Guid]::NewGuid().ToString('N'))
$repo = [IO.Path]::Combine($root, 'repo')
$store = [IO.Path]::Combine($root, 'store')
[IO.Directory]::CreateDirectory($repo) | Out-Null
try {
    & git -C $repo init -b develop -q
    & git -C $repo config user.name HarnessTest
    & git -C $repo config user.email test@example.invalid
    & git -C $repo remote add origin 'git@github.com:TetsujiAoyagi/SampleGameForOneStarMakerFramework.git'
    [IO.File]::WriteAllText([IO.Path]::Combine($repo, 'seed.txt'), 'seed')
    & git -C $repo add seed.txt
    & git -C $repo commit -qm seed
    $base = (& git -C $repo rev-parse HEAD).Trim()
    $taskDirectory = Get-TaskDirectory $repo 'h1-transport-identity' $store
    [IO.Directory]::CreateDirectory($taskDirectory) | Out-Null
    $identity = Get-RepositoryIdentity $repo
    $spec = [ordered]@{ approved = $true; base = $base; testPolicy = 'local-gates-v1'; recordPolicy = 'external-current-v1' }
    $specRecord = Write-NewRecord $taskDirectory 'specifications' $spec
    $current = [pscustomobject]@{ schemaVersion = 1; repoId = $identity.Id; taskId = 'h1-transport-identity'; revision = 1; owner = 'test'; phase = 'B'; updatedAt = '2026-09-29T00:00:00Z'; specId = $specRecord.Id; adoptedRuns = @(); references = @(); selectedInputId = $null; judgmentInputId = $null; gateReceiptId = $null; nextAction = 'test' }
    [IO.File]::WriteAllText([IO.Path]::Combine($taskDirectory, 'registration.json'), (ConvertTo-Json ([ordered]@{ taskId = 'h1-transport-identity'; repoId = $identity.Id })))
    [IO.File]::WriteAllText([IO.Path]::Combine($taskDirectory, 'CURRENT.json'), (ConvertTo-Json $current -Depth 20))

    Run 'repository identity and explicit task' {
        Assert ($identity.Canonical -ceq 'github.com/tetsujiaoyagi/samplegameforonestarmakerframework') 'SSH normalization'
        & git -C $repo remote set-url origin 'https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework.git'
        Assert ((Get-RepositoryIdentity $repo).Id -ceq $identity.Id) 'HTTPS normalization'
        Reject { Get-TaskDirectory $repo '../escape' $store } 'invalid task id accepted'
    }
    Run 'CLI status and missing task' {
        $cli = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '../../harness.ps1'))
        $listed = Invoke-Process 'pwsh' @('-NoProfile', '-File', $cli, 'status', '-Repo', $repo, '-StoreRoot', $store) $repo 15
        Assert ($listed.ExitCode -eq 0 -and $listed.Stdout.Contains('h1-transport-identity owner=test phase=B')) 'CLI status failed'
        $missing = Invoke-Process 'pwsh' @('-NoProfile', '-File', $cli, 'current', '-Repo', $repo, '-StoreRoot', $store, '-Task', 'missing') $repo 15
        Assert ($missing.ExitCode -ne 0 -and ($missing.Stdout + $missing.Stderr).Contains('status') -and ($missing.Stdout + $missing.Stderr).Contains('restore')) 'CLI missing task guidance failed'
    }
    Run 'forged A3 snapshot rejected' {
        $forged = [pscustomobject]@{ approved = $true; base = $base; testPolicy = 'local-gates-v1'; recordPolicy = 'external-current-v1'; text = 'Approved: 2026-09-29 by the human owner.' }
        Reject { Assert-ApprovedSpec 'h1-transport-identity' $forged } 'forged approval accepted'
    }
    Run 'immutable record rejects corruption' {
        $record = Write-NewRecord $taskDirectory 'runs' ([ordered]@{ test = 'value' }) 'unit-run'
        Assert ((Read-Record $taskDirectory 'runs' 'unit-run').hash -ceq $record.Hash) 'record hash'
        Reject { Write-NewRecord $taskDirectory 'runs' @{ test = 'other' } 'unit-run' } 'immutable overwrite accepted'
        $path = [IO.Path]::Combine($taskDirectory, 'runs', 'unit-run.json')
        $raw = [IO.File]::ReadAllText($path).Replace('value', 'tampered')
        [IO.File]::WriteAllText($path, $raw)
        Reject { Read-Record $taskDirectory 'runs' 'unit-run' } 'modified record accepted'
    }
    Run 'run interruption remains identifiable' {
        Start-Run $taskDirectory 'interrupted' $base '2026-09-29T00:00:00Z'
        $marker = [IO.Path]::Combine($taskDirectory, 'in-progress', 'interrupted.json')
        Assert ([IO.File]::Exists($marker)) 'in-progress marker missing'
        Assert (-not [IO.File]::Exists([IO.Path]::Combine($taskDirectory, 'runs', 'interrupted.json'))) 'unfinished run appeared completed'
        $failedRun = [ordered]@{ id = 'interrupted'; base=$base; head=$base; status='failed'; steps=@(); failure='simulated timeout' }
        [void](Finish-Run $taskDirectory $failedRun)
        Assert (-not [IO.File]::Exists($marker)) 'completed marker remains'
        Assert ((Read-Record $taskDirectory 'runs' 'interrupted').content.status -ceq 'failed') 'failure record missing'
    }
    Run 'handoff does not select on failed save' {
        $stale = Read-Current $taskDirectory
        $stale.revision = 0
        Reject { Publish-Handoff $taskDirectory $stale ([ordered]@{ inputKind='discovery'; head=$base }) 'h1-transport-identity' 'CDiscovery' 'r' $base $specRecord.Hash 'discovery' } 'stale handoff accepted'
        Assert (-not (Read-Current $taskDirectory).selectedInputId) 'failed handoff selected input'
    }
    Run 'current revision and restore' {
        $one = Read-Current $taskDirectory
        $one.nextAction = 'second'
        $two = Write-Current $taskDirectory $one 1
        Assert ($two.revision -eq 2) 'revision increment'
        Reject { Write-Current $taskDirectory $two 1 } 'stale update accepted'
        $restored = Restore-Current $taskDirectory
        Assert ($restored.revision -eq 3 -and $restored.phase -ceq 'B' -and -not $restored.gateReceiptId) 'restore invalidates gate'
        Assert ($restored.nextAction -like '*再照合*') 'restore next action'
    }
    Run 'missing current restores previous' {
        $current = Read-Current $taskDirectory
        $current.nextAction = 'before loss'
        [void](Write-Current $taskDirectory $current $current.revision)
        [IO.File]::Delete([IO.Path]::Combine($taskDirectory, 'CURRENT.json'))
        $restored = Restore-Current $taskDirectory
        Assert ($restored.phase -ceq 'B' -and -not $restored.gateReceiptId) 'missing current did not restore safely'
        Assert ((Read-Current $taskDirectory).revision -eq $restored.revision) 'restored current unreadable'
        $restored.nextAction = 'closed test'
        [void](Write-Current $taskDirectory $restored $restored.revision)
        [IO.File]::WriteAllText([IO.Path]::Combine($taskDirectory, 'closed.json'), (ConvertTo-Json ([ordered]@{ kind = 'closed'; closedAt = [DateTimeOffset]::UtcNow.ToString('o'); outcome = 'Abandoned' })))
        Reject { Restore-Current $taskDirectory } 'closed task reopened from previous'
        [IO.File]::Delete([IO.Path]::Combine($taskDirectory, 'closed.json'))
    }
    Run 'actual Git diff detects Unity' {
        [IO.Directory]::CreateDirectory([IO.Path]::Combine($repo, 'tools', 'Artifacts', 'tests')) | Out-Null
        [IO.File]::WriteAllText([IO.Path]::Combine($repo, 'tools', 'Artifacts', 'tests', 'sample.ps1'), 'test')
        [IO.Directory]::CreateDirectory([IO.Path]::Combine($repo, 'unity')) | Out-Null
        [IO.File]::WriteAllText([IO.Path]::Combine($repo, 'unity', 'change.cs'), 'class X {}')
        & git -C $repo add .
        & git -C $repo commit -qm candidate
        $head = (& git -C $repo rev-parse HEAD).Trim()
        $scope = Get-GitScope $repo $base $head
        Assert ($scope.Paths.Count -eq 2 -and $scope.Paths -contains 'unity/change.cs') 'Git path source'
        $specObject = [pscustomobject]@{ testPolicy = 'local-gates-v1'; recordPolicy = 'external-current-v1' }
        Reject { Get-RequiredSteps $specObject $scope.Paths 'discovery' } 'Unity changed with Artifacts-only claim'
        Reject { Get-GitScope $repo $head $base } 'nonancestor base accepted'
    }
    Run 'history catches removed evidence' {
        $handoff = [IO.Path]::Combine($repo, 'docs', 'handoff')
        [IO.Directory]::CreateDirectory($handoff) | Out-Null
        [IO.File]::WriteAllText([IO.Path]::Combine($handoff, 'PHASE_B_RESULT.md'), 'trial')
        & git -C $repo add docs/handoff/PHASE_B_RESULT.md
        $head = (& git -C $repo rev-parse HEAD).Trim()
        $staged = Find-GeneratedEvidenceAdds $repo $base $head
        Assert (@($staged.findings | Where-Object source -eq index).Count -eq 1) 'index evidence addition missing'
        & git -C $repo commit -qm add-evidence
        & git -C $repo rm -q docs/handoff/PHASE_B_RESULT.md
        & git -C $repo commit -qm remove-evidence
        $head = (& git -C $repo rev-parse HEAD).Trim()
        $removed = Find-GeneratedEvidenceAdds $repo $base $head
        Assert (@($removed.findings | Where-Object { $_.path -ceq 'docs/handoff/PHASE_B_RESULT.md' }).Count -ge 1) 'intermediate evidence disappeared from history audit'
        Assert (-not (Test-GeneratedEvidencePath 'unity/Assets/product.json')) 'product JSON falsely rejected'
    }
    Run 'gate requires exact run' {
        $specObject = [pscustomobject]@{ base = $base; specHash = $specRecord.Hash; testPolicy = 'local-gates-v1'; recordPolicy = 'external-current-v1' }
        $head = (& git -C $repo rev-parse HEAD).Trim()
        $run = [pscustomobject]@{ base = $base; head = $head; specHash = $specRecord.Hash; status = 'passed'; dirtyBefore = @(); dirtyAfter = @(); steps = @([pscustomobject]@{ name = 'artifacts-local'; status = 'passed'; registered = @('a'); selected = @('a'); executed = @('a') }) }
        Assert-RunForGate $run $specObject $head @('artifacts-local')
        Reject { Assert-RunForGate $run $specObject $head @('artifacts-local', 'harness-local') } 'missing suite accepted'
        $run.dirtyBefore = @(' M file')
        Reject { Assert-RunForGate $run $specObject $head @('artifacts-local') } 'dirty run accepted'
        $run.dirtyBefore = @(); $run.status = 'failed'
        Reject { Assert-RunForGate $run $specObject $head @('artifacts-local') } 'failed adopted run accepted'
        $run.status = 'passed'; $run.dirtyAfter = $null
        Reject { Assert-RunForGate $run $specObject $head @('artifacts-local') } 'unknown post-run dirty state accepted'
        $run.dirtyAfter = @()
    }
    Run 'case names and duplicates' {
        Assert-CaseSets @('a','b') @('b','a') @('a','b') 'fixture'
        Reject { Assert-CaseSets @('a','b') @('a','b') @('c','d') 'fixture' } 'equal-count mismatched execution accepted'
        Reject { Assert-CaseSets @('a','b') @('a','a') @('a','a') 'fixture' } 'duplicate selection accepted'
        Reject { Assert-CaseSets @('a','a') @('a','a') @('a','a') 'fixture' } 'duplicate registration accepted'
    }
    Run 'broad discovery run is reusable' {
        $specObject = [pscustomobject]@{ id = $specRecord.Id; base = $base; specHash = $specRecord.Hash; text = 'approved A3' }
        $head = (& git -C $repo rev-parse HEAD).Trim()
        $steps = @('artifacts-local', 'harness-local', 'contract-audit', 'docs-audit')
        $run = [pscustomobject]@{ id = 'r'; recordHash = 'h'; base = $base; head = $head; specHash = $specRecord.Hash; stage = 'discovery'; status = 'passed'; dirtyBefore = @(); dirtyAfter = @(); implementationResult = 'done'; steps = @($steps | ForEach-Object { [pscustomobject]@{ name = $_; status = 'passed'; registered = @('case'); selected = @('case'); executed = @('case') } }) }
        Assert-RunForGate $run $specObject $head $steps
        $input = Select-BlindInput $specObject $run 'judgment' @('tools/Harness/sample.ps1') 'diff'
        Assert ($input.inputKind -ceq 'judgment' -and $input.runId -ceq 'r') 'broad run was not reusable for judgment input'
    }
    Run 'reference ownership and expiry' {
        $now = [DateTimeOffset]::UtcNow
        $external = [pscustomobject]@{ referenceId = 'x'; consumerId = 'defect-1'; purpose = 'defect'; runId = 'r'; owner = 'other'; expiresAt = $now.AddMinutes(-1).ToString('o'); releasedAt = $null }
        Reject { Resolve-CloseReferences @($external) 'h1-transport-identity' $true $now } 'expired external reference accepted'
        $external.expiresAt = $now.AddDays(2).ToString('o')
        $own = [pscustomobject]@{ referenceId = 'o'; consumerId = 'h1-transport-identity'; purpose = 'adopted-run'; runId = 'r'; owner = 'test'; expiresAt = $now.AddMinutes(-1).ToString('o'); releasedAt = $null }
        $resolved = @(Resolve-CloseReferences @($external, $own) 'h1-transport-identity' $true $now)
        Assert ($resolved.Count -eq 2 -and $resolved[0].releasedAt -eq $null -and $resolved[1].releasedAt) 'close did not preserve external reference or release own reference'
        $selfDefect = [pscustomobject]@{ referenceId = 'self-defect'; consumerId = 'h1-transport-identity'; purpose = 'defect'; runId = 'r'; owner = 'test'; expiresAt = $now.AddDays(2).ToString('o'); releasedAt = $null }
        $kept = @(Resolve-CloseReferences @($selfDefect) 'h1-transport-identity' $true $now)
        Assert ($kept.Count -eq 1 -and -not $kept[0].releasedAt) 'close released a defect reference owned by this task'
        $review = [pscustomobject]@{ referenceId = 'v'; consumerId = 'CDiscovery'; purpose = 'review'; runId = 'r'; owner = 'test'; expiresAt = $now.AddDays(1).ToString('o'); releasedAt = $null }
        Reject { Resolve-CloseReferences @($review) 'h1-transport-identity' $false $now } 'open review accepted'
    }
    Run 'restore preserves active references' {
        $current = Read-Current $taskDirectory
        $current.references = @()
        [void](Write-Current $taskDirectory $current $current.revision)
        $current = Read-Current $taskDirectory
        $current.references = @([pscustomobject]@{ referenceId = 'defect-ref'; consumerId = 'defect'; purpose = 'defect'; runId = 'r'; owner = 'other'; expiresAt = [DateTimeOffset]::UtcNow.AddDays(2).ToString('o'); releasedAt = $null })
        [void](Write-Current $taskDirectory $current $current.revision)
        Reject { Restore-Current $taskDirectory } 'restore lost an active defect reference'
    }
    Run 'close checks revision under lock' {
        $current = Read-Current $taskDirectory
        Reject { Write-Closed $taskDirectory ($current.revision - 1) { param($latest) @{ kind='closed'; closedAt='now'; outcome='Abandoned' } } } 'stale close accepted'
        Assert (-not (Test-TaskClosed $taskDirectory)) 'stale close created terminal record'
    }
    Run 'blind input omits findings' {
        $specObject = [pscustomobject]@{ id = $specRecord.Id; specHash = $specRecord.Hash; base = $base; text = 'approved A3' }
        $run = [pscustomobject]@{ id = 'r'; recordHash = 'hash'; head = 'head'; steps = @(); implementationResult = 'implemented'; cFinding = 'SENTINEL_C_FINDING' }
        $input = Select-BlindInput $specObject $run 'judgment' @('tools/Harness/sample.ps1')
        $json = ConvertTo-Json $input -Depth 10
        Assert (-not $json.Contains('SENTINEL_C_FINDING')) 'C finding leaked'
        Assert ($input.inputKind -ceq 'judgment') 'input kind'
    }
    Run 'child process result' {
        $result = Invoke-Process 'pwsh' @('-NoProfile', '-Command', '[Console]::WriteLine("OK")') $repo 15
        Assert ($result.ExitCode -eq 0 -and $result.Stdout.Trim() -ceq 'OK' -and $result.DurationMs -ge 0) 'child output'
    }
    Run 'close persists current references' {
        $current = Read-Current $taskDirectory
        $closed = Write-Closed $taskDirectory $current.revision {
            param($latest)
            [ordered]@{ kind='closed'; closedAt=[DateTimeOffset]::UtcNow.ToString('o'); outcome='Abandoned'; references=@($latest.references) }
        }
        Assert ((Test-TaskClosed $taskDirectory) -and @($closed.references | Where-Object referenceId -eq 'defect-ref').Count -eq 1) 'close omitted active reference'
        Reject { Write-Current $taskDirectory $current $current.revision } 'closed task accepted concurrent update'
    }
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not $resolved.StartsWith($tempRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'osm-harness-test-*') { throw 'test cleanup target is outside its temp root' }
    if ([IO.Directory]::Exists($resolved)) {
        foreach ($file in [IO.Directory]::EnumerateFiles($resolved, '*', [IO.SearchOption]::AllDirectories)) { [IO.File]::SetAttributes($file, [IO.FileAttributes]::Normal) }
        [IO.Directory]::Delete($resolved, $true)
    }
}
if ($ResultPath) {
    $result = [ordered]@{ registered = @($registered); selected = @($selected); executed = @($executed); failed = @($failed) }
    [IO.File]::WriteAllText($ResultPath, (ConvertTo-Json -InputObject $result -Depth 10), [Text.UTF8Encoding]::new($false))
}
foreach ($failure in $failed) { [Console]::Error.WriteLine("FAIL $failure") }
[Console]::WriteLine("Harness tests: $($executed.Count) executed, $($failed.Count) failed")
if ($failed.Count -gt 0 -or $executed.Count -ne $registered.Count) { exit 1 }
