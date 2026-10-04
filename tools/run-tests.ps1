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
    [switch]$ObserveUnity,
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Harness/UnityTestResult.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Harness/Adapters/UnityTestOutput.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Harness/UnityObservationResult.psm1') -Force

# process の開始・終了待機は runner が所有する。offline テストはこの内部境界だけを差し替える。
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
    $processId = $null
    $elapsed = [Diagnostics.Stopwatch]::new()
    try {
        if (-not $process.Start()) { throw "Unity process を起動できません: $Executable" }
        # 後から PID を読むと終了・破棄との競合で identity が失われるため、起動直後に固定する。
        $processId = $process.Id
        $startedAt = [DateTimeOffset]::UtcNow.ToString('o')
        $elapsed.Start()
        # 起動呼び出しの完了はテスト完了ではない。開始した process の終了まで期限なしで待つ。
        $process.WaitForExit()
        $elapsed.Stop()
        $endedAt = [DateTimeOffset]::UtcNow.ToString('o')
        return [pscustomobject]@{ Id=$processId; StartedAt=$startedAt; EndedAt=$endedAt; DurationMs=$elapsed.ElapsedMilliseconds; ExitCode=$process.ExitCode; Failure='' }
    } catch {
        $elapsed.Stop()
        if ($null -ne $startedAt) { $endedAt = [DateTimeOffset]::UtcNow.ToString('o') }
        return [pscustomobject]@{ Id=$processId; StartedAt=$startedAt; EndedAt=$endedAt; DurationMs=$(if ($null -ne $startedAt) { $elapsed.ElapsedMilliseconds } else { $null }); ExitCode=$null; Failure=$_.Exception.Message }
    } finally { $process.Dispose() }
}

