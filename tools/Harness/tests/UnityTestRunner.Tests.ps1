Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
. (Join-Path $repo 'tools/run-tests.ps1')

$failed = [Collections.Generic.List[string]]::new()
$executed = 0
function Assert($condition, [string]$message) { if (-not $condition) { throw $message } }
function Run([string]$name, [scriptblock]$body) {
    $script:executed++
    try { & $body; [Console]::WriteLine("PASS $name") }
    catch { $script:failed.Add("$name : $($_.Exception.Message)"); [Console]::Error.WriteLine("FAIL $name : $($_.Exception.Message)") }
}
function Xml([string]$rootResult, [string]$cases, [string]$total, [string]$passed, [string]$failedCount, [string]$skipped, [string]$inconclusive) {
    return "<test-run result='$rootResult' total='$total' passed='$passed' failed='$failedCount' skipped='$skipped' inconclusive='$inconclusive' start-time='start' end-time='end' duration='1.25'>$cases</test-run>"
}
function Case([string]$result, [string]$name, [string]$extra = '') {
    return "<test-case id='1' name='$name' fullname='Suite.$name' result='$result' $extra/>"
}
function Policy([AllowNull()][object]$xmlText, [AllowNull()][object]$logText = '', [AllowNull()][object]$processExit = 0, [string[]]$errors = @()) {
    return Get-UnityTestResult -XmlText $xmlText -LogText $logText -ProcessExitCode $processExit -OrchestrationFailure $errors
}

$passedXml = Xml 'Passed' (Case 'Passed' 'A') '1' '1' '0' '0' '0'
Run 'passed and observed fields' {
    $result = Policy $passedXml
    Assert ($result.status -ceq 'passed' -and $result.exitCode -eq 0 -and $result.counts.executed -eq 1) 'valid pass failed'
    Assert ($result.xmlDurationSeconds -eq 1.25 -and $result.xmlStartedAt -ceq 'start') 'XML timing lost'
}
Run 'failed leaf and root' {
    $result = Policy (Xml 'Failed' (Case 'Failed' 'A') '1' '0' '1' '0' '0')
    Assert ($result.status -ceq 'failed' -and $result.counts.failed -eq 1) 'failed leaf accepted'
}
Run 'zero and all skipped' {
    Assert ((Policy (Xml 'Passed' '' '0' '0' '0' '0' '0')).status -ceq 'failed') 'zero accepted'
    Assert ((Policy (Xml 'Passed' (Case 'Skipped' 'A' "label='Ignored'" ) '1' '0' '0' '1' '0')).status -ceq 'failed') 'all skipped accepted'
}
Run 'partial skip keeps order and duplicates' {
    $cases = (Case 'Passed' 'A') + "<test-case id='1' name='A' fullname='Suite.A' result='Skipped' label='Ignored'><reason><message>why</message></reason></test-case>"
    $result = Policy (Xml 'Passed' $cases '2' '1' '0' '1' '0')
    Assert ($result.status -ceq 'passed' -and $result.cases.Count -eq 2 -and $result.cases[1].reason -ceq 'why' -and $result.cases[1].label -ceq 'Ignored') 'skip or duplicate lost'
}
Run 'inconclusive alone and mixed' {
    Assert ((Policy (Xml 'Inconclusive' (Case 'Inconclusive' 'A') '1' '0' '0' '0' '1')).status -ceq 'failed') 'inconclusive alone accepted'
    $mix = (Case 'Passed' 'A') + (Case 'Inconclusive' 'B')
    Assert ((Policy (Xml 'Passed' $mix '2' '1' '0' '0' '1')).status -ceq 'failed') 'inconclusive mixed accepted'
}
Run 'unknown or absent case result' {
    foreach ($value in @('Other','')) {
        $result = Policy (Xml 'Passed' (Case $value 'A') '1' '1' '0' '0' '0')
        Assert ($result.status -ceq 'failed' -and $null -eq $result.counts.total) 'unknown result accepted'
    }
}
Run 'root failure with passed leaf' {
    Assert ((Policy (Xml 'Failed' (Case 'Passed' 'A') '1' '1' '0' '0' '0')).status -ceq 'failed') 'failed root accepted'
}
Run 'missing invalid and inconsistent counts' {
    foreach ($xmlText in @(
        (Xml 'Passed' (Case 'Passed' 'A') '' '1' '0' '0' '0'),
        (Xml 'Passed' (Case 'Passed' 'A') '-1' '1' '0' '0' '0'),
        (Xml 'Passed' (Case 'Passed' 'A') '2' '1' '0' '0' '0'),
        (Xml 'Passed' (Case 'Passed' 'A') '1' '0' '1' '0' '0')
    )) { Assert ((Policy $xmlText).status -ceq 'failed') 'bad count accepted' }
}
Run 'missing malformed and unsafe XML' {
    foreach ($xmlText in @($null, '<test-run>', '<other/>', '<!DOCTYPE test-run [<!ENTITY x SYSTEM "file:///C:/Windows/win.ini">]><test-run/>')) {
        Assert ((Policy $xmlText).status -ceq 'failed') 'missing or unsafe XML accepted'
    }
}
Run 'compile error and missing log' {
    $result = Policy $passedXml 'error CS1234: bad'
    Assert ($result.status -ceq 'failed' -and $result.compileErrors.Count -eq 1) 'compile error accepted'
    Assert ((Policy $passedXml $null).status -ceq 'failed') 'missing log accepted'
}
Run 'access violation and other process exits' {
    foreach ($code in @(-1073741819, 3221225477)) { Assert ((Policy $passedXml '' $code).status -ceq 'passed') 'access violation XML rejected' }
    Assert ((Policy $passedXml '' 2).status -ceq 'failed') 'other exit accepted'
    Assert ((Policy $passedXml '' $null).status -ceq 'failed') 'unknown exit accepted'
    Assert ((Policy $passedXml '' 0 @('start failed')).status -ceq 'failed') 'orchestration failure accepted'
}

