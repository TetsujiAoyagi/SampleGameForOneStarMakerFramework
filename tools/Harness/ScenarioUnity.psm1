Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'UnityProcess.psm1')

# 実機の検証の基盤が起動する Unity と外部の process: run-tests.ps1 の子の pwsh、batch の Unity
# （-executeMethod）、終わるまで待つ外部 command、起動して保持する補助の process。
# process の起動は呼び出し側が渡す Start（scriptblock）で行う。Scenario.psm1 はそこで台帳に載せ、
# 使用中の記録に PID を足す。この module は record も証拠の置き場も知らない（ScenarioRun に依存しない）。
#
# Start の形: & $Start <実行 file> <引数の列> <作業 directory> @{ CreateWindow; Environment; RedirectOutput }
#   → UnityProcess.psm1 の handle の形の object

$script:MarkerPrefix = 'UNITY_TEST_RESULT '

function Test-ScenarioUnder([string]$Parent, [string]$Target) {
    $p = [IO.Path]::GetFullPath($Parent).TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
    return [IO.Path]::GetFullPath($Target).StartsWith($p, [StringComparison]::OrdinalIgnoreCase)
}

function Invoke-ScenarioTestRunner {
    # その checkout の tools/run-tests.ps1 を子の pwsh で起動し、終わるまで期限なしで待つ（dot-source しない）。
    # 基盤が持つのは子の pwsh の handle だけで、その下の Unity の handle は持たない。子の終了は Unity の
    # 終了を示さないので、終了の確認は次の全てが揃った時だけにする:
    #   子の handle の signal、marker がある、marker の指す step が今回の物（path が OutputRoot の下、
    #   projectPath がその checkout の project、filter が今回渡した値）、step の process.exitCode が整数か、
    #   Unity を起動しなかった step（kind が preflight）。
    param(
        [Parameter(Mandatory)] [scriptblock]$Start,
        [Parameter(Mandatory)] [string]$CheckoutRoot,
        [Parameter(Mandatory)] [string]$OutputRoot,
        [string]$Filter = '',
        [hashtable]$Environment = @{},
        [string]$PwshPath = ([Environment]::ProcessPath)
    )
    $root = [IO.Path]::GetFullPath($CheckoutRoot).TrimEnd('\', '/')
    $project = Join-Path $root 'unity'
    $arguments = @('-NoLogo', '-NoProfile', '-File', (Join-Path $root 'tools/run-tests.ps1'), '-OutputRoot', $OutputRoot)
    if ($Filter) { $arguments += @('-Filter', $Filter) }
    $handle = & $Start $PwshPath ([string[]]$arguments) $root @{ Environment=$Environment; RedirectOutput=$true }
    $wait = Wait-UnityProcess -Handle $handle
    $output = [string]$handle.ReadOutput()
    $result = [ordered]@{
        Handle=$handle; Pid=$handle.Id; StartedUtc=$handle.StartedAt; Argv=@($arguments); Signaled=$wait.Signaled; ChildExitCode=$wait.ExitCode
        Output=$output; StepPath=''; Step=$null; StepIsCurrent=$false; Total=$null; Status='no-marker'; UnityExitCode=$null; TerminationConfirmed=$false; Failure=[Collections.Generic.List[string]]::new()
    }
    $marker = @($output -split "`r?`n" | Where-Object { $_.StartsWith($script:MarkerPrefix, [StringComparison]::Ordinal) }) | Select-Object -Last 1
    if (-not $marker) { $result.Failure.Add('UNITY_TEST_RESULT の marker がありません'); return [pscustomobject]$result }
    # path 自体に空白がありうるので、prefix を除いた行末全体を取得先にする。
    $result.StepPath = $marker.Substring($script:MarkerPrefix.Length)
    try { $step = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($result.StepPath)) -AsHashtable }
    catch { $result.Failure.Add("step を読めません: $($_.Exception.Message)"); return [pscustomobject]$result }
    $result.Step = $step
    if ($step.Contains('status')) { $result.Status = [string]$step.status }
    $projectMatches = $step.Contains('projectPath') -and ([IO.Path]::GetFullPath([string]$step.projectPath).TrimEnd('\', '/') -ieq $project)
    $filterMatches = $step.Contains('filter') -and ([string]$step.filter -ceq $Filter)
    $result.StepIsCurrent = (Test-ScenarioUnder $OutputRoot $result.StepPath) -and $projectMatches -and $filterMatches
    if (-not $result.StepIsCurrent) { $result.Failure.Add("marker の指す step が今回の物ではありません: $($result.StepPath)"); return [pscustomobject]$result }
    if ($step.Contains('counts') -and $step.counts -is [Collections.IDictionary] -and $step.counts.Contains('total')) { $result.Total = $step.counts.total }
    $exitCode = $null
    if ($step.Contains('process') -and $step.process -is [Collections.IDictionary] -and $step.process.Contains('exitCode')) { $exitCode = $step.process.exitCode }
    $isInteger = $exitCode -is [int] -or $exitCode -is [long]
    if ($isInteger) { $result.UnityExitCode = [long]$exitCode }
    $preflight = $step.Contains('kind') -and [string]$step.kind -ceq 'preflight'
    $result.TerminationConfirmed = $wait.Signaled -and ($isInteger -or $preflight)
    if (-not $result.TerminationConfirmed) { $result.Failure.Add('Unity の終了コードが step にありません（起動できなかったか、待ちに失敗した）') }
    return [pscustomobject]$result
}

function Start-ScenarioBatchProcess {
    # batch の Unity（-executeMethod）。窓を作らず、log はその試行の置き場に書かせる。
    param(
        [Parameter(Mandatory)] [scriptblock]$Start,
        [Parameter(Mandatory)] [string]$UnityExecutable,
        [Parameter(Mandatory)] [string]$CheckoutRoot,
        [Parameter(Mandatory)] [string]$Method,
        [Parameter(Mandatory)] [string]$LogPath,
        [AllowEmptyCollection()] [string[]]$Arguments = @()
    )
    $root = [IO.Path]::GetFullPath($CheckoutRoot).TrimEnd('\', '/')
    $argv = @('-batchmode', '-nographics', '-quit', '-projectPath', (Join-Path $root 'unity'), '-executeMethod', $Method, '-logFile', $LogPath) + $Arguments
    return & $Start $UnityExecutable ([string[]]$argv) $root @{ CreateWindow=$false; Environment=@{}; RedirectOutput=$false }
}

function Start-ScenarioCommandProcess {
    # 外部 command（hook や任意の観測）。出力は読み、終わりは呼び出し側が待つ。
    param([Parameter(Mandatory)] [scriptblock]$Start, [Parameter(Mandatory)] [string]$Executable, [AllowEmptyCollection()] [string[]]$Arguments = @(), [string]$WorkingDirectory = '', [hashtable]$Environment = @{})
    return & $Start $Executable ([string[]]$Arguments) $WorkingDirectory @{ CreateWindow=$false; Environment=$Environment; RedirectOutput=$true }
}

function Start-ScenarioHelperProcess {
    # 補助の process。driver の 1 回の呼び出しの間だけ生かす。checkout に触れる子の process を残さない物に限る
    # （基盤は保持した handle の終了から、全ての子孫の終了を主張しない）。
    param([Parameter(Mandatory)] [scriptblock]$Start, [Parameter(Mandatory)] [string]$Executable, [AllowEmptyCollection()] [string[]]$Arguments = @(), [string]$WorkingDirectory = '', [hashtable]$Environment = @{})
    return & $Start $Executable ([string[]]$Arguments) $WorkingDirectory @{ CreateWindow=$false; Environment=$Environment; RedirectOutput=$false }
}

Export-ModuleMember -Function Invoke-ScenarioTestRunner, Start-ScenarioBatchProcess, Start-ScenarioCommandProcess, Start-ScenarioHelperProcess
