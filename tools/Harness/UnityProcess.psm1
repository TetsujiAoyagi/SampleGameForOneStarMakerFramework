Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Unity と、Unity を扱う補助の process の起動・待機・停止。run-tests.ps1 と実機の検証の基盤
# （Scenario*.psm1）が同じ起動を使うために、どちらにも属さない module に置く。状態は持たない。
# 起動した process の handle は呼び出し側が持ち、寿命（Close-UnityProcess）も呼び出し側が決める。
#
# handle は次の形の object で、偽の process（offline test）も同じ形で作る:
#   Id, StartedAt（ISO 8601）, Executable, Argv
#   WaitForExit([int]ms) -> bool（-1 は期限なし）, GetExitCode() -> int, KillTree(), ReadOutput() -> string, Close()
# 終了の確認は WaitForExit が true を返し、GetExitCode が整数を返すことだけで決める。
# 既知の native stall では終了コード 0 でも handle が signal にならないことがあるので、
# ログや lock の有無を終了の代わりにしない（tools/Harness/README.md の停止規則）。

function Get-UnityRequestedVersion {
    # ProjectVersion.txt の要求版。実行 file の版とは別の観測値として扱う。
    param([Parameter(Mandatory)] [string]$ProjectPath)
    $versionFile = Join-Path ([IO.Path]::GetFullPath($ProjectPath)) 'ProjectSettings/ProjectVersion.txt'
    $versionText = [IO.File]::ReadAllText($versionFile)
    $match = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)')
    if (-not $match.Success) { throw "ProjectVersion.txt から要求版を読めません: $versionFile" }
    return $match.Groups[1].Value
}

function Resolve-UnityExecutablePath {
    # 明示の実行 file を優先し、無ければ UnityRoot の下の要求版を使う。存在は確かめない
    # （見つからない時も、どの path を探したかを記録に残すため）。
    param([Parameter(Mandatory)] [string]$RequestedVersion, [string]$UnityRoot = 'D:\UnityEditor', [string]$UnityExe = '')
    if ($UnityExe) { return [IO.Path]::GetFullPath($UnityExe) }
    return [IO.Path]::GetFullPath((Join-Path $UnityRoot "$RequestedVersion/Editor/Unity.exe"))
}

function Get-UnityExecutableVersion {
    param([Parameter(Mandatory)] [string]$Executable)
    if (-not [IO.File]::Exists($Executable)) { throw "Unity.exe が見つかりません: $Executable" }
    $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($Executable).FileVersion
    if ($fileVersion) { return $fileVersion }
    return 'unknown'
}

