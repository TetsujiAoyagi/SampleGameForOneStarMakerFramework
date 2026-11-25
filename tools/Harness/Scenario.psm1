Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'UnityProcess.psm1')
Import-Module (Join-Path $PSScriptRoot 'ScenarioRun.psm1')
Import-Module (Join-Path $PSScriptRoot 'ScenarioCheckout.psm1')

# 実機の検証の driver（scenario）が import する唯一の入口。
# driver は case の列（名前、使う checkout、当てる patch、body）と、body の中の操作だけを書く。
# checkout の用意と事前確認、lease、patch の適用と復元、使用中の記録、process の台帳と終了の確認、
# record と証拠の置き場は、この module が決める。
#
# case ごとの順序（復元の規則）:
#   事前の状態を保存 → 使用中の記録を置く → patch → body → この case で始めた process の終了を確かめる
#   → project lock を消さずに調べる → 戻す前の状態を保存 → 戻す → 戻した後を確かめる → 使用中の記録を消す
# 終了を確かめられない時と lock が保持されている時は戻さない（隔離）。checkout は patch を当てたまま、
# 使用中の記録を残し、以後の case を走らせずに非 0 で終わる。
#
# body に渡す公開の関数は、この file の Export-ModuleMember にある物だけ。checkout の復元、patch、
# process の停止、削除、隔離の解除は公開しない（driver が順序の保証を迂回できないように）。

$script:RepoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script:DefaultSeams = $null
$script:Seams = $null

function New-ScenarioRealClock {
    $clock = [pscustomobject]@{}
    $clock | Add-Member -MemberType ScriptMethod -Name Now -Value { return [DateTimeOffset]::UtcNow }
    # 実時間の待ちは、窓のある Editor の ready や状態の待ちの間隔にだけ使う。offline test は偽の時計を渡す。
    $clock | Add-Member -MemberType ScriptMethod -Name Sleep -Value { param([int]$Milliseconds) [Threading.Thread]::Sleep($Milliseconds) }
    return $clock
}

