Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'PathBudget.psm1')
Import-Module (Join-Path $PSScriptRoot 'UnityProcess.psm1')

# 検証用の checkout（linked worktree、HEAD が detached、その worktree の専用の git dir に基盤の印がある物）
# を扱う: 用意（作成・再利用・commit の切替）、登録、片付け、事前確認、lease、使用中の記録と隔離の解除、
# run の予約、patch の適用、復元、前後の状態の取得。
#
# 状態の置き場:
# - 基盤の印・使用中の記録・予約: <worktree の git dir>/osm-scenario/。worktree と一緒に消える。
# - lease: <共通の git dir>/osm-scenario/leases/<path の hash>.lease。まだ無い checkout の作成の排他にも
#   同じ file を使うので path から決める。worktree の git dir に置くと、開いたままでは
#   `git worktree remove` が消せない。
# 前提: 検証用の checkout は検証の専用で、人が同時に編集しない。人や任意の git の process との排他は保証しない。

$script:StateDirectoryName = 'osm-scenario'
$script:MarkerName = 'checkout.json'
$script:InUseName = 'in-use.json'
$script:ReservationName = 'reservation.json'
$script:InProgressNames = @('rebase-merge', 'rebase-apply', 'BISECT_LOG', 'MERGE_HEAD', 'CHERRY_PICK_HEAD', 'sequencer')
$script:Utf8 = [Text.UTF8Encoding]::new($false)

function Invoke-ScenarioGit {
    # git を ArgumentList で起動し、UTF-8 で読む。失敗は例外にする（AllowFailure の時だけ返す）。
    param([Parameter(Mandatory)] [string]$Root, [Parameter(Mandatory)] [string[]]$Arguments, [switch]$AllowFailure)
    $info = [Diagnostics.ProcessStartInfo]::new('git')
    foreach ($argument in @('-C', $Root) + $Arguments) { [void]$info.ArgumentList.Add($argument) }
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = $script:Utf8
    $info.StandardErrorEncoding = $script:Utf8
    $process = [Diagnostics.Process]::Start($info)
    try {
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEnd()
        $text = $stdout.GetAwaiter().GetResult()
        $process.WaitForExit()
        $result = [pscustomobject]@{ ExitCode=$process.ExitCode; Stdout=$text; Stderr=$stderr }
    } finally { $process.Dispose() }
    if ($result.ExitCode -ne 0 -and -not $AllowFailure) { throw "git $($Arguments -join ' ') に失敗しました（$Root）: $($result.Stderr.Trim())" }
    return $result
}

function ConvertTo-ScenarioFullPath([string]$Path) {
    return [IO.Path]::GetFullPath($Path).TrimEnd('\', '/')
}

function Test-ScenarioSamePath([string]$A, [string]$B) {
    return [string]::Equals((ConvertTo-ScenarioFullPath $A), (ConvertTo-ScenarioFullPath $B), [StringComparison]::OrdinalIgnoreCase)
}

function Get-ScenarioRepository {
    # driver のある checkout から、共通の git dir と主の worktree を決める。
    param([Parameter(Mandatory)] [string]$Root)
    $common = (Invoke-ScenarioGit $Root @('rev-parse', '--path-format=absolute', '--git-common-dir')).Stdout.Trim()
    $commonFull = ConvertTo-ScenarioFullPath $common
    if ([IO.Path]::GetFileName($commonFull) -cne '.git') { throw "主の worktree を決められません（共通の git dir が .git ではありません）: $commonFull" }
    $main = [IO.Path]::GetDirectoryName($commonFull)
    return [pscustomobject]@{ CommonDir=$commonFull; MainWorktree=$main; Place=(Join-Path ([IO.Path]::GetDirectoryName($main)) 'osm-v') }
}

function Get-ScenarioLeasePath([string]$CommonDir, [string]$Path) {
    $key = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($script:Utf8.GetBytes((ConvertTo-ScenarioFullPath $Path).ToLowerInvariant()))).Substring(0, 32).ToLowerInvariant()
    return [IO.Path]::Combine($CommonDir, $script:StateDirectoryName, 'leases', "$key.lease")
}

function Enter-ScenarioCheckoutLease {
    # driver の 1 回の呼び出しの間、checkout（またはまだ無い checkout の path）を占有する。
    # 排他で開いた file を返し、呼び出し側が Dispose する。process が終われば OS が離す。
    param([Parameter(Mandatory)] [string]$CommonDir, [Parameter(Mandatory)] [string]$Path)
    $leasePath = Get-ScenarioLeasePath $CommonDir $Path
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($leasePath))
    try { return [IO.File]::Open($leasePath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None) }
    catch [IO.IOException] { throw "別の呼び出しがこの checkout の lease を持っています: $(ConvertTo-ScenarioFullPath $Path)" }
}