function Test-UnityProjectLock {
    param([string]$ProjectPath)
    $lockFile = Join-Path $ProjectPath 'Temp/UnityLockfile'
    if (-not [IO.File]::Exists($lockFile)) { return }
    # ファイルの存在だけでは残骸と稼働中の Editor を区別できない。
    try {
        $stream = [IO.File]::Open($lockFile, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        $stream.Dispose()
    } catch [IO.IOException] {
        throw "Unity Editor が project lock を保持しています: $lockFile"
    }
    # 前回の異常終了で残った非保持 lock だけを片付ける。
    [IO.File]::Delete($lockFile)
}

function Get-EditorSettingsBytesHash([byte[]]$Bytes) {
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function Assert-EditorSettingsKnownDrift([byte[]]$Before, [byte[]]$After, [string]$RecoveryPath, [string]$InvocationId, [string]$Project) {
    # private復旧記録は同一invocation/projectの設定復元を確認する材料であり、asset差分の許容を拡げる根拠にはしない。
    if (-not [IO.File]::Exists($RecoveryPath)) { throw 'reload-settings原記録がありません' }
    $record = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($RecoveryPath)) -AsHashtable -DateKind String -Depth 8
    $keys = @('invocationId','projectPath','enterPlayModeOptionsEnabled','restored','enterPlayModeOptions')
    if ($record -isnot [Collections.IDictionary] -or $record.PSBase.Count -ne $keys.Count) { throw 'reload-settings原記録のkeyが不正です' }
    foreach ($key in $keys) { if (-not $record.Contains($key)) { throw "reload-settings原記録に$keyがありません" } }
    if ($record.invocationId -isnot [string] -or $record.projectPath -isnot [string] -or
        $record.enterPlayModeOptionsEnabled -isnot [bool] -or $record.restored -isnot [bool] -or
        ($record.enterPlayModeOptions -isnot [int] -and $record.enterPlayModeOptions -isnot [long]) -or
        $record.invocationId -cne $InvocationId -or $record.projectPath -cne $Project -or -not $record.restored) {
        throw 'reload-settings原記録の型・identity・復旧状態が不正です'
    }
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    $beforeText = $utf8.GetString($Before); $afterText = $utf8.GetString($After)
    $enabled = [regex]::Matches($beforeText, '(?m)^  m_EnterPlayModeOptionsEnabled: ([01])\r?$')
    $options = [regex]::Matches($beforeText, '(?m)^  m_EnterPlayModeOptions: (-?[0-9]+)\r?$')
    if ($enabled.Count -ne 1 -or $options.Count -ne 1 -or
        $record.enterPlayModeOptionsEnabled -ne ($enabled[0].Groups[1].Value -ceq '1') -or
        [long]$record.enterPlayModeOptions -ne [long]::Parse($options[0].Groups[1].Value)) {
        throw 'reload-settings原記録と取得前EditorSettings設定が一致しません'
    }
    # 許すのはUnity 6000.6による既知の二行だけ。その他の値・順序・改行・byteはすべて一致を要求する。
    $oldLine = '(?m)^  m_SerializeInlineMappingsOnOneLine: 1(?:\r?\n|$)'
    $newLine = '(?m)^  m_UseLegacyHierarchy: 0(?:\r?\n|$)'
    if ([regex]::Matches($beforeText, $oldLine).Count -ne 1 -or [regex]::Matches($afterText, $oldLine).Count -ne 0 -or
        [regex]::Matches($beforeText, $newLine).Count -ne 0 -or [regex]::Matches($afterText, $newLine).Count -ne 1 -or
        -not [string]::Equals([regex]::Replace($beforeText, $oldLine, ''), [regex]::Replace($afterText, $newLine, ''), [StringComparison]::Ordinal)) {
        throw 'EditorSettings差分が既知二行に限定されません'
    }
}

function Complete-ObservedEditorSettings([string]$Path, [byte[]]$Before, [string]$RecoveryPath, [string]$InvocationId, [string]$Project, [bool]$TerminalConfirmed) {
    $beforeHash = Get-EditorSettingsBytesHash $Before
    $afterHash = 'missing'; $restoreHash = 'missing'; $status = 'rejected'; $failure = ''; $reason = 'none'
    try {
        $after = [IO.File]::ReadAllBytes($Path)
        $afterHash = Get-EditorSettingsBytesHash $after
        $restoreHash = $afterHash
        # WaitForExit失敗ではPIDがあってもUnityが稼働中かもしれない。終了値を得るまでassetを戻さない。
        if (-not $TerminalConfirmed) { $reason = 'termination-unconfirmed'; throw 'Unity processの終了値を確認できずEditorSettingsを復元しません' }
        if ($afterHash -ceq $beforeHash) { $status = 'unchanged' }
        else {
            Assert-EditorSettingsKnownDrift $Before $after $RecoveryPath $InvocationId $Project
            # Unity終了後も再読取し、観測した差分以外へ変化していれば上書きしない。
            if (-not [Linq.Enumerable]::SequenceEqual([byte[]][IO.File]::ReadAllBytes($Path), [byte[]]$after)) { throw 'EditorSettingsが検査後に変化しました' }
            [IO.File]::WriteAllBytes($Path, $Before)
            $restoreHash = Get-EditorSettingsBytesHash ([IO.File]::ReadAllBytes($Path))
            if ($restoreHash -cne $beforeHash) { throw 'EditorSettings原bytes復元後のhashが一致しません' }
            $status = 'restored'
        }
    } catch {
        $failure = $_.Exception.Message
        if ($reason -ceq 'none') { $reason = 'cleanup-rejected' }
        try { if ([IO.File]::Exists($Path)) { $restoreHash = Get-EditorSettingsBytesHash ([IO.File]::ReadAllBytes($Path)) } }
        catch { $restoreHash = 'unreadable' }
    }
    [Console]::Out.WriteLine("OSM_EDITOR_SETTINGS beforeSha256=$beforeHash afterSha256=$afterHash restoreSha256=$restoreHash status=$status reason=$reason")
    return [pscustomobject]@{ Status=$status; Failure=$failure; BeforeSha256=$beforeHash; AfterSha256=$afterHash; RestoreSha256=$restoreHash }
}

function Invoke-UnityTestRun {
    param(
        [string]$Filter = '', [string]$Platform = 'EditMode', [string]$UnityRoot = 'D:\UnityEditor',
        [string]$UnityExe = '', [bool]$WithGraphics = $false, [bool]$ObserveUnity = $false, [string]$OutputRoot = '',
        [string]$ProjectPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'unity'),
        [scriptblock]$ProcessInvoker = ${function:Invoke-UnityTestProcess}
    )
    $clock = [Diagnostics.Stopwatch]::StartNew()
    $project = [IO.Path]::GetFullPath($ProjectPath)
    $repoRoot = Split-Path -Parent $project
    $output = $null
    try {
        # 保存先の確保を最初に行う。ここで失敗したら Unity を起動せず、step/marker も残せない。
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
    $editorSettingsBefore = $null
    $editorSettingsPath = Join-Path $project 'ProjectSettings/EditorSettings.asset'
    $process = [pscustomobject]@{ Id=$null; StartedAt=$null; EndedAt=$null; DurationMs=$null; ExitCode=$null; Failure='' }
    $orchestrationFailure = [Collections.Generic.List[string]]::new()
    try {
        if ($ObserveUnity -and $Platform -cne 'EditMode') { throw '-ObserveUnity は EditMode 専用です' }
        # 要求版と実ファイル版は別の観測値。明示 UnityExe でも要求版を ProjectVersion から残す。
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
        if ($ObserveUnity) { $editorSettingsBefore = [IO.File]::ReadAllBytes($editorSettingsPath) }
        $arguments = @('-batchmode', '-projectPath', $project, '-runTests', '-testPlatform', $Platform,
            '-testResults', $output.XmlPath, '-logFile', $output.LogPath)
        if (-not $WithGraphics) { $arguments = @('-nographics') + $arguments }
        # Unity は空の -testFilter を渡すと 0 件になる。
        if ($Filter) { $arguments += @('-testFilter', $Filter) }
        if ($ObserveUnity) { $arguments += @('-osmTestInvocation', $output.InvocationId, '-osmTestObservation', (Get-UnityObservationArgumentPath $output.ObservationPath)) }
        $process = & $ProcessInvoker $executable ([string[]]$arguments) $repoRoot
        if ($process.Failure) { $orchestrationFailure.Add([string]$process.Failure) }
    # 起動前の失敗も、確保済みディレクトリへ failed step として残す。
    } catch { $orchestrationFailure.Add($_.Exception.Message) }

    if ($ObserveUnity -and $null -ne $editorSettingsBefore) {
        $cleanup = Complete-ObservedEditorSettings $editorSettingsPath $editorSettingsBefore (Join-Path $output.Directory 'reload-settings.json') $output.InvocationId $project ($process.ExitCode -is [int])
        if ($cleanup.Failure) { $orchestrationFailure.Add("EditorSettings cleanup拒否: $($cleanup.Failure)") }
    }

    try {
        # 生 XML/log を先に採取して policy に渡す。欠落を古い run のファイルで補わない。
        $evidence = Read-UnityTestOutput $output -ObserveUnity $ObserveUnity
        foreach ($item in $evidence.Failure) { $orchestrationFailure.Add($item) }
        $policy = Get-UnityTestResult -XmlText $evidence.XmlText -LogText $evidence.LogText -ProcessExitCode $process.ExitCode -OrchestrationFailure $orchestrationFailure.ToArray()
        $observation = $null
        if ($ObserveUnity) {
            # expected PID は Unity の自己申告から作らず、起動直後の process seam が返す値を使う。
            $expectedPid = if ($null -ne $process.Id) { [int]$process.Id } else { 0 }
            $requireReload = $Filter -ceq 'OneStarMaker.Tests.Editor.TestObservation.ObservationReloadTests.RealDomainReloadRoundTrip'
            $observation = Get-UnityObservationResult -XmlText $evidence.XmlText -ObservationText $evidence.ObservationText -ProgressText $evidence.ProgressText -LogText $evidence.LogText -ExpectedInvocation $output.InvocationId -ExpectedProject $project -ExpectedPid $expectedPid -ExpectedPath $output.ObservationPath -RequireReload $requireReload
            if ($observation.status -cne 'complete' -or $null -eq $process.ExitCode -or [long]$process.ExitCode -ne 0) {
                $failures = [Collections.Generic.List[string]]::new()
                foreach ($item in $policy.failure) { $failures.Add([string]$item) }
                foreach ($item in $observation.failure) { $failures.Add([string]$item) }
                if ($null -ne $process.ExitCode -and [long]$process.ExitCode -ne 0) { $failures.Add('観測 required は Unity process exit 0 が必要です') }
                $policy.status = 'failed'; $policy.exitCode = 1; $policy.failure = @($failures.ToArray())
            }
        }
        $clock.Stop()
        $step = [ordered]@{
            schemaVersion=$(if ($ObserveUnity) { 2 } else { 1 }); recordKind='unity-test-step'; invocationId=$output.InvocationId; name='unity-tests'; kind='test'
            status=$policy.status; exitCode=$policy.exitCode; failure=@($policy.failure)
            argv=@($arguments); cwd=$repoRoot; projectPath=$project; platform=$Platform; filter=$Filter; withGraphics=$WithGraphics
            unity=[ordered]@{ executablePath=$executable; requestedVersion=$requestedVersion; executableVersion=$executableVersion }
            process=[ordered]@{ startedAt=$process.StartedAt; endedAt=$process.EndedAt; exitCode=$process.ExitCode }
            durationMs=$clock.ElapsedMilliseconds
            timing=[ordered]@{ processDurationMs=$process.DurationMs; xmlDurationSeconds=$policy.xmlDurationSeconds; xmlStartedAt=$policy.xmlStartedAt; xmlEndedAt=$policy.xmlEndedAt
                unityMarkers=$(if ($null -ne $observation -and $observation.status -ceq 'complete') { [ordered]@{status='observed'; clock=$observation.clock; path=$output.ObservationPath} } elseif ($ObserveUnity) { [ordered]@{status=$observation.status} } else { [ordered]@{status='unknown'} })
                loadedAssemblies=$(if ($null -ne $observation -and $observation.status -ceq 'complete') { [ordered]@{status='observed'; assemblies=@($observation.assemblies); path=$output.ObservationPath} } elseif ($ObserveUnity) { [ordered]@{status=$observation.status} } else { [ordered]@{status='unknown'} }) }
            counts=$policy.counts; cases=@($policy.cases); compileErrors=@($policy.compileErrors)
            logs=@($evidence.Logs); logHash=$evidence.LogHash
        }
        if ($ObserveUnity) {
            $step.process.id = $process.Id
            $step.observation = [ordered]@{ required=$true; path=$output.ObservationPath; sha256=$evidence.ObservationHash
                progressPath=$output.ProgressPath; progressSha256=$evidence.ProgressHash; status=$observation.status; failure=@($observation.failure) }
        }
        # marker は step が一度だけ保存された後の取得先通知。status の代用品ではない。
        $stepPath = Save-UnityTestStep -Output $output -Step $step
        [Console]::Out.WriteLine("UNITY_TEST_RESULT $stepPath")
        return [pscustomobject]@{ ExitCode=$policy.exitCode; StepPath=$stepPath; Status=$policy.status }
    } catch {
        # 成功 XML があっても step を確定できなければ、成功を示す marker は出さない。
        [Console]::Error.WriteLine("Unity test の step を保存できません: $($_.Exception.Message)")
        return [pscustomobject]@{ ExitCode=1; StepPath=$null; Status='failed' }
    }
}

# dot-source は内部 process seam を使う offline テスト専用。公開 CLI は上の引数だけ。
if ($MyInvocation.InvocationName -ne '.') {
    $result = Invoke-UnityTestRun -Filter $Filter -Platform $Platform -UnityRoot $UnityRoot -UnityExe $UnityExe -WithGraphics ([bool]$WithGraphics) -ObserveUnity ([bool]$ObserveUnity) -OutputRoot $OutputRoot
    exit $result.ExitCode
}