function Get-ScenarioDefaultSeams {
    return @{
        RepoRoot = $script:RepoRoot
        UnityRoot = 'D:\UnityEditor'
        UnityExe = ''
        # 外部 root の基点。空なら run の root の drive（<drive>\ovr\<scenario>\<RunId>）。offline test だけが変える。
        ExternalBase = ''
        Clock = (New-ScenarioRealClock)
        ProcessQuery = { Get-ScenarioSystemProcesses }
        Starter = {
            param([string]$Executable, [string[]]$Arguments, [string]$WorkingDirectory, [hashtable]$Options)
            $environment = if ($Options.ContainsKey('Environment') -and $null -ne $Options.Environment) { $Options.Environment } else { @{} }
            Start-UnityProcess -Executable $Executable -Arguments $Arguments -WorkingDirectory $WorkingDirectory `
                -CreateWindow:([bool]$Options['CreateWindow']) -Environment $environment -RedirectOutput:([bool]$Options['RedirectOutput'])
        }
        Cli = {
            # その checkout の tools/unity-editor.cmd で、開いている Editor を操作する（別の接続の実装を作らない）。
            # CLI の process が期限を過ぎたら、その CLI だけを止める（Editor には触れない）。
            param([string]$CheckoutRoot, [string[]]$Arguments, [int]$TimeoutMs)
            $cmd = Join-Path $CheckoutRoot 'tools/unity-editor.cmd'
            $handle = Start-UnityProcess -Executable $cmd -Arguments $Arguments -WorkingDirectory $CheckoutRoot -RedirectOutput
            try {
                $wait = Wait-UnityProcess -Handle $handle -TimeoutMs $TimeoutMs
                if (-not $wait.Signaled) { Stop-UnityProcessTree -Handle $handle; return [pscustomobject]@{ ExitCode=$null; Output=''; TimedOut=$true } }
                return [pscustomobject]@{ ExitCode=$wait.ExitCode; Output=$handle.ReadOutput(); TimedOut=$false }
            } finally { Close-UnityProcess -Handle $handle }
        }
    }
}

function Set-ScenarioSeam {
    # offline test 専用。process の起動、Editor の CLI、時計、process の照会、repo の root を差し替える。
    # driver は呼ばない（driver の走査が拒む）。
    param([Parameter(Mandatory)] [hashtable]$Values)
    if ($null -eq $script:Seams) { $script:Seams = Get-ScenarioDefaultSeams }
    foreach ($key in $Values.Keys) { $script:Seams[$key] = $Values[$key] }
}

function Reset-ScenarioSeam {
    $script:Seams = Get-ScenarioDefaultSeams
}

function Get-ScenarioSeams {
    if ($null -eq $script:Seams) { $script:Seams = Get-ScenarioDefaultSeams }
    return $script:Seams
}

function New-Scenario {
    # scenario の宣言。Checkout は case が使う checkout の名前（X、Y など）。
    # RecordCheckout・RecordDirectory を省けば、run の root は driver のある checkout の
    # TestResults/scenario/<名前>/<RunId>。渡せば <その checkout>/<RecordDirectory>/<RunId>。
    param([Parameter(Mandatory)] [string]$Name, [AllowEmptyCollection()] [string[]]$Checkout = @(), [string]$RecordCheckout = '', [string]$RecordDirectory = '')
    Assert-ScenarioName $Name 'scenario の名前'
    $names = [Collections.Generic.List[string]]::new()
    foreach ($item in $Checkout) {
        Assert-ScenarioName $item 'checkout の名前'
        if ($names -ccontains $item) { throw "checkout の名前が重複しています: $item" }
        $names.Add($item)
    }
    if ($RecordCheckout -and $names -cnotcontains $RecordCheckout) { throw "record を置く checkout が宣言にありません: $RecordCheckout" }
    if ($RecordDirectory) {
        foreach ($segment in $RecordDirectory.Split([char[]]@('/', '\'))) { Assert-ScenarioName $segment 'record の directory の成分' }
    }
    return [pscustomobject]@{ Name=$Name; Checkouts=@($names.ToArray()); Cases=[Collections.Generic.List[object]]::new(); RecordCheckout=$RecordCheckout; RecordDirectory=$RecordDirectory }
}

function Add-ScenarioCase {
    # case の登録。Checkout を省いた case は checkout を使わない（複写、片付けなど）。
    # Patches は checkout の root からの相対 path。Body は ($Context) を受け、値を返す（record の body 欄）。
    # After は ($Context, $Result) を受け、復元の後に呼ばれる（scenario が自分の形の record を書く所）。
    param([Parameter(Mandatory)] [psobject]$Scenario, [Parameter(Mandatory)] [string]$Name, [string]$Checkout = '',
        [AllowEmptyCollection()] [string[]]$Patches = @(), [scriptblock]$Body = $null, [scriptblock]$After = $null)
    Assert-ScenarioName $Name 'case の名前'
    foreach ($case in $Scenario.Cases) { if ($case.Name -ceq $Name) { throw "case の名前が重複しています: $Name" } }
    if ($Checkout -and $Scenario.Checkouts -cnotcontains $Checkout) { throw "case $Name が宣言していない checkout を指しています: $Checkout" }
    if (-not $Checkout -and $Patches.Count -gt 0) { throw "checkout を使わない case に patch は当てられません: $Name" }
    foreach ($patch in $Patches) {
        if ([IO.Path]::IsPathRooted($patch) -or @($patch.Split([char[]]@('/', '\')) | Where-Object { $_ -eq '..' }).Count -gt 0) { throw "patch は checkout の中の相対 path で指します: $patch" }
    }
    $Scenario.Cases.Add([pscustomobject]@{ Name=$Name; Checkout=$Checkout; Patches=@($Patches); Body=$Body; After=$After })
}

function Write-ScenarioError([string]$Message) {
    [Console]::Error.WriteLine("[scenario] $Message")
}

function Add-ScenarioEvent {
    param([psobject]$State, [string]$Event, [AllowNull()] [psobject]$Context, [Collections.IDictionary]$Fields = @{})
    $record = [ordered]@{ event=$Event; case=$null; attempt=$null }
    if ($null -ne $Context) { $record.case = $Context.Case; $record.attempt = $Context.Attempt.Number }
    foreach ($key in $Fields.Keys) { $record[$key] = $Fields[$key] }
    $record['utc'] = [DateTime]::UtcNow.ToString('o')
    Add-ScenarioRecord -Run $State.Run -Record $record
}

function Resolve-ScenarioCommit {
    param([string]$RepoRoot, [string]$Commit)
    return (Invoke-ScenarioGit $RepoRoot @('rev-parse', '--verify', '-q', "$Commit^{commit}")).Stdout.Trim()
}

function Invoke-Scenario {
    # scenario を 1 回の driver の呼び出しとして走らせる。返す object の ExitCode を driver の終了コードにする。
    # exit 0 は「基盤の操作が全て通り、選んだ case が最後まで走った」ことだけを示し、製品の合格を意味しない。
    param(
        [Parameter(Mandatory)] [psobject]$Scenario,
        [Parameter(Mandatory)] [string]$RunId,
        [string]$Commit = '',
        [AllowEmptyCollection()] [string[]]$Only = @(),
        [switch]$DryRun,
        [hashtable]$CheckoutRoots = @{},
        [string]$CheckoutPlace = ''
    )
    $state = $null
    try {
        Assert-ScenarioName $RunId 'RunId'
        $only = @($Only | Where-Object { $_ })
        $unknown = @($only | Where-Object { $name = $_; -not @($Scenario.Cases | Where-Object { $_.Name -ceq $name }) })
        if ($unknown.Count -gt 0) { Write-ScenarioError "-Only に case の列に無い名前があります: $($unknown -join ', ')"; return [pscustomobject]@{ ExitCode=1; RunRoot=$null } }
        $selected = @($Scenario.Cases | Where-Object { $only.Count -eq 0 -or $only -ccontains $_.Name })
        if ($selected.Count -eq 0) { Write-ScenarioError '選ばれた case が 0 件です'; return [pscustomobject]@{ ExitCode=1; RunRoot=$null } }
        foreach ($key in $CheckoutRoots.Keys) { if ($Scenario.Checkouts -cnotcontains [string]$key) { Write-ScenarioError "宣言していない checkout の root が渡されました: $key"; return [pscustomobject]@{ ExitCode=1; RunRoot=$null } } }
        $state = New-ScenarioInvocationState -Scenario $Scenario -RunId $RunId -DryRun ([bool]$DryRun)
        [void](Open-ScenarioInvocation -State $state -Commit $Commit -Continue ($only.Count -gt 0) -Only $only -CheckoutRoots $CheckoutRoots -CheckoutPlace $CheckoutPlace)
        if ($null -eq $state.Run) { return [pscustomobject]@{ ExitCode=1; RunRoot=$null } }
        foreach ($case in $selected) {
            [Console]::Out.WriteLine("[$($Scenario.Name)] $($case.Name)")
            $outcome = Invoke-ScenarioCaseInternal -State $state -Case $case
            if ($outcome.Stop) { $state.Failed = $true; break }
        }
        if (-not (Complete-ScenarioInvocationProcesses $state)) { $state.Failed = $true }
        return [pscustomobject]@{ ExitCode=$(if ($state.Failed) { 1 } else { 0 }); RunRoot=$state.Run.Root }
    } catch {
        Write-ScenarioError $_.Exception.Message
        return [pscustomobject]@{ ExitCode=1; RunRoot=$(if ($null -ne $state -and $null -ne $state.Run) { $state.Run.Root } else { $null }) }
    } finally {
        if ($null -ne $state) { Close-ScenarioInvocation $state }
    }
}

function Close-ScenarioInvocation {
    param([psobject]$State)
    foreach ($entry in $State.Ledger) { try { Close-UnityProcess -Handle $entry.Handle } catch { } }
    foreach ($checkout in $State.Checkouts.Values) { if ($null -ne $checkout.Lease) { $checkout.Lease.Dispose(); $checkout.Lease = $null } }
    if ($null -ne $State.Run) { Close-ScenarioRun -Run $State.Run }
}

function New-ScenarioInvocationState {
    param([psobject]$Scenario, [string]$RunId, [bool]$DryRun)
    $seams = Get-ScenarioSeams
    return [pscustomobject]@{
        Scenario=$Scenario; RunId=$RunId; DryRun=$DryRun; Repo=$null; Seams=$seams; Commit=$null; Run=$null; Failed=$false
        Checkouts=[ordered]@{}; Ledger=[Collections.Generic.List[object]]::new()
    }
}

function Open-ScenarioInvocation {
    # 全ての checkout の事前確認と lease の取得を終えてから、最初の書き込み（checkout の作成・HEAD の切替、
    # run の root の作成）を行う。事前確認が 1 つでも落ちれば、何も書かずに Run=$null のまま返す。
    # 取った lease は State に載せ、例外の時も呼び出し側の Close-ScenarioInvocation が離す。
    param([psobject]$State, [string]$Commit, [bool]$Continue, [AllowEmptyCollection()] [string[]]$Only = @(), [hashtable]$CheckoutRoots, [string]$CheckoutPlace)
    $state = $State
    $Scenario = $state.Scenario
    $RunId = $state.RunId
    $DryRun = $state.DryRun
    $seams = $state.Seams
    $repo = Get-ScenarioRepository $seams.RepoRoot
    $state.Repo = $repo
    $place = if ($CheckoutPlace) { [IO.Path]::GetFullPath($CheckoutPlace).TrimEnd('\', '/') } else { $repo.Place }
    $anyManaged = @($Scenario.Checkouts | Where-Object { -not $CheckoutRoots.ContainsKey($_) }).Count -gt 0
    if ($Commit) { $state.Commit = Resolve-ScenarioCommit $seams.RepoRoot $Commit }
    elseif ($anyManaged) { $state.Commit = (Invoke-ScenarioGit $seams.RepoRoot @('rev-parse', 'HEAD')).Stdout.Trim() }

    foreach ($name in $Scenario.Checkouts) {
        $explicit = $CheckoutRoots.ContainsKey($name)
        $root = if ($explicit) { [IO.Path]::GetFullPath([string]$CheckoutRoots[$name]).TrimEnd('\', '/') } else {
            $leaf = "$($Scenario.Name)-$name"
            Assert-ScenarioName $leaf 'checkout の directory の名前' -MaxLength 64
            Join-Path $place $leaf
        }
        $state.Checkouts[$name] = [pscustomobject]@{ Name=$name; Root=$root; Explicit=$explicit; Lease=$null; Info=$null; Action='use'; Head=$null }
    }
    $runRoot = Get-ScenarioRunRoot $state
    $continuing = $Continue -and [IO.Directory]::Exists($runRoot)
    $identity = @{ scenario=$Scenario.Name; runId=$RunId }
    $problems = [Collections.Generic.List[string]]::new()
    $seenRoots = @{}
    foreach ($checkout in $state.Checkouts.Values) {
        $rootKey = $checkout.Root.ToLowerInvariant()
        if ($seenRoots.ContainsKey($rootKey)) { $problems.Add("checkout $($seenRoots[$rootKey]) と $($checkout.Name) が同じ物です: $($checkout.Root)"); continue }
        $seenRoots[$rootKey] = $checkout.Name
        try { $checkout.Lease = Enter-ScenarioCheckoutLease -CommonDir $repo.CommonDir -Path $checkout.Root }
        catch { $problems.Add($_.Exception.Message); continue }
        $checkout.Info = Get-ScenarioCheckoutInfo $checkout.Root
        if ($checkout.Explicit) {
            foreach ($p in (Get-ScenarioCheckoutProblems -Info $checkout.Info -CommonDir $repo.CommonDir -BudgetRoot $seams.RepoRoot -ExpectedCommit ([string]$state.Commit) -Identity $identity)) { $problems.Add($p) }
            $checkout.Head = $checkout.Info.Head
        } elseif ($checkout.Info.Exists) {
            $allowMove = -not $continuing
            foreach ($p in (Get-ScenarioCheckoutProblems -Info $checkout.Info -CommonDir $repo.CommonDir -BudgetRoot $seams.RepoRoot -ExpectedCommit ([string]$state.Commit) -AllowHeadMove $allowMove -Identity $identity)) { $problems.Add($p) }
            if ($checkout.Info.Head -cne $state.Commit) {
                # 予約のある checkout の HEAD は切り替えない（管理する file が別の commit の前提で残っている）。
                if ($null -ne $checkout.Info.Reservation) { $problems.Add("予約のある checkout の HEAD は切り替えません: $($checkout.Root)") }
                $checkout.Action = 'switch'
            }
            $checkout.Head = $state.Commit
        } else {
            if ($continuing) { $problems.Add("続きの実行で checkout がありません: $($checkout.Root)") }
            foreach ($p in (Get-ScenarioCheckoutCreationProblems -Path $checkout.Root -Place $place -BudgetRoot $seams.RepoRoot)) { $problems.Add($p) }
            $checkout.Action = 'create'
            $checkout.Head = $state.Commit
        }
    }
    # 2 つの checkout が同じ物（同じ worktree の git dir、または同じ path）であってはいけない。
    $seen = @{}
    foreach ($checkout in $state.Checkouts.Values) {
        $key = if ($null -ne $checkout.Info -and $null -ne $checkout.Info.GitDir) { 'git:' + $checkout.Info.GitDir.ToLowerInvariant() } else { 'path:' + $checkout.Root.ToLowerInvariant() }
        if ($seen.ContainsKey($key)) { $problems.Add("checkout $($seen[$key]) と $($checkout.Name) が同じ物です: $($checkout.Root)") } else { $seen[$key] = $checkout.Name }
    }
    if ($problems.Count -gt 0) {
        foreach ($p in $problems) { Write-ScenarioError $p }
        return $state
    }

    # ここから書き込み。checkout を先に用意する（run の root が checkout の中にあることがあるため）。
    foreach ($checkout in $state.Checkouts.Values) {
        if ($DryRun -and $checkout.Action -ne 'use') { throw "-DryRun は checkout を作らず、HEAD も切り替えません。先に本番か scenario-checkout.ps1 で用意します: $($checkout.Root)" }
        if ($checkout.Action -eq 'create') { $checkout.Info = New-ScenarioCheckout -RepoRoot $seams.RepoRoot -Path $checkout.Root -Commit $state.Commit -CommonDir $repo.CommonDir }
        elseif ($checkout.Action -eq 'switch') { Switch-ScenarioCheckoutCommit -Root $checkout.Root -Commit $state.Commit; $checkout.Info = Get-ScenarioCheckoutInfo $checkout.Root }
    }
    $map = [ordered]@{}
    foreach ($checkout in $state.Checkouts.Values) { $map[$checkout.Name] = [ordered]@{ root=$checkout.Root; head=$checkout.Head } }
    $mode = if ($DryRun) { 'dry-run' } else { 'run' }
    $state.Run = Open-ScenarioRun -Root $runRoot -Scenario $Scenario.Name -RunId $RunId -Mode $mode -Commit ([string]$state.Commit) -Checkouts $map -Continue $Continue -ExternalDrive ([string]$seams.ExternalBase)
    if ($state.Run.Continued) {
        foreach ($checkout in $state.Checkouts.Values) { Assert-ScenarioManagedFilesUnchanged -RunRoot $state.Run.Root -Checkout $checkout.Name -CheckoutRoot $checkout.Root }
    }
    $checkoutRecord = [ordered]@{}
    foreach ($checkout in $state.Checkouts.Values) {
        $version = try { Get-UnityRequestedVersion -ProjectPath (Join-Path $checkout.Root 'unity') } catch { 'unknown' }
        $checkoutRecord[$checkout.Name] = [ordered]@{ root=$checkout.Root; head=$checkout.Info.Head; unityVersion=$version; action=$checkout.Action }
    }
    Add-ScenarioEvent $state 'run-start' $null ([ordered]@{ scenario=$Scenario.Name; runId=$RunId; mode=$mode; commit=$state.Commit; continued=$state.Run.Continued; only=@($Only); checkouts=$checkoutRecord })
    return $state
}

function Get-ScenarioRunRoot {
    param([psobject]$State)
    $scenario = $State.Scenario
    if ($scenario.RecordCheckout) {
        return [IO.Path]::GetFullPath([IO.Path]::Combine($State.Checkouts[$scenario.RecordCheckout].Root, $scenario.RecordDirectory, $State.RunId))
    }
    $directory = if ($scenario.RecordDirectory) { $scenario.RecordDirectory } else { "TestResults/scenario/$($scenario.Name)" }
    return [IO.Path]::GetFullPath([IO.Path]::Combine($State.Seams.RepoRoot, $directory, $State.RunId))
}

function New-ScenarioContext {
    param([psobject]$State, [psobject]$Case, [psobject]$Attempt)
    $checkout = if ($Case.Checkout) { $State.Checkouts[$Case.Checkout] } else { $null }
    $roots = [ordered]@{}
    foreach ($item in $State.Checkouts.Values) { $roots[$item.Name] = $item.Root }
    return [pscustomobject]@{
        Case=$Case.Name; Attempt=$Attempt; RunId=$State.RunId; Scenario=$State.Scenario.Name; DryRun=$State.DryRun
        Checkout=$Case.Checkout; CheckoutRoot=$(if ($null -ne $checkout) { $checkout.Root } else { $null }); Roots=$roots; RunRoot=$State.Run.Root
        State=$State; Failures=[Collections.Generic.List[string]]::new(); Processes=[Collections.Generic.List[object]]::new()
    }
}

function Add-ScenarioFailure {
    # 基盤の操作の失敗を case に残す。case は body の後に失敗として終わり、以後の case は走らない。
    param([psobject]$Context, [string]$Operation, [string]$Message)
    $Context.Failures.Add("${Operation}: $Message")
}

function Save-ScenarioSnapshot {
    param([psobject]$State, [psobject]$Context, [string]$Prefix, [psobject]$Snapshot)
    [void](Save-ScenarioBaseFile -Run $State.Run -Attempt $Context.Attempt -Name "$Prefix.status.txt" -Content $Snapshot.Porcelain)
    [void](Save-ScenarioBaseFile -Run $State.Run -Attempt $Context.Attempt -Name "$Prefix.head.txt" -Content $Snapshot.Head)
    [void](Save-ScenarioBaseFile -Run $State.Run -Attempt $Context.Attempt -Name "$Prefix.index.diff" -Content $Snapshot.IndexDiff)
    [void](Save-ScenarioBaseFile -Run $State.Run -Attempt $Context.Attempt -Name "$Prefix.work.diff" -Content $Snapshot.WorkDiff)
}

function Invoke-ScenarioCaseInternal {
    param([psobject]$State, [psobject]$Case)
    $attempt = New-ScenarioAttempt -Run $State.Run -Case $Case.Name
    $context = New-ScenarioContext $State $Case $attempt
    $baseFailure = $null
    $bodyValue = $null
    $result = [ordered]@{ Case=$Case.Name; Attempt=$attempt.Number; Checkout=$Case.Checkout; BodyValue=$null; GitStatusBefore=@(); GitStatusAfter=@(); GitStatusSame=$null; HeadSame=$null; IndexClean=$null; Restored=$false; Quarantined=$false; Failures=@() }
    $checkout = if ($Case.Checkout) { $State.Checkouts[$Case.Checkout] } else { $null }
    $before = $null
    if ($null -ne $checkout) {
        $before = Get-ScenarioCheckoutSnapshot $checkout.Root
        Save-ScenarioSnapshot $State $context 'before' $before
        $result.GitStatusBefore = @($before.PorcelainLines)
        Add-ScenarioEvent $State 'case-start' $context ([ordered]@{ checkout=$Case.Checkout; head=$before.Head; patches=@($Case.Patches) })
        if ($State.Commit -and $before.Head -cne $State.Commit) {
            # 起動の直前の HEAD が対象の commit と違う。何も変えていないので戻す物は無い。
            $baseFailure = 'head'
            Add-ScenarioFailure $context 'head' "case の前の HEAD $($before.Head) が対象の commit $($State.Commit) と違います"
        } else {
            Set-ScenarioCheckoutInUse -Info $checkout.Info -Record ([ordered]@{ scenario=$State.Scenario.Name; runId=$State.RunId; runRoot=$State.Run.Root; case=$Case.Name; attempt=$attempt.Number; utc=[DateTime]::UtcNow.ToString('o') })
            Add-ScenarioEvent $State 'in-use' $context ([ordered]@{ checkout=$Case.Checkout; state='placed' })
            try { Invoke-ScenarioCheckoutPatch -Root $checkout.Root -PatchPaths @($Case.Patches | ForEach-Object { Join-Path $checkout.Root $_ }) }
            catch { $baseFailure = 'patch'; Add-ScenarioFailure $context 'patch' $_.Exception.Message }
        }
    } else {
        Add-ScenarioEvent $State 'case-start' $context ([ordered]@{ checkout=$null; head=$null; patches=@() })
    }

    if ($null -eq $baseFailure -and $null -ne $Case.Body) {
        try { $bodyValue = & $Case.Body $context }
        catch { $baseFailure = 'body'; Add-ScenarioFailure $context 'body' $_.Exception.Message }
    }
    $result.BodyValue = $bodyValue

    # この case で基盤が始めた process の終了を確かめる（Editor が開いたままなら終了を頼む）。
    $unconfirmed = @(Confirm-ScenarioCaseProcesses $State $context)
    $quarantine = $unconfirmed.Count -gt 0
    if ($null -ne $checkout -and $null -ne $before -and $baseFailure -cne 'head') {
        $lock = Get-UnityProjectLockState (Join-Path $checkout.Root 'unity')
        if ($lock.Held) { $quarantine = $true }
        if ($quarantine) {
            foreach ($entry in $unconfirmed) {
                Add-ScenarioEvent $State 'quarantine' $context ([ordered]@{ checkout=$Case.Checkout; pid=$entry.Handle.Id; startedUtc=$entry.Handle.StartedAt; kind=$entry.Kind; restored=$false; reason='termination-unconfirmed' })
            }
            if ($lock.Held) { Add-ScenarioEvent $State 'quarantine' $context ([ordered]@{ checkout=$Case.Checkout; pid=$null; startedUtc=$null; kind='project-lock'; restored=$false; reason='project-lock-held' }) }
            $result.Quarantined = $true
            if (-not $baseFailure) { $baseFailure = 'termination' }
            Add-ScenarioFailure $context 'termination' '終了を確かめられないか project lock が保持されているので、checkout を戻さずに止めます（隔離）'
        } else {
            $saveFailed = $false
            try { Save-ScenarioSnapshot $State $context 'restore-before' (Get-ScenarioCheckoutSnapshot $checkout.Root) }
            catch { $saveFailed = $true; Add-ScenarioFailure $context 'evidence' $_.Exception.Message }
            try { Restore-ScenarioCheckout -Root $checkout.Root }
            catch { Add-ScenarioFailure $context 'restore' $_.Exception.Message }
            $after = Get-ScenarioCheckoutSnapshot $checkout.Root
            try { Save-ScenarioSnapshot $State $context 'after' $after }
            catch { $saveFailed = $true; Add-ScenarioFailure $context 'evidence' $_.Exception.Message }
            $result.GitStatusAfter = @($after.PorcelainLines)
            $result.GitStatusSame = ($before.Porcelain -ceq $after.Porcelain)
            $result.HeadSame = ($before.Head -ceq $after.Head)
            $result.IndexClean = $after.IndexClean
            Add-ScenarioEvent $State 'restore' $context ([ordered]@{ gitStatusSame=$result.GitStatusSame; headSame=$result.HeadSame; indexClean=$result.IndexClean })
            if ($result.GitStatusSame -and $result.HeadSame -and $result.IndexClean) {
                Clear-ScenarioCheckoutInUse -Info $checkout.Info
                Add-ScenarioEvent $State 'in-use' $context ([ordered]@{ checkout=$Case.Checkout; state='cleared' })
                $result.Restored = $true
            } elseif (-not $baseFailure) { $baseFailure = 'restore' }
            if ($saveFailed -and -not $baseFailure) { $baseFailure = 'evidence' }
            if ($State.Commit -and $after.Head -cne $State.Commit -and -not $baseFailure) {
                $baseFailure = 'head'; Add-ScenarioFailure $context 'head' "case の後の HEAD $($after.Head) が対象の commit と違います"
            }
        }
    } elseif ($quarantine) {
        foreach ($entry in $unconfirmed) {
            Add-ScenarioEvent $State 'quarantine' $context ([ordered]@{ checkout=$null; pid=$entry.Handle.Id; startedUtc=$entry.Handle.StartedAt; kind=$entry.Kind; restored=$false; reason='termination-unconfirmed' })
        }
        if (-not $baseFailure) { $baseFailure = 'termination' }
    }
    if ($context.Failures.Count -gt 0 -and -not $baseFailure) { $baseFailure = ($context.Failures[0] -split ':', 2)[0] }
    $result.Failures = @($context.Failures.ToArray())

    if (-not $result.Quarantined -and $null -ne $Case.After) {
        try { & $Case.After $context ([pscustomobject]$result) }
        catch { if (-not $baseFailure) { $baseFailure = 'after' }; $context.Failures.Add("after: $($_.Exception.Message)") }
    }
    $headAfter = if ($null -ne $checkout) { (Get-ScenarioCheckoutInfo $checkout.Root).Head } else { $null }
    Add-ScenarioEvent $State 'case-end' $context ([ordered]@{ head=$headAfter; base=$(if ($baseFailure) { $baseFailure } else { 'ok' }); body=$bodyValue; failure=@($context.Failures.ToArray()) })
    foreach ($message in $context.Failures) { Write-ScenarioError "$($Case.Name): $message" }
    return [pscustomobject]@{ Stop=[bool]$baseFailure; Quarantined=$result.Quarantined }
}

function Register-ScenarioProcess {
    # 基盤が始めた process を台帳に載せ、checkout を使う case なら使用中の記録に PID と開始時刻を足す。
    param([psobject]$Context, [psobject]$Handle, [string]$Kind, [int]$TimeoutMs = 0, [bool]$KillOnTimeout = $false, [scriptblock]$Finalize = $null)
    $entry = [pscustomobject]@{ Handle=$Handle; Kind=$Kind; Case=$Context.Case; Attempt=$Context.Attempt.Number; Checkout=$Context.Checkout; Confirmed=$false; Signaled=$false; ExitCode=$null; Forced=$false; Recorded=$false; TimeoutMs=$TimeoutMs; KillOnTimeout=$KillOnTimeout; Finalize=$Finalize; Session=$null }
    $Context.State.Ledger.Add($entry)
    $Context.Processes.Add($entry)
    if ($Context.Checkout) {
        Add-ScenarioCheckoutInUseProcess -Info $Context.State.Checkouts[$Context.Checkout].Info -ProcessId $Handle.Id -StartedAt $Handle.StartedAt -Kind $Kind
    }
    return $entry
}

function Write-ScenarioProcessExit {
    param([psobject]$State, [psobject]$Context, [psobject]$Entry)
    if ($Entry.Recorded) { return }
    $Entry.Recorded = $true
    Add-ScenarioEvent $State 'process-exit' $Context ([ordered]@{ pid=$Entry.Handle.Id; kind=$Entry.Kind; signaled=$Entry.Signaled; exitCode=$Entry.ExitCode; forced=$Entry.Forced })
}

function Wait-ScenarioLedgerEntry {
    # 直接起動した process（batch、補助、Editor）の終了を、保持した handle の signal と終了コードで確かめる。
    # 期限の後に止めるのは KillOnTimeout を明示した時だけ。止めた終了は forced と記録する。
    param([psobject]$Entry, [int]$TimeoutMs)
    $wait = Wait-UnityProcess -Handle $Entry.Handle -TimeoutMs $TimeoutMs
    if (-not $wait.Signaled -and $Entry.KillOnTimeout) {
        Stop-UnityProcessTree -Handle $Entry.Handle
        $Entry.Forced = $true
        $wait = Wait-UnityProcess -Handle $Entry.Handle -TimeoutMs $TimeoutMs
    }
    $Entry.Signaled = $wait.Signaled
    $Entry.ExitCode = $wait.ExitCode
    $Entry.Confirmed = $wait.Signaled -and ($wait.ExitCode -is [int])
    return $Entry.Confirmed
}

function Confirm-ScenarioCaseProcesses {
    # case の終わりに、その case で始めた process（補助の process を除く）の終了を確かめる。
    # 確かめられなかった entry を返す。
    param([psobject]$State, [psobject]$Context)
    $unconfirmed = [Collections.Generic.List[object]]::new()
    foreach ($entry in @($Context.Processes)) {
        if ($entry.Kind -ceq 'helper') { continue }
        if (-not $entry.Confirmed) {
            try {
                if ($null -ne $entry.Finalize) { [void](& $entry.Finalize $Context $entry) }
                elseif ($entry.Kind -cne 'test-runner') { [void](Wait-ScenarioLedgerEntry $entry $entry.TimeoutMs) }
            } catch { Add-ScenarioFailure $Context 'termination' $_.Exception.Message }
        }
        if ($entry.Kind -cne 'test-runner') { Write-ScenarioProcessExit $State $Context $entry }
        if (-not $entry.Confirmed) { $unconfirmed.Add($entry) }
    }
    return @($unconfirmed.ToArray())
}

function Complete-ScenarioInvocationProcesses {
    # driver の呼び出しの終わりに、基盤が始めた補助の process の終了を確かめる。
    param([psobject]$State)
    $ok = $true
    foreach ($entry in @($State.Ledger | Where-Object { $_.Kind -ceq 'helper' })) {
        if (-not $entry.Confirmed) { [void](Wait-ScenarioLedgerEntry $entry $entry.TimeoutMs) }
        if ($null -ne $State.Run) {
            $record = [ordered]@{ event='process-exit'; case=$entry.Case; attempt=$entry.Attempt; pid=$entry.Handle.Id; kind=$entry.Kind; signaled=$entry.Signaled; exitCode=$entry.ExitCode; forced=$entry.Forced }
            Add-ScenarioRecord -Run $State.Run -Record $record
        }
        if (-not $entry.Confirmed) { $ok = $false; Write-ScenarioError "補助の process（PID $($entry.Handle.Id)）の終了を確かめられません" }
    }
    return $ok
}

# --- case の body が使う関数 -------------------------------------------------

function Write-ScenarioRecord {
    # scenario が自分の形の行を driver.jsonl に足す。基盤は utc だけを足し、event を足さない
    # （基盤の行と scenario の行を event の有無で分けるため）。
    param([Parameter(Mandatory)] [psobject]$Context, [Parameter(Mandatory)] [Collections.IDictionary]$Record)
    Add-ScenarioRecord -Run $Context.State.Run -Record $Record
}

function Save-ScenarioArtifact {
    # scenario が保存する証拠。evidence/<case>/<試行>/<名前> に置き、同じ試行で同じ名前は拒む。
    param([Parameter(Mandatory)] [psobject]$Context, [Parameter(Mandatory)] [string]$Name, [AllowNull()] [object]$Content = $null, [string]$Path = '')
    return Save-ScenarioEvidenceFile -Run $Context.State.Run -Attempt $Context.Attempt -Name $Name -Content $Content -SourcePath $Path
}

function Get-ScenarioDirectory {
    # run の外部 root の下の用途の directory（cache や fixture）。-DryRun では作らずに path だけを返す。
    param([Parameter(Mandatory)] [psobject]$Context, [Parameter(Mandatory)] [string]$Purpose)
    return Get-ScenarioExternalDirectory -Run $Context.State.Run -Purpose $Purpose -Create (-not $Context.DryRun)
}

function Remove-ScenarioDirectories {
    # この run の外部 root を片付ける（同じ run では作り直さない）。消した時は $true。
    param([Parameter(Mandatory)] [psobject]$Context)
    if ($Context.DryRun) { return $false }
    return Remove-ScenarioExternalRoot -Run $Context.State.Run
}

function Set-ScenarioFile {
    # checkout の中の ignore される「管理する file」を書く。書く前にその checkout に run の予約を置く。
    # 寿命は run で、戻すのは Reset-ScenarioFiles を呼んだ時だけ。-DryRun では書かない。
    param([Parameter(Mandatory)] [psobject]$Context, [Parameter(Mandatory)] [string]$Checkout, [Parameter(Mandatory)] [string]$Path, [AllowNull()] [object]$Content)
    $state = $Context.State
    if (-not $state.Checkouts.Contains($Checkout)) { throw "宣言していない checkout です: $Checkout" }
    $target = $state.Checkouts[$Checkout]
    $full = [IO.Path]::GetFullPath((Join-Path $target.Root $Path))
    if ($Context.DryRun) { return $full }
    if (-not (Test-ScenarioPathIgnored -Root $target.Root -RelativePath $Path)) { throw "管理する file は ignore される file に限ります: $Path" }
    Set-ScenarioCheckoutReservation -Info $target.Info -Scenario $state.Scenario.Name -RunId $state.RunId -RunRoot $state.Run.Root -Checkout $Checkout
    $bytes = if ($Content -is [byte[]]) { $Content } else { [Text.UTF8Encoding]::new($false).GetBytes([string]$Content) }
    $written = Write-ScenarioManagedFile -Run $state.Run -Checkout $Checkout -CheckoutRoot $target.Root -RelativePath $Path -Bytes $bytes
    Add-ScenarioEvent $state 'managed-file' $Context ([ordered]@{ checkout=$Checkout; path=$Path; beforeSha256=$written.BeforeSha256; writtenSha256=$written.WrittenSha256 })
    return $written.Path
}

function Reset-ScenarioFiles {
    # この run がその checkout に書いた管理する file を、書く前の状態へ戻し、予約を外す。
    # 何も書いていなければ何もしない。今の bytes が基盤の書いた物と違えば、何も変えずに止まる。
    param([Parameter(Mandatory)] [psobject]$Context, [Parameter(Mandatory)] [string]$Checkout)
    $state = $Context.State
    if (-not $state.Checkouts.Contains($Checkout)) { throw "宣言していない checkout です: $Checkout" }
    if ($Context.DryRun) { return @() }
    $target = $state.Checkouts[$Checkout]
    $info = Get-ScenarioCheckoutInfo $target.Root
    if (@(Get-ScenarioManagedFiles -RunRoot $state.Run.Root -Checkout $Checkout).Count -eq 0) {
        if ($null -ne $info.Reservation -and [string]$info.Reservation.runId -ceq $state.RunId -and [string]$info.Reservation.scenario -ceq $state.Scenario.Name) { Clear-ScenarioCheckoutReservation -Info $info }
        return @()
    }
    $restored = @(Restore-ScenarioManagedFiles -RunRoot $state.Run.Root -Checkout $Checkout -CheckoutRoot $target.Root)
    Clear-ScenarioCheckoutReservation -Info $info
    Add-ScenarioEvent $state 'managed-restore' $Context ([ordered]@{ checkout=$Checkout; paths=@($restored) })
    return $restored
}

Export-ModuleMember -Function New-Scenario, Add-ScenarioCase, Invoke-Scenario, Set-ScenarioSeam, Reset-ScenarioSeam,
    Write-ScenarioRecord, Save-ScenarioArtifact, Get-ScenarioDirectory, Remove-ScenarioDirectories, Set-ScenarioFile, Reset-ScenarioFiles