function Read-ScenarioStateFile([string]$Path) {
    if (-not [IO.File]::Exists($Path)) { return $null }
    return ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($Path)) -AsHashtable
}

function Write-ScenarioStateFile([string]$Path, [object]$Value) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    $temp = "$Path.tmp"
    [IO.File]::WriteAllText($temp, (ConvertTo-Json -InputObject $Value -Depth 6), $script:Utf8)
    [IO.File]::Move($temp, $Path, $true)
}

function Get-ScenarioCheckoutInfo {
    # 読むだけ。存在しない path、worktree の最上位でない path も、その事実を返す。
    param([Parameter(Mandatory)] [string]$Root)
    $full = ConvertTo-ScenarioFullPath $Root
    $info = [ordered]@{ Root=$full; Exists=$false; IsTop=$false; GitDir=$null; CommonDir=$null; IsLinked=$false; Head=$null; Branch=$null; Marker=$null; InUse=$null; Reservation=$null; StateDirectory=$null }
    if (-not [IO.Directory]::Exists($full)) { return [pscustomobject]$info }
    $info.Exists = $true
    $top = Invoke-ScenarioGit $full @('rev-parse', '--show-toplevel') -AllowFailure
    if ($top.ExitCode -ne 0 -or -not (Test-ScenarioSamePath $top.Stdout.Trim() $full)) { return [pscustomobject]$info }
    $info.IsTop = $true
    $paths = (Invoke-ScenarioGit $full @('rev-parse', '--path-format=absolute', '--absolute-git-dir', '--git-common-dir')).Stdout.Trim() -split "`r?`n"
    $info.GitDir = ConvertTo-ScenarioFullPath $paths[0]
    $info.CommonDir = ConvertTo-ScenarioFullPath $paths[1]
    $info.IsLinked = -not (Test-ScenarioSamePath $info.GitDir $info.CommonDir)
    $head = Invoke-ScenarioGit $full @('rev-parse', '--verify', '-q', 'HEAD') -AllowFailure
    if ($head.ExitCode -eq 0) { $info.Head = $head.Stdout.Trim() }
    $branch = Invoke-ScenarioGit $full @('symbolic-ref', '-q', 'HEAD') -AllowFailure
    if ($branch.ExitCode -eq 0) { $info.Branch = $branch.Stdout.Trim() }
    $state = Join-Path $info.GitDir $script:StateDirectoryName
    $info.StateDirectory = $state
    $info.Marker = Read-ScenarioStateFile (Join-Path $state $script:MarkerName)
    $info.InUse = Read-ScenarioStateFile (Join-Path $state $script:InUseName)
    $info.Reservation = Read-ScenarioStateFile (Join-Path $state $script:ReservationName)
    return [pscustomobject]$info
}

function Get-ScenarioCheckoutSnapshot {
    # case の前・戻す前・戻した後に取る原出力。判定の担当が record の真偽の欄を確かめ直す材料になる。
    param([Parameter(Mandatory)] [string]$Root)
    $porcelain = (Invoke-ScenarioGit $Root @('status', '--porcelain')).Stdout
    $head = (Invoke-ScenarioGit $Root @('rev-parse', 'HEAD')).Stdout.Trim()
    $index = (Invoke-ScenarioGit $Root @('diff', '--cached', '--binary')).Stdout
    $work = (Invoke-ScenarioGit $Root @('diff', '--binary')).Stdout
    return [pscustomobject]@{
        Porcelain=$porcelain; PorcelainLines=@($porcelain -split "`r?`n" | Where-Object { $_ }); Head=$head
        IndexDiff=$index; IndexClean=($index.Length -eq 0); WorkDiff=$work
    }
}

