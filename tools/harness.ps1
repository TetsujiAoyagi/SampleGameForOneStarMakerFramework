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

# 適用作業の入口。現行仕様と採用 run は Git 外の CURRENT から読み、過去の RESULT ファイルは見ない。
# init 以外は登録済み task だけを更新し、close 後の追記は拒否する。
function Get-Head {
    $value = & git -C $Repo rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Git HEADを取得できません。' }
    return $value.Trim()
}
function Get-Context {
    # 読むたびに凍結仕様の承認 hash を照合し直す。CURRENT の本文差し替えだけでは通さない。
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
                # 1件の破損で一覧全体を落とさない。壊れた task は理由を出して次へ進む。
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
        # 承認文面と完全な base SHA は入口の形。本文 hash の一致は Assert-ApprovedSpec が見る。
        # 登録済みディレクトリは上書きしない。欠落は restore、新規だけが init。
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
        # 仕様本文はここでは変えない。未解決・blocker・次作業だけを、読んだ revision と一致するとき更新する。
        if ($PSBoundParameters.ContainsKey('NextAction') -or $PSBoundParameters.ContainsKey('Unresolved') -or $PSBoundParameters.ContainsKey('Blockers')) {
            [void](Set-CurrentNotes -TaskDirectory $directory -Current $context.Current -ExpectedRevision $ExpectedRevision -HasNextAction ($PSBoundParameters.ContainsKey('NextAction')) -NextAction $NextAction -HasUnresolved ($PSBoundParameters.ContainsKey('Unresolved')) -Unresolved $Unresolved -HasBlockers ($PSBoundParameters.ContainsKey('Blockers')) -Blockers $Blockers)
            $context = Get-Context
        }
        $closed = Test-TaskClosed $directory
        Write-Output "task=$Task revision=$($context.Current.revision) owner=$($context.Current.owner) phase=$(if ($closed) {'closed'} else {$context.Current.phase})"
        Write-Output "A3=$($context.Spec.title) base=$($context.Spec.base) head=$($context.Current.candidateHead)"
        Write-Output "specification=$($context.Current.specId) recordPath=$([IO.Path]::Combine($directory, 'specifications', "$($context.Current.specId).json")) recordHash=$($context.Spec.specHash)"
        Write-Output "question=$($context.Spec.question)"
        Write-Output "summary=$($context.Spec.summary) outOfScope=$($context.Spec.outOfScope) minimum=$($context.Spec.minimum)"
        Write-Output "unresolved=$($context.Current.unresolved -join '; ') blockers=$($context.Current.blockers -join '; ')"
        Write-Output "next=$($context.Current.nextAction) input=$($context.Current.selectedInputId) receipt=$($context.Current.gateReceiptId)"
        foreach ($adopted in @($context.Current.adoptedRuns)) {
            $runRecord = Read-Record $directory 'runs' $adopted.id
            if ($runRecord.content.specHash -cne $context.Spec.specHash) { throw "採用runの凍結仕様が一致しません: $($adopted.id)" }
            Write-Output "adopted-run=$($adopted.id) reason=$($adopted.reason)"
            Write-Output "run-record=$($runRecord.id) recordPath=$([IO.Path]::Combine($directory, 'runs', "$($runRecord.id).json")) recordHash=$($runRecord.hash) startedAt=$($runRecord.content.startedAt) status=$($runRecord.content.status)"
            foreach ($step in @($runRecord.content.steps | Where-Object { $_.loadedPath })) {
                Write-Output "loaded-binary=$($step.loadedPath) sha256=$($step.loadedHash)"
            }
        }
        return
    }
    if (Test-TaskClosed $directory) { throw 'taskはclose済みです。新しいtaskをinitしてください。' }
    if ($Command -ceq 'run') {
        # 質問・停止条件・前回との差が空の再実行は残さない。初回だけ predecessor を省略できる。
        if (-not $Question -or -not $StopWhen -or -not $Difference) { throw 'runには前回との差・知りたい条件・停止条件を入力してください。初回は-Difference 初回です。' }
        if ($Difference -cne '初回' -and -not $PreviousRun) { throw '再実行には-PreviousRunが必要です。' }
        if ($PreviousRun) { [void](Read-Record $directory 'runs' $PreviousRun) }
        $head = Get-Head
        $scope = Get-GitScope $Repo $context.Spec.base $head
        $steps = Get-RequiredSteps $context.Spec $scope.Paths $Stage
        $id = [Guid]::NewGuid().ToString('N')
        $started = [DateTimeOffset]::UtcNow
        $runClock = [Diagnostics.Stopwatch]::StartNew()
        Start-Run $directory $id $head $started.ToString('o')
        $results = [Collections.Generic.List[object]]::new()
        $failure = $null
        $after = $null
        try {
            # step が例外でも進行中のまま残さない。失敗 record を書いてから戻る。
            foreach ($step in $steps) { $results.Add((Invoke-LocalStep $Repo $directory $id $step)) }
            $after = Get-GitScope $Repo $context.Spec.base (Get-Head)
        } catch {
            $failure = $_.Exception.Message
        }
        $runClock.Stop()
        $run = New-RunResult -Spec $context.Spec -Current $context.Current -Task $Task -Stage $Stage -Scope $scope -After $after -Started $started -ElapsedMs $runClock.ElapsedMilliseconds -Id $id -PreviousRun $PreviousRun -Difference $Difference -Question $Question -StopWhen $StopWhen -Results @($results) -Failure $failure
        $record = Finish-Run $directory $run
        [void](Set-RunCurrent $directory $context.Current $head $id)
        Write-Output "run=$id status=$($run.status) head=$head dirty=$(@($scope.Dirty).Count) recordHash=$($record.Hash)"
        return
    }
    if ($Command -ceq 'adopt') {
        [void](Add-AdoptedRun -TaskDirectory $directory -Current $context.Current -RunId $RunId -Reason $Reason -SpecHash $context.Spec.specHash -RetainUntil $RetainUntil)
        Write-Output "adopted $RunId; gate合格とは別です。"
        return
    }
    if ($Command -ceq 'reference') {
        $ReferenceId = Edit-Reference -TaskDirectory $directory -Current $context.Current -Action $Action -Owner $Owner -RunId $RunId -ConsumerId $ConsumerId -Purpose $Purpose -ExpiresAt $ExpiresAt -ReferenceId $ReferenceId
        Write-Output "reference=$ReferenceId action=$Action"
        return
    }
    if ($Command -ceq 'assist') {
        # 調査依頼は常に未完成。ready を立てて、判定入力の代わりに使わせない。
        if (-not $Reason) { throw 'assistには-Reasonが必要です。' }
        Write-Output ([ordered]@{ taskId = $Task; ready = $false; phase = 'B'; reason = $Reason; candidateHead = $context.Current.candidateHead; adoptedRuns = @($context.Current.adoptedRuns) } | ConvertTo-Json -Depth 10)
        return
    }
    if ($Command -ceq 'handoff') {
        $scope = Get-GitScope $Repo $context.Spec.base (Get-Head)
        if (@($scope.Dirty).Count -gt 0) { throw "dirtyな作業ツリーです: $($scope.Dirty -join ', ')。commitするかtrialとして実行してください。" }
        if ($To -ceq 'CBlind') {
            # 呼び出し側の run 指定では差し替えない。判定が残した入力 id だけを読む。
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
        Assert-HandoffCandidate $context.Current $To
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
        $closed = Write-Closed $directory $context.Current.revision {
            param($latest)
            New-CloseRecord -Current $latest -Task $Task -Owner $Owner -Outcome $Outcome -Reason $Reason -CReview $CReview -CBlindReview $CBlindReview -ReviewClosed ([bool]$ReviewClosed) -ClosedAt ([DateTimeOffset]::UtcNow)
        }
        Write-Output "closed $Task at $($closed.closedAt); 証拠削除はH3で判定します。"
        return
    }
} catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