$root = Join-Path ([IO.Path]::GetTempPath()) ("osm-h2a-test-" + [guid]::NewGuid())
try {
    [void][IO.Directory]::CreateDirectory($root)
    $project = Join-Path $root 'project with space/unity'
    [void][IO.Directory]::CreateDirectory((Join-Path $project 'ProjectSettings'))
    [IO.File]::WriteAllText((Join-Path $project 'ProjectSettings/ProjectVersion.txt'), 'm_EditorVersion: 6000.6.0f1')
    $exe = Join-Path $root 'Unity Folder/Unity.exe'
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $exe))
    [IO.File]::WriteAllText($exe, 'fixture')
    $outputRoot = Join-Path $root 'output with space'
    $fake = {
        param($binary, $argv, $cwd)
        $xmlPath = $argv[[array]::IndexOf($argv, '-testResults') + 1]
        $logPath = $argv[[array]::IndexOf($argv, '-logFile') + 1]
        [IO.File]::WriteAllText($xmlPath, $passedXml)
        [IO.File]::WriteAllText($logPath, 'fixture log')
        return [pscustomobject]@{ StartedAt='2026-01-01T00:00:00Z'; EndedAt='2026-01-01T00:00:01Z'; DurationMs=1000; ExitCode=0; Failure='' }
    }
    Run 'process seam, argv with spaces, step schema and hash' {
        $result = Invoke-UnityTestRun -ProjectPath $project -UnityExe $exe -OutputRoot $outputRoot -Filter 'A B' -ProcessInvoker $fake
        $step = Get-Content $result.StepPath -Raw | ConvertFrom-Json
        Assert ($result.ExitCode -eq 0 -and $step.status -ceq 'passed' -and $step.recordKind -ceq 'unity-test-step') 'step did not pass'
        Assert ($step.argv[[array]::IndexOf($step.argv, '-projectPath') + 1] -ceq $project -and $step.argv[[array]::IndexOf($step.argv, '-testFilter') + 1] -ceq 'A B') 'arguments lost spaces'
        Assert ($step.unity.requestedVersion -ceq '6000.6.0f1' -and $step.unity.executablePath -ceq $exe -and $step.timing.loadedAssemblies.status -ceq 'unknown') 'identity or unknown observations lost'
        Assert ($step.logs.Count -eq 2 -and $step.logHash -cmatch '^[0-9a-f]{64}$' -and $step.process.exitCode -eq 0 -and $step.durationMs -ge 0) 'evidence or timing lost'
        $hashes = @($step.logs | ForEach-Object { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes((Join-Path (Split-Path $result.StepPath) $_)))) })
        $expected = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes([string]::Join('|', $hashes)))).ToLowerInvariant()
        Assert ($step.logHash -ceq $expected) 'logHash algorithm'
    }
    Run 'empty filter and graphics switch' {
        $result = Invoke-UnityTestRun -ProjectPath $project -UnityExe $exe -OutputRoot $outputRoot -WithGraphics $true -ProcessInvoker $fake
        $step = Get-Content $result.StepPath -Raw | ConvertFrom-Json
        Assert ($step.argv -notcontains '-testFilter' -and $step.argv -notcontains '-nographics' -and $step.withGraphics) 'default filter or graphics'
    }
    Run 'ProjectVersion resolves UnityRoot' {
        $editor = Join-Path $root 'Unity installs/6000.6.0f1/Editor/Unity.exe'
        [void][IO.Directory]::CreateDirectory((Split-Path -Parent $editor))
        [IO.File]::WriteAllText($editor, 'fixture')
        $result = Invoke-UnityTestRun -ProjectPath $project -UnityRoot (Join-Path $root 'Unity installs') -OutputRoot $outputRoot -ProcessInvoker $fake
        $step = Get-Content $result.StepPath -Raw | ConvertFrom-Json
        Assert ($result.ExitCode -eq 0 -and $step.unity.executablePath -ceq $editor) 'ProjectVersion did not resolve UnityRoot'
    }
    Run 'missing XML and log create failed step' {
        $silent = { param($binary, $argv, $cwd) [pscustomobject]@{ StartedAt='now'; EndedAt='later'; DurationMs=1; ExitCode=0; Failure='' } }
        $result = Invoke-UnityTestRun -ProjectPath $project -UnityExe $exe -OutputRoot $outputRoot -ProcessInvoker $silent
        $step = Get-Content $result.StepPath -Raw | ConvertFrom-Json
        Assert ($result.ExitCode -eq 1 -and $step.counts.total -eq $null -and $step.logs.Count -eq 0) 'missing files were accepted'
        $emptyHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(''))).ToLowerInvariant()
        Assert ($step.logHash -ceq $emptyHash) 'empty log hash'
    }
    Run 'start failure records failed step' {
        $throwing = { param($binary, $argv, $cwd) throw 'fixture start failure' }
        $result = Invoke-UnityTestRun -ProjectPath $project -UnityExe $exe -OutputRoot $outputRoot -ProcessInvoker $throwing
        $step = Get-Content $result.StepPath -Raw | ConvertFrom-Json
        Assert ($result.ExitCode -eq 1 -and $step.process.startedAt -eq $null -and $step.failure.Count -gt 0) 'start failure lost'
    }
    Run 'held lock refuses process' {
        $temp = Join-Path $project 'Temp'
        [void][IO.Directory]::CreateDirectory($temp)
        $lock = Join-Path $temp 'UnityLockfile'
        $stream = [IO.File]::Open($lock, [IO.FileMode]::Create, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try {
            $result = Invoke-UnityTestRun -ProjectPath $project -UnityExe $exe -OutputRoot $outputRoot -ProcessInvoker $fake
            $step = Get-Content $result.StepPath -Raw | ConvertFrom-Json
            Assert ($result.ExitCode -eq 1 -and $step.process.startedAt -eq $null) 'held lock did not refuse'
        } finally { $stream.Dispose(); [IO.File]::Delete($lock) }
    }
    Run 'output creation failure has no step' {
        $blocked = Join-Path $root 'blocked'
        [IO.File]::WriteAllText($blocked, 'file')
        $result = Invoke-UnityTestRun -ProjectPath $project -UnityExe $exe -OutputRoot $blocked -ProcessInvoker $fake
        Assert ($result.ExitCode -eq 1 -and $null -eq $result.StepPath) 'output failure created step'
    }
    Run 'existing GUID and step refuse overwrite' {
        $id = [guid]::NewGuid()
        $first = New-UnityTestOutput -OutputRoot $outputRoot -InvocationId $id
        $rejected = $false
        try { [void](New-UnityTestOutput -OutputRoot $outputRoot -InvocationId $id) } catch { $rejected = $true }
        Assert $rejected 'existing GUID reused'
        [IO.File]::WriteAllText($first.StepPath, 'original')
        $rejected = $false
        try { [void](Save-UnityTestStep -Output $first -Step @{status='passed'}) } catch { $rejected = $true }
        Assert ($rejected -and [IO.File]::ReadAllText($first.StepPath) -ceq 'original') 'step overwritten'
    }
    Run 'runner step save failure exits one' {
        $intercept = {
            param($binary, $argv, $cwd)
            $xmlPath = $argv[[array]::IndexOf($argv, '-testResults') + 1]
            [IO.File]::WriteAllText($xmlPath, $passedXml)
            [IO.File]::WriteAllText($argv[[array]::IndexOf($argv, '-logFile') + 1], 'log')
            [IO.File]::WriteAllText((Join-Path (Split-Path $xmlPath) 'step.json'), 'occupied')
            return [pscustomobject]@{ StartedAt='now'; EndedAt='later'; DurationMs=1; ExitCode=0; Failure='' }
        }
        $result = Invoke-UnityTestRun -ProjectPath $project -UnityExe $exe -OutputRoot $outputRoot -ProcessInvoker $intercept
        Assert ($result.ExitCode -eq 1 -and $null -eq $result.StepPath) 'step save failure passed'
    }
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'osm-h2a-test-*') { throw 'unsafe cleanup path' }
    if ([IO.Directory]::Exists($resolved)) { [IO.Directory]::Delete($resolved, $true) }
}
[Console]::WriteLine("Unity runner offline: $executed executed, $($failed.Count) failed")
if ($failed.Count -gt 0) { exit 1 }
