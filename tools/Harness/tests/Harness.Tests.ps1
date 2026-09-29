param([string]$ResultPath = '')
# 登録名は下の Run と件数を一致させる。選択・実行がずれると終了コードを落とす。
# 一時 Git とストアはテスト専用。掃除は temp 配下のこのディレクトリだけに限る。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../RecordStore.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../GatePolicy.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../Adapters/LocalChecks.psm1') -Force
$registered = @('repository identity and explicit task', 'CLI status and missing task', 'forged A3 snapshot rejected', 'immutable record rejects corruption', 'run interruption remains identifiable', 'handoff does not select on failed save', 'current revision and restore', 'missing current restores previous', 'corrupt current is not overwritten', 'restore rejects dangling reference', 'run payload rejects old DLL and modification', 'failing step keeps record', 'run record carries identity and monotonic duration', 'actual Git diff detects Unity', 'history catches removed evidence', 'gate requires exact run', 'case names and duplicates', 'broad discovery run is reusable', 'reference ownership and expiry', 'restore preserves active references', 'close checks revision under lock', 'close rejects in-progress run', 'blind input omits findings', 'blind handoff records receipt and review', 'child process result', 'new run invalidates prior judgment', 'close persists current references', 'terminal survives display update failure')
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
    try { & $Body; $executed.Add($Name) } catch { $executed.Add($Name); $failed.Add("$Name`: $($_.Exception.Message) $($_.ScriptStackTrace)") }
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

    # SSH と HTTPS の origin を同じ保存先 ID に畳む。task-id のパス逸脱は拒否する。
    Run 'repository identity and explicit task' {
        Assert ($identity.Canonical -ceq 'github.com/tetsujiaoyagi/samplegameforonestarmakerframework') 'SSH normalization'
        & git -C $repo remote set-url origin 'https://github.com/TetsujiAoyagi/SampleGameForOneStarMakerFramework.git'
        Assert ((Get-RepositoryIdentity $repo).Id -ceq $identity.Id) 'HTTPS normalization'
        Reject { Get-TaskDirectory $repo '../escape' $store } 'invalid task id accepted'
    }
    # 登録済みは一覧し、未知の task は init せず status と restore を案内して失敗する。
    Run 'CLI status and missing task' {
        $cli = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '../../harness.ps1'))
        $listed = Invoke-Process 'pwsh' @('-NoProfile', '-File', $cli, 'status', '-Repo', $repo, '-StoreRoot', $store) $repo 15
        Assert ($listed.ExitCode -eq 0 -and $listed.Stdout.Contains('h1-transport-identity owner=test phase=B')) 'CLI status failed'
        $missing = Invoke-Process 'pwsh' @('-NoProfile', '-File', $cli, 'current', '-Repo', $repo, '-StoreRoot', $store, '-Task', 'missing') $repo 15
        Assert ($missing.ExitCode -ne 0 -and ($missing.Stdout + $missing.Stderr).Contains('status') -and ($missing.Stdout + $missing.Stderr).Contains('restore')) 'CLI missing task guidance failed'
    }
    # 承認文面だけでは通さず、本文 hash が承認値と違う仕様を拒否する。
    Run 'forged A3 snapshot rejected' {
        $forged = [pscustomobject]@{ approved = $true; base = $base; testPolicy = 'local-gates-v1'; recordPolicy = 'external-current-v1'; text = 'Approved: 2026-09-29 by the human owner.' }
        Reject { Assert-ApprovedSpec 'h1-transport-identity' $forged } 'forged approval accepted'
    }
    # 同じ id の上書きと、本文を改ざんしたあとの読取を拒否する。
    Run 'immutable record rejects corruption' {
        $record = Write-NewRecord $taskDirectory 'runs' ([ordered]@{ test = 'value' }) 'unit-run'
        Assert ((Read-Record $taskDirectory 'runs' 'unit-run').hash -ceq $record.Hash) 'record hash'
        Reject { Write-NewRecord $taskDirectory 'runs' @{ test = 'other' } 'unit-run' } 'immutable overwrite accepted'
        $path = [IO.Path]::Combine($taskDirectory, 'runs', 'unit-run.json')
        $raw = [IO.File]::ReadAllText($path).Replace('value', 'tampered')
        [IO.File]::WriteAllText($path, $raw)
        Reject { Read-Record $taskDirectory 'runs' 'unit-run' } 'modified record accepted'
    }
    # 完了ファイルが無い中断は進行中のまま残し、失敗として閉じたときだけ run になる。
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
    # revision が一致しない引渡しは、入力を選択済みにしない。
    Run 'handoff does not select on failed save' {
        $stale = Read-Current $taskDirectory
        $stale.revision = 0
        Reject { Publish-Handoff $taskDirectory $stale ([ordered]@{ inputKind='discovery'; head=$base }) 'h1-transport-identity' 'CDiscovery' 'r' $base $specRecord.Hash 'discovery' } 'stale handoff accepted'
        Assert (-not (Read-Current $taskDirectory).selectedInputId) 'failed handoff selected input'
    }
    # 古い revision の更新を拒否し、復旧は phase を戻して受領を無効にする。
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
    # CURRENT の欠落は直前版で戻す。close 後の復旧は拒否する。
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
    # 存在する CURRENT が読めないとき、例外の文面で復旧可否を決めず原本を保全する。
    Run 'corrupt current is not overwritten' {
        $path = [IO.Path]::Combine($taskDirectory, 'CURRENT.json')
        $before = [IO.File]::ReadAllText($path)
        try {
            [IO.File]::WriteAllText($path, '{broken')
            Reject { Restore-Current $taskDirectory } 'corrupt current was silently overwritten'
            Assert ([IO.File]::ReadAllText($path) -ceq '{broken') 'corrupt current changed after failed restore'
        } finally { [IO.File]::WriteAllText($path, $before) }
    }
    # 直前版に残るreview/defect参照も、採用run以外を含めて実体を照合する。
    Run 'restore rejects dangling reference' {
        $path = [IO.Path]::Combine($taskDirectory, 'CURRENT.json')
        $previous = [IO.Path]::Combine($taskDirectory, 'CURRENT.previous')
        $before = [IO.File]::ReadAllText($path)
        $beforePrevious = [IO.File]::ReadAllText($previous)
        try {
            $candidate = $before | ConvertFrom-Json -Depth 40 -DateKind String
            $candidate.references = @([pscustomobject]@{ referenceId='dangling'; consumerId='defect'; purpose='defect'; runId='missing-run'; owner='owner'; expiresAt=[DateTimeOffset]::UtcNow.AddDays(1).ToString('o'); releasedAt=$null })
            [IO.File]::WriteAllText($previous, (ConvertTo-Json -InputObject $candidate -Depth 40))
            Reject { Restore-Current $taskDirectory } 'restore accepted missing referenced run'
            Assert ([IO.File]::ReadAllText($path) -ceq $before) 'rejected restore changed CURRENT'
        } finally {
            [IO.File]::WriteAllText($path, $before)
            [IO.File]::WriteAllText($previous, $beforePrevious)
        }
    }
    # run IDごとの専用payload以外を読み込んだDLLと、確定後に書き換えた生ログを拒否する。
    Run 'run payload rejects old DLL and modification' {
        $runId = 'payload-fixture'
        $folder = [IO.Path]::Combine($taskDirectory, 'payload', $runId)
        [IO.Directory]::CreateDirectory($folder) | Out-Null
        $binary = [IO.Path]::Combine($folder, 'test.dll')
        $log = [IO.Path]::Combine($folder, 'test.log')
        [IO.File]::WriteAllText($binary, 'new-binary')
        [IO.File]::WriteAllText($log, 'pass')
        $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $binary).Hash.ToLowerInvariant()
        $logHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $log).Hash
        $combined = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($logHash))).ToLowerInvariant()
        $step = [pscustomobject]@{ name='artifacts-local'; logs=@("payload/$runId/test.log"); logHash=$combined; binaryPath=$binary; binaryHashBefore=$hash; binaryHashAfter=$hash; loadedPath=$binary; loadedHash=$hash; dependencies=@([pscustomobject]@{ status='memory'; name='fixture' }) }
        $run = [pscustomobject]@{ id=$runId; steps=@($step) }
        Assert-RunPayload $taskDirectory $run
        $old = [IO.Path]::Combine($taskDirectory, 'payload', 'older-run', 'test.dll')
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($old)) | Out-Null
        [IO.File]::Copy($binary, $old)
        $step.binaryPath = $old; $step.loadedPath = $old
        Reject { Assert-RunPayload $taskDirectory $run } 'old run DLL accepted'
        $step.binaryPath = $binary; $step.loadedPath = $binary
        [IO.File]::WriteAllText($log, 'tampered')
        Reject { Assert-RunPayload $taskDirectory $run } 'modified raw log accepted'
    }
    # 子processの失敗でも実行引数・ログ・時間が固定stepに残り、原本を照合できる。
    Run 'failing step keeps record' {
        $step = Invoke-LocalStep $repo $taskDirectory 'failed-step-fixture' 'artifacts-local'
        Assert ($step.status -ceq 'failed' -and $step.exitCode -ne 0) 'missing project unexpectedly succeeded'
        Assert (@($step.argv).Count -eq 1 -and $step.argv[0].executable -ceq 'dotnet' -and @($step.argv[0].arguments).Count -gt 0) 'failed step lost command arguments'
        Assert ($step.cwd -ceq $repo -and @($step.logs).Count -gt 0 -and $step.logHash -and $step.timing.buildMs -ge 0) 'failed step lost evidence'
        Assert-RunPayload $taskDirectory ([pscustomobject]@{ id='failed-step-fixture'; steps=@($step) })
    }
    # 壁時計が補正されても計測値は注入した単調時計の経過値を正とし、adapter版まで固定する。
    Run 'run record carries identity and monotonic duration' {
        $policySpec = [pscustomobject]@{ base=$base; specHash=$specRecord.Hash; testPolicy='local-gates-v1'; trialDays=7 }
        $policyCurrent = [pscustomobject]@{ repoId=$identity.Id; specId=$specRecord.Id }
        $scope = [pscustomobject]@{ Head=$base; Paths=@('tools/Harness/sample.ps1'); Dirty=@() }
        $started = [DateTimeOffset]::UtcNow.AddHours(1)
        $record = New-RunResult -Spec $policySpec -Current $policyCurrent -Task 'h1-transport-identity' -Stage 'discovery' -Scope $scope -After $scope -Started $started -ElapsedMs 123 -Id 'clock-fixture' -PreviousRun '' -Difference '初回' -Question 'identity' -StopWhen 'checked' -Results @() -Failure ''
        Assert ($record.durationMs -eq 123 -and $record.repoId -ceq $identity.Id -and $record.suiteVersion -ceq 'local-gates-v1' -and $record.adapterVersion -ceq "git:$base") 'run identity or monotonic duration lost'
    }
    # Unity を含む差分は、Artifacts だけの必須集合にしない。祖先でない base は拒否する。
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
    # 後で削除した生成証拠も、途中 commit と index の追加として残る。製品 JSON は証拠パスにしない。
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
    # 必須 step の欠落、dirty、失敗、run 後の dirty 不明は、完了引渡しに使えない。
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
    # 件数だけでは通さず、重複と中身の不一致を拒否する。順序は問わない。
    Run 'case names and duplicates' {
        Assert-CaseSets @('a','b') @('b','a') @('a','b') 'fixture'
        Reject { Assert-CaseSets @('a','b') @('a','b') @('c','d') 'fixture' } 'equal-count mismatched execution accepted'
        Reject { Assert-CaseSets @('a','b') @('a','a') @('a','a') 'fixture' } 'duplicate selection accepted'
        Reject { Assert-CaseSets @('a','a') @('a','a') @('a','a') 'fixture' } 'duplicate registration accepted'
    }
    # 広い集合を実行済みの discovery run は、判定入力の種別へ再利用できる。
    Run 'broad discovery run is reusable' {
        $specObject = [pscustomobject]@{ id = $specRecord.Id; base = $base; specHash = $specRecord.Hash; text = 'approved A3' }
        $head = (& git -C $repo rev-parse HEAD).Trim()
        $steps = @('artifacts-local', 'harness-local', 'contract-audit', 'docs-audit')
        $run = [pscustomobject]@{ id = 'r'; recordHash = 'h'; base = $base; head = $head; specHash = $specRecord.Hash; stage = 'discovery'; status = 'passed'; dirtyBefore = @(); dirtyAfter = @(); predecessor = 'earlier'; implementationResult = 'done'; steps = @($steps | ForEach-Object { [pscustomobject]@{ name = $_; status = 'passed'; registered = @('case'); selected = @('case'); executed = @('case') } }) }
        Assert-RunForGate $run $specObject $head $steps
        $input = Select-BlindInput $specObject $run 'judgment' @('tools/Harness/sample.ps1') 'diff'
        Assert ($input.inputKind -ceq 'judgment' -and $input.runId -ceq 'r') 'broad run was not reusable for judgment input'
    }
    # 期限切れの他者参照は close を止める。自分の採用 run は終了し、欠陥参照は残す。
    # 進行中のレビューは、終了の明示が無いと拒否する。
    Run 'reference ownership and expiry' {
        $now = [DateTimeOffset]::UtcNow
        $external = [pscustomobject]@{ referenceId = 'x'; consumerId = 'defect-1'; purpose = 'defect'; runId = 'r'; owner = 'other'; expiresAt = $now.AddMinutes(-1).ToString('o'); releasedAt = $null }
        Reject { Resolve-CloseReferences @($external) 'h1-transport-identity' $true $now 'test' } 'expired external reference accepted'
        $external.expiresAt = $now.AddDays(2).ToString('o')
        $own = [pscustomobject]@{ referenceId = 'o'; consumerId = 'h1-transport-identity'; purpose = 'adopted-run'; runId = 'r'; owner = 'test'; expiresAt = $now.AddMinutes(-1).ToString('o'); releasedAt = $null }
        $resolved = @(Resolve-CloseReferences @($external, $own) 'h1-transport-identity' $true $now 'test')
        Assert ($resolved.Count -eq 2 -and $resolved[0].releasedAt -eq $null -and $resolved[1].releasedAt) 'close did not preserve external reference or release own reference'
        $selfDefect = [pscustomobject]@{ referenceId = 'self-defect'; consumerId = 'h1-transport-identity'; purpose = 'defect'; runId = 'r'; owner = 'test'; expiresAt = $now.AddDays(2).ToString('o'); releasedAt = $null }
        $kept = @(Resolve-CloseReferences @($selfDefect) 'h1-transport-identity' $true $now 'test')
        Assert ($kept.Count -eq 1 -and -not $kept[0].releasedAt) 'close released a defect reference owned by this task'
        $review = [pscustomobject]@{ referenceId = 'v'; consumerId = 'CDiscovery'; purpose = 'review'; runId = 'r'; owner = 'test'; expiresAt = $now.AddDays(1).ToString('o'); releasedAt = $null }
        Reject { Resolve-CloseReferences @($review) 'h1-transport-identity' $false $now 'test' } 'open review accepted'
        $externalReview = [pscustomobject]@{ referenceId = 'external-review'; consumerId = 'other-consumer'; purpose = 'review'; runId = 'r'; owner = 'other-owner'; expiresAt = $now.AddDays(2).ToString('o'); releasedAt = $null }
        $preserved = @(Resolve-CloseReferences @($externalReview) 'h1-transport-identity' $true $now 'test')
        Assert ($preserved.Count -eq 1 -and -not $preserved[0].releasedAt) 'close released another consumer review'
    }
    # 現行にだけある有効参照を、直前版へ戻して消すことはできない。
    Run 'restore preserves active references' {
        $current = Read-Current $taskDirectory
        $current.references = @()
        [void](Write-Current $taskDirectory $current $current.revision)
        $current = Read-Current $taskDirectory
        $current.references = @([pscustomobject]@{ referenceId = 'defect-ref'; consumerId = 'defect'; purpose = 'defect'; runId = 'r'; owner = 'other'; expiresAt = [DateTimeOffset]::UtcNow.AddDays(2).ToString('o'); releasedAt = $null })
        [void](Write-Current $taskDirectory $current $current.revision)
        Reject { Restore-Current $taskDirectory } 'restore lost an active defect reference'
    }
    # 古い revision の close は、終端ファイルを作らない。
    Run 'close checks revision under lock' {
        $current = Read-Current $taskDirectory
        Reject { Write-Closed $taskDirectory ($current.revision - 1) { param($latest) @{ kind='closed'; closedAt='now'; outcome='Abandoned' } } } 'stale close accepted'
        Assert (-not (Test-TaskClosed $taskDirectory)) 'stale close created terminal record'
    }
    # 固定入力へ写す項目に、レビュー所見のフィールドは含めない。
    Run 'blind input omits findings' {
        $specObject = [pscustomobject]@{ id = $specRecord.Id; specHash = $specRecord.Hash; base = $base; text = 'approved A3' }
        $run = [pscustomobject]@{ id = 'r'; recordHash = 'hash'; head = 'head'; steps = @(); predecessor = 'prior'; implementationResult = 'implemented'; cFinding = 'SENTINEL_C_FINDING' }
        $input = Select-BlindInput $specObject $run 'judgment' @('tools/Harness/sample.ps1')
        $json = ConvertTo-Json $input -Depth 10
        Assert (-not $json.Contains('SENTINEL_C_FINDING')) 'C finding leaked'
        Assert ($input.inputKind -ceq 'judgment') 'input kind'
    }
    # 子プロセスの標準出力と終了コードを、期限付きで回収する。
    Run 'child process result' {
        $result = Invoke-Process 'pwsh' @('-NoProfile', '-Command', '[Console]::WriteLine("OK")') $repo 15
        Assert ($result.ExitCode -eq 0 -and $result.Stdout.Trim() -ceq 'OK' -and $result.DurationMs -ge 0 -and $result.LaunchMs -ge 0) 'child output'
        $timed = Invoke-Process 'pwsh' @('-NoProfile', '-Command', 'while ($true) {}') $repo 1
        Assert ($timed.TimedOut -and $timed.ExitCode -ne 0 -and $timed.DurationMs -ge 1000) 'timeout did not return a failed process record'
    }
    # 同じ task の新runでは、旧headの判定入力と受領を現行表示から外す。
    Run 'new run invalidates prior judgment' {
        $current = Read-Current $taskDirectory
        $current | Add-Member -NotePropertyName candidateHead -NotePropertyValue 'old-head' -Force
        $current.phase = 'CJudgment'
        $current.selectedInputId = 'old-input'
        $current.judgmentInputId = 'old-input'
        $current.gateReceiptId = 'old-receipt'
        $updated = Set-RunCurrent $taskDirectory $current $base 'new-run'
        Assert ($updated.phase -ceq 'B' -and $updated.candidateHead -ceq $base) 'new run did not reset phase/head'
        Assert (-not $updated.selectedInputId -and -not $updated.judgmentInputId -and -not $updated.gateReceiptId) 'old judgment remained selected'
    }
    # CBlindは判定入力の同じIDを返し、別の固定receiptとreview参照を1回だけ残す。
    Run 'blind handoff records receipt and review' {
        $input = Write-NewRecord $taskDirectory 'inputs' ([ordered]@{ inputKind='judgment'; runId='blind-fixture'; head=$base }) 'blind-input'
        $inputRecord = Read-Record $taskDirectory 'inputs' $input.Id
        $before = Read-Current $taskDirectory
        $first = Publish-BlindHandoff $taskDirectory $before $inputRecord 'h1-transport-identity' 'blind-fixture' $base $specRecord.Hash
        $after = Read-Current $taskDirectory
        Assert ($first.Id -and $after.phase -ceq 'CBlind' -and @($after.references | Where-Object { $_.consumerId -ceq 'CBlind' -and -not $_.releasedAt }).Count -eq 1) 'CBlind receipt or review reference missing'
        $again = Publish-BlindHandoff $taskDirectory $after $inputRecord 'h1-transport-identity' 'blind-fixture' $base $specRecord.Hash
        Assert ($again.id -ceq $first.Id -and (Read-Current $taskDirectory).revision -eq $after.revision) 'CBlind reread created another receipt or reference'
    }
    # closeは同じtask lockの下で進行中runを見て、終端recordの確定を拒否する。
    Run 'close rejects in-progress run' {
        $current = Read-Current $taskDirectory
        Start-Run $taskDirectory 'close-pending' $base ([DateTimeOffset]::UtcNow.ToString('o'))
        try {
            Reject { Write-Closed $taskDirectory $current.revision { param($latest) @{ kind='closed'; closedAt=[DateTimeOffset]::UtcNow.ToString('o'); outcome='Abandoned' } } } 'close accepted an unfinished run'
            Assert (-not [IO.File]::Exists([IO.Path]::Combine($taskDirectory, 'closed.json'))) 'close wrote a terminal record with an unfinished run'
        } finally {
            [void](Finish-Run $taskDirectory ([ordered]@{ id='close-pending'; base=$base; head=$base; status='failed'; steps=@(); failure='fixture ended' }))
        }
    }
    # close は当時の参照を終端ファイルへ残し、以降の CURRENT 更新を拒否する。
    Run 'close persists current references' {
        $current = Read-Current $taskDirectory
        $closed = Write-Closed $taskDirectory $current.revision {
            param($latest)
            [ordered]@{ kind='closed'; closedAt=[DateTimeOffset]::UtcNow.ToString('o'); outcome='Abandoned'; references=@($latest.references) }
        }
        Assert ((Test-TaskClosed $taskDirectory) -and @($closed.references | Where-Object referenceId -eq 'defect-ref').Count -eq 1) 'close omitted active reference'
        Assert ((Read-Current $taskDirectory).phase -ceq 'closed') 'CURRENT display was not updated after terminal record'
        Reject { Write-Current $taskDirectory $current $current.revision } 'closed task accepted concurrent update'
    }
    # 表示更新が失敗しても終端ファイルは残り、status/restoreはcloseとして扱う。
    Run 'terminal survives display update failure' {
        $isolated = [IO.Path]::Combine($root, 'display-failure-task')
        [IO.Directory]::CreateDirectory($isolated) | Out-Null
        [IO.File]::WriteAllText([IO.Path]::Combine($isolated, 'registration.json'), (ConvertTo-Json ([ordered]@{ taskId='isolated'; repoId=$identity.Id })))
        [IO.File]::WriteAllText([IO.Path]::Combine($isolated, 'CURRENT.json'), (ConvertTo-Json ([ordered]@{ schemaVersion=1; revision=1; taskId='isolated'; repoId=$identity.Id; specId='fixture-spec'; phase='B'; references=@() })))
        $closed = Write-Closed $isolated 1 { param($latest) [ordered]@{ kind='closed'; closedAt=[DateTimeOffset]::UtcNow.ToString('o'); outcome='Abandoned' } } { throw 'fixture display failure' }
        Assert ($closed.outcome -ceq 'Abandoned' -and (Test-TaskClosed $isolated)) 'terminal record lost on display failure'
        Assert ((Read-Current $isolated).phase -ceq 'B') 'display unexpectedly changed after injected failure'
        Reject { Restore-Current $isolated } 'closed task reopened after display failure'
    }
} finally {
    # 掃除先が temp のこのテストディレクトリ以外なら削除しない。
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
