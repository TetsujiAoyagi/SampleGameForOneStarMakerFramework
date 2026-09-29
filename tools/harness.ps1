#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Position=0, Mandatory=$true)][ValidateSet('init','status','current','run','adopt','handoff','assist','reference','close','restore')][string]$Command,
    [string]$Task = '',
    [string]$Repo = (Split-Path -Parent $PSScriptRoot),
    [string]$StoreRoot = '',
    [string]$SpecFile = '',
    [string]$Owner = '',
    [ValidateSet('discovery','judgment')][string]$Stage = 'discovery',
    [string]$RunId = '',
    [string]$PreviousRun = '',
    [string]$Difference = '',
    [string]$Question = '',
    [string]$StopWhen = '',
    [string]$Reason = '',
    [ValidateSet('B','CDiscovery','CJudgment','CBlind')][string]$To = 'B',
    [ValidateSet('Completed','Abandoned')][string]$Outcome = 'Completed',
    [string]$CReview = '',
    [string]$CBlindReview = '',
    [string]$RetainUntil = '',
    [switch]$ReviewClosed,
    [ValidateSet('Add','Release','Extend')][string]$Action = 'Add',
    [string]$ReferenceId = '',
    [string]$ConsumerId = '',
    [ValidateSet('adopted-run','review','defect','other')][string]$Purpose = 'other',
    [string]$ExpiresAt = '',
    [int]$ExpectedRevision = 0,
    [string]$NextAction = '',
    [string[]]$Unresolved = @(),
    [string[]]$Blockers = @()
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Harness/RecordStore.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Harness/GatePolicy.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Harness/Adapters/LocalChecks.psm1') -Force