function Get-UnityProjectLockState {
    # project lock を消さずに調べる。基盤はこれだけを使う。
    # file の存在だけでは残骸と稼働中の Editor を区別できないので、排他で開けるかを見る。
    # 開けた時もすぐ閉じ、file は残す（残骸を片付けてよいのは run-tests.ps1 だけ）。
    param([Parameter(Mandatory)] [string]$ProjectPath)
    $lockFile = Join-Path ([IO.Path]::GetFullPath($ProjectPath)) 'Temp/UnityLockfile'
    if (-not [IO.File]::Exists($lockFile)) { return [pscustomobject]@{ Path=$lockFile; Exists=$false; Held=$false } }
    try {
        $stream = [IO.File]::Open($lockFile, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $stream.Dispose()
        return [pscustomobject]@{ Path=$lockFile; Exists=$true; Held=$false }
    } catch [IO.FileNotFoundException] {
        # 調べる間に消えた。Unity の終了の途中でも起きるので、終了の確認には使わない。
        return [pscustomobject]@{ Path=$lockFile; Exists=$false; Held=$false }
    } catch [IO.IOException] {
        return [pscustomobject]@{ Path=$lockFile; Exists=$true; Held=$true }
    }
}

function Clear-UnityStaleProjectLock {
    # 稼働中の Editor が持つ lock なら止め、前回の異常終了で残った非保持の lock だけを消す。
    # run-tests.ps1 の起動の直前だけが使う。基盤（Scenario*.psm1）は使わない。
    param([Parameter(Mandatory)] [string]$ProjectPath)
    $state = Get-UnityProjectLockState $ProjectPath
    if ($state.Held) { throw "Unity Editor が project lock を保持しています: $($state.Path)" }
    if ($state.Exists) { [IO.File]::Delete($state.Path) }
}

function Start-UnityProcess {
    # ArgumentList で起動し、起動の直後に PID と開始時刻を固定する（後から読むと終了・破棄との
    # 競合で identity が失われる）。窓は既定で作らない。窓のある Editor だけが -CreateWindow を渡す。
    # Environment は子の process にだけ設定し、この process の環境は変えない。
    param(
        [Parameter(Mandatory)] [string]$Executable,
        [AllowEmptyCollection()] [string[]]$Arguments = @(),
        [string]$WorkingDirectory = '',
        [switch]$CreateWindow,
        [hashtable]$Environment = @{},
        [switch]$RedirectOutput
    )
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Executable
    if ($WorkingDirectory) { $info.WorkingDirectory = $WorkingDirectory }
    $info.UseShellExecute = $false
    $info.CreateNoWindow = -not $CreateWindow
    foreach ($argument in $Arguments) { [void]$info.ArgumentList.Add($argument) }
    foreach ($key in $Environment.Keys) { $info.Environment[[string]$key] = [string]$Environment[$key] }
    if ($RedirectOutput) {
        $info.RedirectStandardOutput = $true
        $info.RedirectStandardError = $true
        $info.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
        $info.StandardErrorEncoding = [Text.UTF8Encoding]::new($false)
    }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    try {
        if (-not $process.Start()) { throw "process を起動できません: $Executable" }
    } catch { $process.Dispose(); throw }
    $processId = $process.Id
    $startedAt = [DateTimeOffset]::UtcNow.ToString('o')
    # 出力は起動の直後から読み始める。読まずに待つと、出力の多い子が pipe で止まる。
    $stdout = $null; $stderr = $null
    if ($RedirectOutput) { $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync() }
    $handle = [pscustomobject]@{ Id=$processId; StartedAt=$startedAt; Executable=$Executable; Argv=@($Arguments); Process=$process; StdoutTask=$stdout; StderrTask=$stderr }
    $handle | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value { param([int]$Milliseconds) return $this.Process.WaitForExit($Milliseconds) }
    $handle | Add-Member -MemberType ScriptMethod -Name GetExitCode -Value { return [int]$this.Process.ExitCode }
    # 保持している object に対してだけ止める。PID や名前で探さない。
    $handle | Add-Member -MemberType ScriptMethod -Name KillTree -Value { $this.Process.Kill($true) }
    $handle | Add-Member -MemberType ScriptMethod -Name ReadOutput -Value {
        if ($null -eq $this.StdoutTask) { return '' }
        $out = $this.StdoutTask.GetAwaiter().GetResult()
        $err = $this.StderrTask.GetAwaiter().GetResult()
        if ($err) { return $out + $err }
        return $out
    }
    $handle | Add-Member -MemberType ScriptMethod -Name Close -Value { $this.Process.Dispose() }
    return $handle
}

function Wait-UnityProcess {
    # TimeoutMs を省けば期限なしで待つ（run-tests.ps1 の待ち方）。期限があれば、期限までに
    # signal にならなかった時は Signaled=$false で返し、止めない。止めるかは呼び出し側が決める。
    param([Parameter(Mandatory)] [psobject]$Handle, [int]$TimeoutMs = -1)
    $elapsed = [Diagnostics.Stopwatch]::StartNew()
    $signaled = [bool]$Handle.WaitForExit($TimeoutMs)
    $elapsed.Stop()
    $exitCode = $null
    $endedAt = $null
    if ($signaled) {
        # 期限なしの WaitForExit(-1) は、出力を読む非同期の処理の終わりまで待たない版がある。
        # 終了コードは signal の後にだけ読む。
        $exitCode = [int]$Handle.GetExitCode()
        $endedAt = [DateTimeOffset]::UtcNow.ToString('o')
    }
    return [pscustomobject]@{ Signaled=$signaled; ExitCode=$exitCode; EndedAt=$endedAt; DurationMs=$elapsed.ElapsedMilliseconds }
}

function Stop-UnityProcessTree {
    # 期限の後に止めるのは、scenario が起動で明示した時だけ（呼び出し側が判断する）。
    # 相手は保持している handle の process とその子孫に限る。
    param([Parameter(Mandatory)] [psobject]$Handle)
    $Handle.KillTree()
}

function Close-UnityProcess {
    param([Parameter(Mandatory)] [psobject]$Handle)
    $Handle.Close()
}

Export-ModuleMember -Function Get-UnityRequestedVersion, Resolve-UnityExecutablePath, Get-UnityExecutableVersion,
    Get-UnityProjectLockState, Clear-UnityStaleProjectLock, Start-UnityProcess, Wait-UnityProcess, Stop-UnityProcessTree, Close-UnityProcess