function Get-ScenarioCheckoutProblems {
    # AC1 の事前確認。問題の説明の列を返し、空なら通る。何も書かない。
    # ExpectedCommit が空でなく AllowHeadMove が偽なら、HEAD が違う時に拒む（渡された checkout は動かさない）。
    # Identity（scenario と RunId）と同じ run の予約は通す（続きの実行）。
    param(
        [Parameter(Mandatory)] [psobject]$Info,
        [Parameter(Mandatory)] [string]$CommonDir,
        [Parameter(Mandatory)] [string]$BudgetRoot,
        [string]$ExpectedCommit = '',
        [bool]$AllowHeadMove = $false,
        [AllowNull()] [Collections.IDictionary]$Identity = $null,
        [bool]$RequireMarker = $true
    )
    $problems = [Collections.Generic.List[string]]::new()
    $root = $Info.Root
    if (-not $Info.Exists) { $problems.Add("checkout がありません: $root"); return @($problems.ToArray()) }
    if (-not $Info.IsTop) { $problems.Add("git の worktree の最上位ではありません: $root"); return @($problems.ToArray()) }
    if (-not $Info.IsLinked) { $problems.Add("主の worktree は検証用の checkout にしません: $root"); return @($problems.ToArray()) }
    if (-not (Test-ScenarioSamePath $Info.CommonDir $CommonDir)) { $problems.Add("別の repo の worktree です（共通の git dir $($Info.CommonDir)）: $root"); return @($problems.ToArray()) }
    if ($RequireMarker) {
        if ($null -eq $Info.Marker) { $problems.Add("基盤の印がありません（検証用の checkout として登録されていません）: $root"); return @($problems.ToArray()) }
        if (-not (Test-ScenarioSamePath ([string]$Info.Marker.commonDir) $CommonDir)) { $problems.Add("基盤の印が別の repo の物です: $root") }
    }
    if ($null -ne $Info.Branch) { $problems.Add("branch を checkout しています（$($Info.Branch)）。検証用の checkout は detached にします: $root") }
    foreach ($name in $script:InProgressNames) {
        $path = Join-Path $Info.GitDir $name
        if ([IO.File]::Exists($path) -or [IO.Directory]::Exists($path)) { $problems.Add("進行中の git の操作があります（$name）: $root") }
    }
    $porcelain = @((Invoke-ScenarioGit $root @('status', '--porcelain')).Stdout -split "`r?`n" | Where-Object { $_ })
    if ($porcelain.Count -ne 0) { $problems.Add("tracked の変更か、ignore されない未追跡の file があります: $root :: $($porcelain -join '; ')") }
    if ($null -ne $Info.InUse) { $problems.Add("使用中の記録が残っています（隔離中。Harness README の手順で解除します）: $root") }
    if ($null -ne $Info.Reservation) {
        $same = $null -ne $Identity -and [string]$Info.Reservation.scenario -ceq [string]$Identity.scenario -and [string]$Info.Reservation.runId -ceq [string]$Identity.runId
        if (-not $same) { $problems.Add("別の run の予約があります（$($Info.Reservation.scenario) / $($Info.Reservation.runId)）: $root") }
    }
    $lock = Get-UnityProjectLockState (Join-Path $root 'unity')
    if ($lock.Held) { $problems.Add("稼働中の Editor が project lock を持っています: $($lock.Path)") }
    $budget = Read-PathBudget $BudgetRoot
    $rootViolation = Test-PathBudgetRoot $root $budget
    if ($rootViolation) { $problems.Add($rootViolation) }
    if ($ExpectedCommit -and -not $AllowHeadMove -and $Info.Head -cne $ExpectedCommit) { $problems.Add("HEAD（$($Info.Head)）が対象の commit（$ExpectedCommit）と違います。渡された checkout の HEAD は動かしません: $root") }
    return @($problems.ToArray())
}

function Get-ScenarioCheckoutCreationProblems {
    # まだ無い checkout を作る前の、置き場の path の検査。何も書かない。
    param([Parameter(Mandatory)] [string]$Path, [Parameter(Mandatory)] [string]$Place, [Parameter(Mandatory)] [string]$BudgetRoot)
    $problems = [Collections.Generic.List[string]]::new()
    $full = ConvertTo-ScenarioFullPath $Path
    if (-not (Test-ScenarioSamePath ([IO.Path]::GetDirectoryName($full)) $Place)) { $problems.Add("置き場の外です（置き場 $(ConvertTo-ScenarioFullPath $Place)）: $full") }
    if ([IO.Directory]::Exists($full) -or [IO.File]::Exists($full)) { $problems.Add("その path に既に何かがあります: $full") }
    $budget = Read-PathBudget $BudgetRoot
    $rootViolation = Test-PathBudgetRoot $full $budget
    if ($rootViolation) { $problems.Add($rootViolation) }
    return @($problems.ToArray())
}