function Get-Head {
    $value = & git -C $Repo rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Git HEADを取得できません。' }
    return $value.Trim()
}
function Get-Context {
    if (-not $Task) { throw '-Taskでtask-idを指定してください。' }
    $directory = Get-TaskDirectory $Repo $Task $StoreRoot
    $current = Read-Current $directory
    $record = Read-Record $directory 'specifications' $current.specId
    $spec = $record.content
    Assert-ApprovedSpec $Task $spec
    $spec | Add-Member -NotePropertyName id -NotePropertyValue $record.id -Force
    $spec | Add-Member -NotePropertyName specHash -NotePropertyValue $record.hash -Force
    return [pscustomobject]@{ Directory = $directory; Current = $current; Spec = $spec }
}
function Save-CurrentFor([object]$Context) { [void](Write-Current $Context.Directory $Context.Current $Context.Current.revision) }
try {
    if ($Command -ceq 'status') {
        $identity = Get-RepositoryIdentity $Repo
        $root = if ($StoreRoot) { [IO.Path]::Combine($StoreRoot, $identity.Id, 'tasks') } else { [IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'), 'OneStarMaker', 'Harness', $identity.Id, 'tasks') }
        if (-not [IO.Directory]::Exists($root)) { Write-Output 'taskはありません。新規作業のみinitを実行してください。'; return }
        foreach ($dir in [IO.Directory]::EnumerateDirectories($root)) {
            $id = [IO.Path]::GetFileName($dir)
            try {
                if (Test-TaskClosed $dir) { Write-Output "$id phase=closed next=新規taskをinit"; continue }
                $current = Read-Current $dir
                $expired = @($current.references | Where-Object { $_.expiresAt -and -not $_.releasedAt -and [DateTimeOffset]::Parse($_.expiresAt) -lt [DateTimeOffset]::UtcNow }).Count
                Write-Output "$id owner=$($current.owner) phase=$($current.phase) expiredRefs=$expired next=current -Task $id"
            } catch { Write-Output "$id unhealthy: $($_.Exception.Message)" }
        }
        return
    }
    if (-not $Task) { throw '-Taskでtask-idを指定してください。' }
    $directory = Get-TaskDirectory $Repo $Task $StoreRoot
    if ($Command -ceq 'init') {
        if (-not $Owner -or -not $SpecFile) { throw 'initには-Ownerと承認済み-SpecFileが必要です。' }
        if ([IO.Directory]::Exists($directory)) { throw 'taskは登録済みです。status/currentを使い、欠落時はrestoreしてください。' }
        $text = [IO.File]::ReadAllText([IO.Path]::GetFullPath($SpecFile))
        if ($text -cnotmatch 'Approved: 2026-09-29 by the human owner\.' -or $text -cnotmatch 'Implementation base: ([a-f0-9]{40})') { throw '承認済みA3 snapshotと完全base SHAが必要です。' }
        $base = $Matches[1]
        $identity = Get-RepositoryIdentity $Repo
        $spec = [ordered]@{ approved = $true; title = 'H1-PILOT-TRANSPORT-IDENTITY'; question = '過去RESULTを読まずに現行仕様と採用runから着手・判定できるか'; summary = 'Git外CURRENT、機械run、Artifacts/Harness限定のB exitとC entry'; outOfScope = 'Unity・R2 live・Route Proof本体・既存履歴の削除'; minimum = 'A3 snapshotのM1〜M6を満たす'; base = $base; testPolicy = 'local-gates-v1'; recordPolicy = 'external-current-v1'; trialDays = 7; adoptedDaysAfterClose = 30; text = $text }
        Assert-ApprovedSpec $Task ([pscustomobject]$spec)
        $specRecord = Initialize-Task $directory $Task $Owner $identity $spec
        Write-Output "initialized $Task spec=$($specRecord.Id) base=$base"
        return
    }
    if ($Command -ceq 'restore') { $result = Restore-Current $directory; Write-Output "restored revision=$($result.revision); gateは再照合が必要です。"; return }
    $context = Get-Context
    if ($Command -ceq 'current') {
        if ($PSBoundParameters.ContainsKey('NextAction') -or $PSBoundParameters.ContainsKey('Unresolved') -or $PSBoundParameters.ContainsKey('Blockers')) {
            if ($ExpectedRevision -ne $context.Current.revision) { throw '更新には一致する-ExpectedRevisionが必要です。currentを読み直してください。' }
            if ($PSBoundParameters.ContainsKey('NextAction')) { $context.Current.nextAction = $NextAction }
            if ($PSBoundParameters.ContainsKey('Unresolved')) { $context.Current.unresolved = @($Unresolved | Where-Object { $_ }) }
            if ($PSBoundParameters.ContainsKey('Blockers')) { $context.Current.blockers = @($Blockers | Where-Object { $_ }) }
            Save-CurrentFor $context
            $context = Get-Context
        }
        $closed = Test-TaskClosed $directory
        Write-Output "task=$Task revision=$($context.Current.revision) owner=$($context.Current.owner) phase=$(if ($closed) {'closed'} else {$context.Current.phase})"
        Write-Output "A3=$($context.Spec.title) base=$($context.Spec.base) head=$($context.Current.candidateHead)"
        Write-Output "question=$($context.Spec.question)"
        Write-Output "summary=$($context.Spec.summary) outOfScope=$($context.Spec.outOfScope) minimum=$($context.Spec.minimum)"
        Write-Output "unresolved=$($context.Current.unresolved -join '; ') blockers=$($context.Current.blockers -join '; ')"
        Write-Output "next=$($context.Current.nextAction) input=$($context.Current.selectedInputId) receipt=$($context.Current.gateReceiptId)"
        foreach ($adopted in @($context.Current.adoptedRuns)) { Write-Output "adopted-run=$($adopted.id) reason=$($adopted.reason)" }
        return
    }
    if (Test-TaskClosed $directory) { throw 'taskはclose済みです。新しいtaskをinitしてください。' }
    if ($Command -ceq 'run') {
        if (-not $Question -or -not $StopWhen -or -not $Difference) { throw 'runには前回との差・知りたい条件・停止条件を入力してください。初回は-Difference 初回です。' }
        if ($Difference -cne '初回' -and -not $PreviousRun) { throw '再実行には-PreviousRunが必要です。' }
        if ($PreviousRun) { [void](Read-Record $directory 'runs' $PreviousRun) }
        $head = Get-Head
        $scope = Get-GitScope $Repo $context.Spec.base $head
        $steps = Get-RequiredSteps $context.Spec $scope.Paths $Stage
        $id = [Guid]::NewGuid().ToString('N')
        $started = [DateTimeOffset]::UtcNow
        Start-Run $directory $id $head $started.ToString('o')
        $results = [Collections.Generic.List[object]]::new()
        $failure = $null
        $after = $null
        try {
            foreach ($step in $steps) { $results.Add((Invoke-LocalStep $Repo $directory $id $step)) }
            $after = Get-GitScope $Repo $context.Spec.base (Get-Head)
        } catch {
            $failure = $_.Exception.Message
        }
        $failed = @($results | Where-Object { $_.status -cne 'passed' }).Count -gt 0
        $status = if ($failure -or $failed -or -not $after -or $head -cne $after.Head) { 'failed' } else { 'passed' }
        $dirtyAfter = [object[]]@()
        if ($after) { $dirtyAfter = [object[]]@($after.Dirty | Where-Object { $_ }) }
        else { $dirtyAfter = [object[]]@('post-run scope unavailable') }
        $run = [ordered]@{ schemaVersion = 1; id = $id; taskId = $Task; base = $context.Spec.base; head = $head; specId = $context.Current.specId; specHash = $context.Spec.specHash; stage = $Stage; changedPaths = @($scope.Paths); dirtyBefore = @($scope.Dirty); dirtyAfter = $dirtyAfter; startedAt = $started.ToString('o'); endedAt = [DateTimeOffset]::UtcNow.ToString('o'); durationMs = [long]([DateTimeOffset]::UtcNow - $started).TotalMilliseconds; predecessor = $PreviousRun; difference = $Difference; question = $Question; stopWhen = $StopWhen; steps = @($results); status = $status; failure = $failure; implementationResult = '固定base/headの変更pathと実行結果を参照'; retainUntil = $started.AddDays(7).ToString('o') }
        $record = Finish-Run $directory $run
        $context.Current.candidateHead = $head
        $context.Current.nextAction = "run $id の結果を確認する"
        Save-CurrentFor $context
        Write-Output "run=$id status=$($run.status) head=$head dirty=$(@($scope.Dirty).Count) recordHash=$($record.Hash)"
        return
    }
    if ($Command -ceq 'adopt') {
        if (-not $RunId -or -not $Reason) { throw 'adoptには-RunIdと-Reasonが必要です。' }
        $record = Read-Record $directory 'runs' $RunId
        if ($record.content.specHash -cne $context.Spec.specHash) { throw '別仕様のrunは採用できません。' }
        if (@($context.Current.adoptedRuns | Where-Object id -eq $RunId).Count -gt 0) { throw '同じrunは採用済みです。' }
        if ($RetainUntil) { $expiry = [DateTimeOffset]::Parse($RetainUntil).ToUniversalTime() } else { $expiry = [DateTimeOffset]::UtcNow.AddDays(30) }
        if ($expiry -le [DateTimeOffset]::UtcNow) { throw 'retainUntilは将来の時刻が必要です。' }
        $context.Current.adoptedRuns = @($context.Current.adoptedRuns) + @([ordered]@{ id = $RunId; reason = $Reason; adoptedAt = [DateTimeOffset]::UtcNow.ToString('o') })
        $context.Current.references = @($context.Current.references) + @([ordered]@{ referenceId = [Guid]::NewGuid().ToString('N'); consumerId = $Task; purpose = 'adopted-run'; runId = $RunId; owner = $context.Current.owner; expiresAt = $expiry.ToString('o'); releasedAt = $null })
        Save-CurrentFor $context
        Write-Output "adopted $RunId; gate合格とは別です。"
        return
    }
    if ($Command -ceq 'reference') {
        if (-not $Owner) { throw 'referenceには-Ownerが必要です。' }
        if ($Action -ceq 'Add') {
            if (-not $RunId -or -not $ConsumerId -or -not $ExpiresAt) { throw '参照追加には-RunId、-ConsumerId、-ExpiresAtが必要です。' }
            [void](Read-Record $directory 'runs' $RunId)
            $expiry = [DateTimeOffset]::Parse($ExpiresAt).ToUniversalTime()
            if ($expiry -le [DateTimeOffset]::UtcNow) { throw 'expiresAtは将来の有限時刻が必要です。' }
            $ReferenceId = [Guid]::NewGuid().ToString('N')
            $context.Current.references = @($context.Current.references) + @([ordered]@{ referenceId = $ReferenceId; consumerId = $ConsumerId; purpose = $Purpose; runId = $RunId; owner = $Owner; expiresAt = $expiry.ToString('o'); releasedAt = $null })
        } else {
            if (-not $ReferenceId) { throw '解放・延長には-ReferenceIdが必要です。' }
            $matches = @($context.Current.references | Where-Object referenceId -eq $ReferenceId)
            if ($matches.Count -ne 1 -or $matches[0].owner -cne $Owner -or $matches[0].releasedAt) { throw '所有する有効参照が見つかりません。' }
            if ($Action -ceq 'Release') { $matches[0].releasedAt = [DateTimeOffset]::UtcNow.ToString('o') }
            else {
                if (-not $ExpiresAt) { throw '延長には-ExpiresAtが必要です。' }
                $expiry = [DateTimeOffset]::Parse($ExpiresAt).ToUniversalTime()
                if ($expiry -le [DateTimeOffset]::UtcNow) { throw 'expiresAtは将来の有限時刻が必要です。' }
                $matches[0].expiresAt = $expiry.ToString('o')
            }
        }
        Save-CurrentFor $context
        Write-Output "reference=$ReferenceId action=$Action"
        return
    }
    if ($Command -ceq 'assist') {
        if (-not $Reason) { throw 'assistには-Reasonが必要です。' }
        Write-Output ([ordered]@{ taskId = $Task; ready = $false; phase = 'B'; reason = $Reason; candidateHead = $context.Current.candidateHead; adoptedRuns = @($context.Current.adoptedRuns) } | ConvertTo-Json -Depth 10)
        return
    }
    if ($Command -ceq 'handoff') {
        $scope = Get-GitScope $Repo $context.Spec.base (Get-Head)
        if (@($scope.Dirty).Count -gt 0) { throw "dirtyな作業ツリーです: $($scope.Dirty -join ', ')。commitするかtrialとして実行してください。" }
        if ($To -ceq 'CBlind') {
            if (-not $context.Current.judgmentInputId) { throw '判定Cの固定入力がありません。CJudgmentを先に確定してください。' }
            $input = Read-Record $directory 'inputs' $context.Current.judgmentInputId
            if ($input.content.inputKind -cne 'judgment' -or $input.content.head -cne $scope.Head -or $input.content.specHash -cne $context.Spec.specHash) { throw 'CBlind入力のhead/仕様/種別が一致しません。' }
            $blindRun = Read-Record $directory 'runs' $input.content.runId
            if ($blindRun.hash -cne $input.content.runHash) { throw 'CBlind入力が参照するrunが変わりました。' }
            $required = Get-RequiredSteps $context.Spec $scope.Paths 'judgment'
            Assert-RunForGate $blindRun.content $context.Spec $scope.Head $required
            Assert-RunPayload $directory $blindRun.content
            Write-Output "ready=true input=$($input.id) hash=$($input.hash) head=$($scope.Head)"
            Write-Output "inputPath=$([IO.Path]::Combine($directory, 'inputs', "$($input.id).json"))"
            return
        }
        if (-not $RunId) { throw 'handoffには-RunIdが必要です。' }
        $record = Read-Record $directory 'runs' $RunId
        $run = $record.content
        $requiredStage = if ($To -ceq 'CJudgment') { 'judgment' } else { 'discovery' }
        # discoveryで広い必須集合まで実行済みなら、同一headの結果を判定Cへ再利用する。
        # 判定入力の種別は渡し先と照合済みstep集合から決め、run名だけで狭めない。
        if ($To -ceq 'CJudgment' -and @($context.Current.blockers).Count -gt 0) { throw '未解決blockerがあります。' }
        $steps = Get-RequiredSteps $context.Spec $scope.Paths $requiredStage
        Assert-RunForGate $run $context.Spec $scope.Head $steps
        Assert-RunPayload $directory $run
        $run | Add-Member -NotePropertyName recordHash -NotePropertyValue $record.hash -Force
        $diffResult = Invoke-Process 'git' @('-C', $Repo, 'diff', '--no-ext-diff', '--no-color', $context.Spec.base, $scope.Head, '--') $Repo 30
        if ($diffResult.ExitCode -ne 0) { throw '固定base/headの完全diffを取得できません。' }
        $inputBody = Select-BlindInput $context.Spec $run $requiredStage $scope.Paths $diffResult.Stdout
        $published = Publish-Handoff $directory $context.Current $inputBody $Task $To $RunId $scope.Head $context.Spec.specHash $requiredStage
        $input = $published.Input
        $receipt = $published.Receipt
        Write-Output "ready=true to=$To input=$($input.Id) hash=$($input.Hash) receipt=$($receipt.Id)"
        Write-Output "inputPath=$($input.Path)"
        return
    }
    if ($Command -ceq 'close') {
        if (-not $Reason) { throw 'closeには-Reasonが必要です。' }
        if ($Owner -cne $context.Current.owner) { throw 'task ownerを-Ownerで明示してください。' }
        if ($Outcome -ceq 'Completed' -and (-not $context.Current.judgmentInputId -or -not $CReview -or -not $CBlindReview)) { throw 'Completedには判定入力とC/C′結果の明示が必要です。' }
        if ($ReviewClosed -and $Outcome -ceq 'Completed' -and (-not $CReview -or -not $CBlindReview)) { throw 'レビュー終了の明示にはC/C′結果が必要です。' }
        $closed = Write-Closed $directory $context.Current.revision {
            param($latest)
            $closedAt = [DateTimeOffset]::UtcNow
            $closedRefs = @(Resolve-CloseReferences @($latest.references) $Task ([bool]$ReviewClosed) $closedAt)
            $retainUntil = $closedAt.AddDays(30)
            foreach ($ref in @($closedRefs | Where-Object { $_.expiresAt -and -not $_.releasedAt })) {
                $expiry = [DateTimeOffset]::Parse($ref.expiresAt)
                if ($expiry -gt $retainUntil) { $retainUntil = $expiry }
            }
            return [ordered]@{ kind = 'closed'; taskId = $Task; owner = $Owner; closedAt = $closedAt.ToString('o'); outcome = $Outcome; reason = $Reason; cReview = $CReview; cBlindReview = $CBlindReview; retainUntil = $retainUntil.ToString('o'); references = @($closedRefs) }
        }
        Write-Output "closed $Task at $($closed.closedAt); 証拠削除はH3で判定します。"
        return
    }
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
