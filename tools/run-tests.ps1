<#
.SYNOPSIS
    Unity テストを実行し、XML、log、機械判定した step を保存する。
.DESCRIPTION
    各呼び出しを新しい GUID ディレクトリに保存する。結果の取得先は
    UNITY_TEST_RESULT marker の step.json。status と exitCode を読んで合否を判断する。
.PARAMETER Filter
    -testFilter の値。空なら全 EditMode テストを実行する。
.PARAMETER Platform
    -testPlatform の値。既定は EditMode。
.PARAMETER UnityRoot
    ProjectVersion.txt の版を探す Unity インストール親ディレクトリ。
.PARAMETER UnityExe
    実行ファイルを直接指定する。ProjectVersion の要求版とは別に記録する。
.PARAMETER WithGraphics
    -nographics を付けない。
.PARAMETER OutputRoot
    保存先の親ディレクトリ。既定はリポジトリ直下 TestResults。
.OUTPUTS
    exit 0 = step の status が passed。exit 1 = failed、または保存不能。
#>
[CmdletBinding()]
param(
    [string]$Filter = '',
    [string]$Platform = 'EditMode',
    [string]$UnityRoot = 'D:\UnityEditor',
    [string]$UnityExe = '',
    [switch]$WithGraphics,
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Harness/UnityTestResult.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Harness/Adapters/UnityTestOutput.psm1') -Force

function Invoke-UnityTestProcess {
    param([string]$Executable, [string[]]$Arguments, [string]$WorkingDirectory)
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = $Executable
    $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    foreach ($argument in $Arguments) { [void]$info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $info
    $startedAt = $null
    $endedAt = $null
    $elapsed = [Diagnostics.Stopwatch]::new()
    try {
        if (-not $process.Start()) { throw "Unity process を起動できません: $Executable" }
        $startedAt = [DateTimeOffset]::UtcNow.ToString('o')
        $elapsed.Start()
        # Unity は子 process に制御を返すため、所有した process だけを期限なしで待つ。
        $process.WaitForExit()
        $elapsed.Stop()
        $endedAt = [DateTimeOffset]::UtcNow.ToString('o')
        return [pscustomobject]@{ StartedAt=$startedAt; EndedAt=$endedAt; DurationMs=$elapsed.ElapsedMilliseconds; ExitCode=$process.ExitCode; Failure='' }
    } catch {
        $elapsed.Stop()
        if ($null -ne $startedAt) { $endedAt = [DateTimeOffset]::UtcNow.ToString('o') }
        return [pscustomobject]@{ StartedAt=$startedAt; EndedAt=$endedAt; DurationMs=$(if ($null -ne $startedAt) { $elapsed.ElapsedMilliseconds } else { $null }); ExitCode=$null; Failure=$_.Exception.Message }
    } finally { $process.Dispose() }
}

function Test-UnityProjectLock {
    param([string]$ProjectPath)
    $lockFile = Join-Path $ProjectPath 'Temp/UnityLockfile'
    if (-not [IO.File]::Exists($lockFile)) { return }
    try {
        $stream = [IO.File]::Open($lockFile, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $stream.Dispose()
    } catch [IO.IOException] {
        throw "Unity Editor が project lock を保持しています: $lockFile"
    }
    # 前回の異常終了で残った非保持 lock だけを片付ける。
    [IO.File]::Delete($lockFile)
}

function Invoke-UnityTestRun {
    param(
        [string]$Filter = '', [string]$Platform = 'EditMode', [string]$UnityRoot = 'D:\UnityEditor',
        [string]$UnityExe = '', [bool]$WithGraphics = $false, [string]$OutputRoot = '',
        [string]$ProjectPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'unity'),
        [scriptblock]$ProcessInvoker = ${function:Invoke-UnityTestProcess}
    )
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $project = [IO.Path]::GetFullPath($ProjectPath)
    $repoRoot = Split-Path -Parent $project
    $output = $null
    try {
        $root = if ($OutputRoot) { $OutputRoot } else { Join-Path $repoRoot 'TestResults' }
        $output = New-UnityTestOutput -OutputRoot $root
    } catch {
        [Console]::Error.WriteLine("Unity test の出力先を確保できません: $($_.Exception.Message)")
        return [pscustomobject]@{ ExitCode=1; StepPath=$null; Status='failed' }
    }

    $requestedVersion = 'unknown'
    $executableVersion = 'unknown'
    $executable = ''
    $arguments = @()
    $process = [pscustomobject]@{ StartedAt=$null; EndedAt=$null; DurationMs=$null; ExitCode=$null; Failure='' }
    $orchestrationFailure = [Collections.Generic.List[string]]::new()
    try {
        $versionFile = Join-Path $project 'ProjectSettings/ProjectVersion.txt'
        $versionText = [IO.File]::ReadAllText($versionFile)
        $match = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)')
        if (-not $match.Success) { throw "ProjectVersion.txt から要求版を読めません: $versionFile" }
        $requestedVersion = $match.Groups[1].Value
        if ($UnityExe) {
            $executable = [IO.Path]::GetFullPath($UnityExe)
        } else {
            $executable = [IO.Path]::GetFullPath((Join-Path $UnityRoot "$requestedVersion/Editor/Unity.exe"))
        }
        if (-not [IO.File]::Exists($executable)) { throw "Unity.exe が見つかりません: $executable" }
        $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable).FileVersion
        if ($fileVersion) { $executableVersion = $fileVersion }
        Test-UnityProjectLock $project
        $arguments = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', $Platform,
            '-testResults', $output.XmlPath, '-logFile', $output.LogPath)
        if (-not $WithGraphics) { $arguments = @('-nographics') + $arguments }
        # Unity は空の -testFilter を渡すと 0 件になる。
        if ($Filter) { $arguments += @('-testFilter', $Filter) }
        $process = & $ProcessInvoker $executable ([string[]]$arguments) $repoRoot
        if ($process.Failure) { $orchestrationFailure.Add([string]$process.Failure) }
    } catch { $orchestrationFailure.Add($_.Exception.Message) }

    try {
        $evidence = Read-UnityTestOutput $output
        foreach ($item in $evidence.Failure) { $orchestrationFailure.Add($item) }
        $policy = Get-UnityTestResult -XmlText $evidence.XmlText -LogText $evidence.LogText -ProcessExitCode $process.ExitCode -OrchestrationFailure $orchestrationFailure.ToArray()
        $clock.Stop()
        $step = [ordered]@{
            schemaVersion=1; recordKind='unity-test-step'; invocationId=$output.InvocationId; name='unity-tests'; kind='test'
            status=$policy.status; exitCode=$policy.exitCode; failure=@($policy.failure)
            argv=@($arguments); cwd=$repoRoot; projectPath=$project; platform=$Platform; filter=$Filter; withGraphics=$WithGraphics
            unity=[ordered]@{ executablePath=$executable; requestedVersion=$requestedVersion; executableVersion=$executableVersion }
            process=[ordered]@{ startedAt=$process.StartedAt; endedAt=$process.EndedAt; exitCode=$process.ExitCode }
            durationMs=$clock.ElapsedMilliseconds
            timing=[ordered]@{ processDurationMs=$process.DurationMs; xmlDurationSeconds=$policy.xmlDurationSeconds; xmlStartedAt=$policy.xmlStartedAt; xmlEndedAt=$policy.xmlEndedAt; unityMarkers=[ordered]@{status='unknown'}; loadedAssemblies=[ordered]@{status='unknown'} }
            counts=$policy.counts; cases=@($policy.cases); compileErrors=@($policy.compileErrors)
            logs=@($evidence.Logs); logHash=$evidence.LogHash
        }
        $stepPath = Save-UnityTestStep -Output $output -Step $step
        [Console]::Out.WriteLine("UNITY_TEST_RESULT $stepPath")
        return [pscustomobject]@{ ExitCode=$policy.exitCode; StepPath=$stepPath; Status=$policy.status }
    } catch {
        [Console]::Error.WriteLine("Unity test の step を保存できません: $($_.Exception.Message)")
        return [pscustomobject]@{ ExitCode=1; StepPath=$null; Status='failed' }
    }
}

# dot-source は内部 process seam を使う offline テスト専用。公開 CLI は上の引数だけ。
if ($MyInvocation.InvocationName -ne '.') {
    $result = Invoke-UnityTestRun -Filter $Filter -Platform $Platform -UnityRoot $UnityRoot -UnityExe $UnityExe -WithGraphics ([bool]$WithGraphics) -OutputRoot $OutputRoot
    exit $result.ExitCode
}