function Set-ScenarioCheckoutMarker {
    param([Parameter(Mandatory)] [psobject]$Info, [Parameter(Mandatory)] [string]$CommonDir, [Parameter(Mandatory)] [string]$Origin)
    Write-ScenarioStateFile (Join-Path $Info.StateDirectory $script:MarkerName) ([ordered]@{ schemaVersion=1; kind='osm-scenario-checkout'; commonDir=$CommonDir; path=$Info.Root; origin=$Origin; utc=[DateTime]::UtcNow.ToString('o') })
}

function New-ScenarioCheckout {
    # linked worktree を detached で作り、その worktree の専用の git dir に基盤の印を置く。
    # 呼び出し側は先に Get-ScenarioCheckoutCreationProblems を通し、path の lease を持っている。
    param([Parameter(Mandatory)] [string]$RepoRoot, [Parameter(Mandatory)] [string]$Path, [Parameter(Mandatory)] [string]$Commit, [Parameter(Mandatory)] [string]$CommonDir)
    $full = ConvertTo-ScenarioFullPath $Path
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($full))
    [void](Invoke-ScenarioGit $RepoRoot @('worktree', 'add', '--detach', $full, $Commit))
    $info = Get-ScenarioCheckoutInfo $full
    if (-not $info.IsTop -or -not $info.IsLinked) { throw "作った worktree を読めません: $full" }
    Set-ScenarioCheckoutMarker $info $CommonDir 'created'
    return Get-ScenarioCheckoutInfo $full
}

function Switch-ScenarioCheckoutCommit {
    # 基盤が用意した checkout の HEAD を対象の commit に合わせる。事前確認（clean、予約なし）を通した物だけ。
    param([Parameter(Mandatory)] [string]$Root, [Parameter(Mandatory)] [string]$Commit)
    [void](Invoke-ScenarioGit $Root @('-c', 'advice.detachedHead=false', 'checkout', '--detach', '-q', $Commit))
}

function Set-ScenarioCheckoutInUse {
    # checkout を変える前（patch を当てる前、Unity を起動する前）に置く。復元を確かめた後にだけ消す。
    # driver が途中で落ちても残り、その checkout を隔離する。
    param([Parameter(Mandatory)] [psobject]$Info, [Parameter(Mandatory)] [Collections.IDictionary]$Record)
    $value = [ordered]@{}
    foreach ($key in $Record.Keys) { $value[$key] = $Record[$key] }
    if (-not $value.Contains('processes')) { $value['processes'] = @() }
    Write-ScenarioStateFile (Join-Path $Info.StateDirectory $script:InUseName) $value
}

function Add-ScenarioCheckoutInUseProcess {
    # process を起動するたびに、その PID と開始時刻を使用中の記録に足す。
    param([Parameter(Mandatory)] [psobject]$Info, [Parameter(Mandatory)] [int]$ProcessId, [string]$StartedAt = '', [string]$Kind = '')
    $path = Join-Path $Info.StateDirectory $script:InUseName
    $record = Read-ScenarioStateFile $path
    if ($null -eq $record) { throw "使用中の記録がありません: $($Info.Root)" }
    $processes = [Collections.Generic.List[object]]::new()
    foreach ($item in @($record.processes)) { if ($null -ne $item) { $processes.Add($item) } }
    $processes.Add([ordered]@{ pid=$ProcessId; startedUtc=$StartedAt; kind=$Kind })
    $record.processes = @($processes.ToArray())
    Write-ScenarioStateFile $path $record
}

function Clear-ScenarioCheckoutInUse {
    param([Parameter(Mandatory)] [psobject]$Info)
    $path = Join-Path $Info.StateDirectory $script:InUseName
    if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
}

function Set-ScenarioCheckoutReservation {
    # run が checkout の中に管理する file を残している間、その checkout をその run に結び付ける。
    param([Parameter(Mandatory)] [psobject]$Info, [Parameter(Mandatory)] [string]$Scenario, [Parameter(Mandatory)] [string]$RunId,
        [Parameter(Mandatory)] [string]$RunRoot, [Parameter(Mandatory)] [string]$Checkout)
    $path = Join-Path $Info.StateDirectory $script:ReservationName
    $current = Read-ScenarioStateFile $path
    if ($null -ne $current) {
        if ([string]$current.scenario -ceq $Scenario -and [string]$current.runId -ceq $RunId) { return }
        throw "別の run の予約があります（$($current.scenario) / $($current.runId)）: $($Info.Root)"
    }
    Write-ScenarioStateFile $path ([ordered]@{ scenario=$Scenario; runId=$RunId; runRoot=$RunRoot; checkout=$Checkout; utc=[DateTime]::UtcNow.ToString('o') })
}

function Clear-ScenarioCheckoutReservation {
    param([Parameter(Mandatory)] [psobject]$Info)
    $path = Join-Path $Info.StateDirectory $script:ReservationName
    if ([IO.File]::Exists($path)) { [IO.File]::Delete($path) }
}

function Test-ScenarioPathIgnored {
    # 管理する file は ignore される file に限る（tracked の file は case の後の復元が戻すので、管理の対象にならない）。
    param([Parameter(Mandatory)] [string]$Root, [Parameter(Mandatory)] [string]$RelativePath)
    $result = Invoke-ScenarioGit $Root @('check-ignore', '-q', '--',$RelativePath.Replace('\', '/')) -AllowFailure
    return $result.ExitCode -eq 0
}

function Invoke-ScenarioCheckoutPatch {
    # 当てる順に全てをまとめて確かめてから、同じ flag で当てる。1 つでも当たらなければ何も当てない。
    param([Parameter(Mandatory)] [string]$Root, [Parameter(Mandatory)] [AllowEmptyCollection()] [string[]]$PatchPaths)
    if ($PatchPaths.Count -eq 0) { return }
    [void](Invoke-ScenarioGit $Root (@('apply', '--check', '--whitespace=nowarn') + $PatchPaths))
    [void](Invoke-ScenarioGit $Root (@('apply', '--whitespace=nowarn') + $PatchPaths))
}

function Restore-ScenarioCheckout {
    # tracked の file と index を HEAD へ戻し、ignore されない未追跡の file を消す（事前確認で、case の
    # 開始の時点に 0 件だったことを確かめてある）。ignore される file は消さない（-x を付けない）。
    param([Parameter(Mandatory)] [string]$Root)
    [void](Invoke-ScenarioGit $Root @('reset', '--hard', '-q'))
    [void](Invoke-ScenarioGit $Root @('clean', '-fd', '-q'))
}

function Remove-ScenarioCheckout {
    # `git worktree remove`（--force なし）で外す。呼び出し側が lease を持ち、事前確認を通してある。
    param([Parameter(Mandatory)] [string]$MainWorktree, [Parameter(Mandatory)] [string]$Root)
    [void](Invoke-ScenarioGit $MainWorktree @('worktree', 'remove', (ConvertTo-ScenarioFullPath $Root)))
}

function Get-ScenarioProjectProcesses {
    # command line がその checkout の project を指す process を読む（止めない）。
    # 子の pwsh だけが終わって、その下の Unity が残っている時を見落とさないために、記録の PID ではなく
    # project の path で探す。ProcessQuery は offline test が差し替える。
    param([Parameter(Mandatory)] [string]$Root, [Parameter(Mandatory)] [scriptblock]$ProcessQuery)
    $project = ConvertTo-ScenarioFullPath (Join-Path $Root 'unity')
    $needles = @($project, $project.Replace('\', '/'))
    return @(& $ProcessQuery | Where-Object {
        $line = [string]$_.CommandLine
        $hit = $false
        foreach ($needle in $needles) { if ($line.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0) { $hit = $true } }
        $hit
    })
}

function Get-ScenarioSystemProcesses {
    # 実の ProcessQuery。CIM の Win32_Process を読むだけで、process に触れない。
    return @(Get-CimInstance -ClassName Win32_Process | ForEach-Object { [pscustomobject]@{ ProcessId=[int]$_.ProcessId; ParentProcessId=[int]$_.ParentProcessId; CommandLine=[string]$_.CommandLine; CreationDate=$_.CreationDate } })
}

Export-ModuleMember -Function Invoke-ScenarioGit, Get-ScenarioRepository, Enter-ScenarioCheckoutLease, Get-ScenarioCheckoutInfo, Get-ScenarioCheckoutSnapshot,
    Get-ScenarioCheckoutProblems, Get-ScenarioCheckoutCreationProblems, Set-ScenarioCheckoutMarker, New-ScenarioCheckout, Switch-ScenarioCheckoutCommit,
    Set-ScenarioCheckoutInUse, Add-ScenarioCheckoutInUseProcess, Clear-ScenarioCheckoutInUse, Set-ScenarioCheckoutReservation, Clear-ScenarioCheckoutReservation,
    Test-ScenarioPathIgnored, Invoke-ScenarioCheckoutPatch, Restore-ScenarioCheckout, Remove-ScenarioCheckout, Get-ScenarioProjectProcesses, Get-ScenarioSystemProcesses
