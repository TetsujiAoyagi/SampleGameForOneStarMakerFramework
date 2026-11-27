param([string]$ResultPath = '', [string]$Filter = '')
# 実機の検証の基盤の process の側の offline test: UnityProcess、run-tests.ps1 の子の起動と step の照合、
# batch、外部 command、補助の process、窓のある Editor の adapter。偽の process・偽の CLI・偽の時計を使い、
# 実の Unity も Unity CLI も起動しない。実の process は、合図なしに自分で終わる子の pwsh だけを使う。
# 基盤が返した成否だけでなく、偽の CLI と偽の process が受けた呼び出しの回数と中身、checkout の bytes を直接見る。
. (Join-Path $PSScriptRoot 'ScenarioTestSupport.ps1')
$script:pwsh = [Environment]::ProcessPath

# --- 偽の process・時計・CLI --------------------------------------------------
function New-FakeProcess {
    # WaitForExit が ExitAfter 回を超えて呼ばれたら終わる。Never なら、Exited を外から立てるか KillTree まで終わらない。
    param([int]$Id = 4100, [int]$ExitCode = 0, [int]$ExitAfter = 0, [switch]$Never, [string]$Output = '', [switch]$ExitOnKill, [scriptblock]$OnWait = $null)
    $p = [pscustomobject]@{ Id=$Id; StartedAt='2026-11-26T00:00:00.0000000+00:00'; Executable=''; Argv=@(); Exited=$false; ExitCodeValue=$ExitCode
        Waits=0; WaitArgs=[Collections.Generic.List[int]]::new(); ExitAfter=$ExitAfter; Never=[bool]$Never; Kills=0; ExitOnKill=[bool]$ExitOnKill; OutputText=$Output; OnWait=$OnWait }
    $p | Add-Member -MemberType ScriptMethod -Name WaitForExit -Value {
        param([int]$Milliseconds)
        $this.WaitArgs.Add($Milliseconds); $this.Waits++
        if ($null -ne $this.OnWait) { & $this.OnWait $this }
        if (-not $this.Exited -and -not $this.Never -and $this.Waits -gt $this.ExitAfter) { $this.Exited = $true }
        return $this.Exited
    }
    $p | Add-Member -MemberType ScriptMethod -Name GetExitCode -Value { if (-not $this.Exited) { throw 'not exited' }; return [int]$this.ExitCodeValue }
    $p | Add-Member -MemberType ScriptMethod -Name KillTree -Value { $this.Kills++; if ($this.ExitOnKill) { $this.Exited = $true; $this.ExitCodeValue = -1 } }
    $p | Add-Member -MemberType ScriptMethod -Name ReadOutput -Value { return $this.OutputText }
    $p | Add-Member -MemberType ScriptMethod -Name Close -Value { }
    return $p
}

$global:OsmFake_now = [DateTimeOffset]::Parse('2026-11-26T00:00:00Z')
$global:OsmFake_sleeps = 0
$global:OsmFake_clock = [pscustomobject]@{}
$global:OsmFake_clock | Add-Member -MemberType ScriptMethod -Name Now -Value { return $global:OsmFake_now }
$global:OsmFake_clock | Add-Member -MemberType ScriptMethod -Name Sleep -Value { param([int]$Milliseconds) $global:OsmFake_now = $global:OsmFake_now.AddMilliseconds($Milliseconds); $global:OsmFake_sleeps += $Milliseconds }

$global:OsmFake_starts = [Collections.Generic.List[object]]::new()
$global:OsmFake_onStart = $null
$global:OsmFake_fakeStarter = {
    param([string]$Executable, [string[]]$Arguments, [string]$WorkingDirectory, [hashtable]$Options)
    $global:OsmFake_starts.Add([pscustomobject]@{ Executable=$Executable; Argv=@($Arguments); WorkingDirectory=$WorkingDirectory; Options=$Options })
    $process = & $global:OsmFake_onStart $Executable $Arguments $WorkingDirectory $Options
    $process.Executable = $Executable; $process.Argv = @($Arguments)
    return $process
}

# 偽の Editor と、その checkout の tools/unity-editor.cmd の代わりの CLI。eval の code から印と照合の PID を読み、
# 偽の Editor の PID と比べて応答する。Plan に積んだ handler が先に応答を決められる。
$global:OsmFake_editor = $null
function Reset-FakeEditor([int]$EditorPid = 5100) {
    $global:OsmFake_editor = [pscustomobject]@{ Pid=$EditorPid; Process=$null; Store=@{}; Calls=[Collections.Generic.List[object]]::new(); Plan=[Collections.Generic.Queue[scriptblock]]::new(); SendVisibleAfterReads=0 }
}
function New-EvalResponse([string]$CallId, [int]$ResponderPid, [string]$Status, [string]$Value) {
    $b64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($Value))
    return [pscustomobject]@{ ExitCode=0; Output="{`"success`":true,`"result`":`"OSMEVAL|$CallId|$ResponderPid|$Status|$b64`"}"; TimedOut=$false }
}
$global:OsmFake_defaultEditor = {
    param($Call, [string]$Root)
    $e = $global:OsmFake_editor
    if ($e.Pid -ne $Call.Expected) { return New-EvalResponse $Call.CallId $e.Pid 'wrong-target' '' }
    switch ($Call.Kind) {
        'ready' { return New-EvalResponse $Call.CallId $e.Pid 'ok' 'ready' }
        'send' {
            $m = [regex]::Match($Call.Code, 'SetString\("([^"]+)", "([^"]+)"\)')
            $e.Store[$m.Groups[1].Value] = $m.Groups[2].Value
            return New-EvalResponse $Call.CallId $e.Pid 'ok' 'set'
        }
        'exit' { $e.Process.Exited = $true; $e.Process.ExitCodeValue = 0; return [pscustomobject]@{ ExitCode=6; Output='Invalid response format'; TimedOut=$false } }
        default {
            if ($Call.Code -like '*unityVersion*') { return New-EvalResponse $Call.CallId $e.Pid 'ok' "6000.6.9f1|$Root\unity" }
            $m = [regex]::Match($Call.Code, 'GetString\("([^"]+)"')
            if ($e.SendVisibleAfterReads -gt 0) { $e.SendVisibleAfterReads--; return New-EvalResponse $Call.CallId $e.Pid 'ok' '' }
            $value = if ($e.Store.ContainsKey($m.Groups[1].Value)) { $e.Store[$m.Groups[1].Value] } else { '' }
            return New-EvalResponse $Call.CallId $e.Pid 'ok' $value
        }
    }
}
$global:OsmFake_fakeCli = {
    param([string]$CheckoutRoot, [string[]]$Arguments, [int]$TimeoutMs)
    $code = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Arguments[1]))
    $callId = [regex]::Match($code, 'OSMEVAL\|([0-9a-f]{32})\|').Groups[1].Value
    $expected = [int][regex]::Match($code, '__osmPid != ([0-9]+)').Groups[1].Value
    $kind = if ($code -like '*EditorApplication.Exit*') { 'exit' } elseif ($code -like '*return "ready";*') { 'ready' } elseif ($code -like '*SetString*') { 'send' } else { 'read' }
    $call = [pscustomobject]@{ Kind=$kind; CallId=$callId; Expected=$expected; Code=$code; Verb=$Arguments[0] }
    $global:OsmFake_editor.Calls.Add($call)
    if ($global:OsmFake_editor.Plan.Count -gt 0) { $handler = $global:OsmFake_editor.Plan.Dequeue(); $r = & $handler $call $CheckoutRoot; if ($null -ne $r) { return $r } }
    return & $global:OsmFake_defaultEditor $call $CheckoutRoot
}
$global:OsmFake_editorStart = { param($e, $a, $w, $o) $p = New-FakeProcess -Id $global:OsmFake_editor.Pid -Never -ExitOnKill; $global:OsmFake_editor.Process = $p; return $p }

function Use-FakeSeams([psobject]$Fx) {
    $unityRoot = Join-Path $Fx.Fixture 'Unity installs'
    $exe = Join-Path $unityRoot '6000.6.9f1/Editor/Unity.exe'
    Write-Text $exe 'fixture'
    $global:OsmFake_starts.Clear()
    $global:OsmFake_now = [DateTimeOffset]::Parse('2026-11-26T00:00:00Z'); $global:OsmFake_sleeps = 0
    Reset-FakeEditor
    Set-ScenarioSeam @{ Starter=$global:OsmFake_fakeStarter; Cli=$global:OsmFake_fakeCli; Clock=$global:OsmFake_clock; UnityRoot=$unityRoot; UnityExe=''; ProcessQuery={ @() } }
    return $exe
}

# 偽の runner: argv の -OutputRoot に step を書き、marker を出力に含む偽の子の pwsh を返す。
function New-RunnerStart([hashtable]$Step = @{}, [switch]$NoMarker, [string]$StaleStepPath = '', [int]$ChildExitCode = 0, [scriptblock]$OnWait = $null) {
    $global:OsmFake_runnerStep = $Step; $global:OsmFake_runnerNoMarker = [bool]$NoMarker; $global:OsmFake_runnerStale = $StaleStepPath; $global:OsmFake_runnerExit = $ChildExitCode; $global:OsmFake_runnerOnWait = $OnWait
    return {
        param($e, $a, $w, $o)
        $out = $a[[array]::IndexOf($a, '-OutputRoot') + 1]
        $filterIndex = [array]::IndexOf($a, '-Filter')
        $filter = if ($filterIndex -ge 0) { $a[$filterIndex + 1] } else { '' }
        $step = [ordered]@{ schemaVersion=1; name='unity-tests'; kind='test'; status='passed'; projectPath=(Join-Path $w 'unity'); filter=$filter; counts=@{ total=1 }; process=@{ exitCode=0 } }
        foreach ($k in $global:OsmFake_runnerStep.Keys) { $step[$k] = $global:OsmFake_runnerStep[$k] }
        $dir = Join-Path $out 'inv'
        [void][IO.Directory]::CreateDirectory($dir)
        $stepPath = Join-Path $dir 'step.json'
        [IO.File]::WriteAllText($stepPath, (ConvertTo-Json -InputObject $step -Depth 4))
        $markerPath = if ($global:OsmFake_runnerStale) { $global:OsmFake_runnerStale } else { $stepPath }
        $output = if ($global:OsmFake_runnerNoMarker) { "noise`n" } else { "noise`nUNITY_TEST_RESULT $markerPath`n" }
        return New-FakeProcess -Id 4200 -ExitCode $global:OsmFake_runnerExit -Output $output -OnWait $global:OsmFake_runnerOnWait
    }
}

function Get-Events([string]$RunRoot, [string]$Event = '') { return @(Read-Records $RunRoot | Where-Object { $_.Contains('event') -and (-not $Event -or $_.event -ceq $Event) }) }
function Get-InUse([string]$Root) { $p = Join-Path (Get-WorktreeGitDir $Root) 'osm-scenario/in-use.json'; if ([IO.File]::Exists($p)) { return ConvertFrom-Json ([IO.File]::ReadAllText($p)) -AsHashtable }; return $null }

try {
    # --- UnityProcess ---------------------------------------------------------
    Run 'AC7 Start-UnityProcess fixes the PID, makes no window by default and gives environment only to the child' {
        $h = Start-UnityProcess -Executable $script:pwsh -Arguments @('-NoLogo', '-NoProfile', '-Command', '[Console]::Out.Write($env:OSM_SCENARIO_PROBE); exit 7') -Environment @{ OSM_SCENARIO_PROBE='child-only' } -RedirectOutput
        try {
            $w = Wait-UnityProcess -Handle $h
            Assert ($h.Id -gt 0 -and [DateTimeOffset]::Parse($h.StartedAt) -le [DateTimeOffset]::UtcNow -and $h.Process.StartInfo.CreateNoWindow) 'identity or window flag'
            Assert ($w.Signaled -and $w.ExitCode -eq 7 -and $w.ExitCode -is [int] -and $h.ReadOutput() -ceq 'child-only' -and $null -eq $env:OSM_SCENARIO_PROBE) "exit $($w.ExitCode) output $($h.ReadOutput())"
        } finally { Close-UnityProcess $h }
        $h2 = Start-UnityProcess -Executable $script:pwsh -Arguments @('-NoLogo', '-NoProfile', '-Command', 'exit 0') -CreateWindow
        try { [void](Wait-UnityProcess -Handle $h2); Assert (-not $h2.Process.StartInfo.CreateNoWindow) 'CreateWindow ignored' } finally { Close-UnityProcess $h2 }
    }

    Run 'AC7 runner process seam waits without a deadline and returns an integer exit code' {
        $fake = New-FakeProcess -ExitCode 3
        $w = Wait-UnityProcess -Handle $fake
        Assert ($fake.WaitArgs.Count -eq 1 -and $fake.WaitArgs[0] -eq -1 -and $w.ExitCode -eq 3) 'no-deadline wait'
        $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repoUnderTest 'tools/run-tests.ps1'), [ref]$null, [ref]$null)
        $fn = $ast.Find({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq 'Invoke-UnityTestProcess' }, $true)
        $waits = @($fn.FindAll({ param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -ceq 'Wait-UnityProcess' }, $true))
        Assert ($waits.Count -eq 1 -and $waits[0].Extent.Text -notmatch 'TimeoutMs') 'Invoke-UnityTestProcess waits with a deadline'
        $result = & { . (Join-Path $repoUnderTest 'tools/run-tests.ps1'); Invoke-UnityTestProcess -Executable $script:pwsh -Arguments @('-NoLogo', '-NoProfile', '-Command', 'exit 5') -WorkingDirectory $repoUnderTest }
        Assert ($result.ExitCode -eq 5 -and $result.ExitCode -is [int] -and $result.Failure -ceq '' -and $result.Id -gt 0 -and $result.StartedAt) 'real child exit code'
    }

    Run 'AC7 runner process seam keeps a null exit code when the start fails' {
        $missing = Join-Path $script:tempRoot 'no-such-unity.exe'
        $result = & { . (Join-Path $repoUnderTest 'tools/run-tests.ps1'); Invoke-UnityTestProcess -Executable $missing -Arguments @() -WorkingDirectory $repoUnderTest }
        Assert ($null -eq $result.ExitCode -and $result.Failure -and $null -eq $result.StartedAt -and $null -eq $result.Id) 'start failure was not null'
    }

    Run 'AC7 a deadline wait that does not signal neither kills nor reads the exit code' {
        $fake = New-FakeProcess -Never
        $w = Wait-UnityProcess -Handle $fake -TimeoutMs 50
        Assert (-not $w.Signaled -and $null -eq $w.ExitCode -and $fake.Kills -eq 0 -and $fake.WaitArgs[0] -eq 50) 'deadline wait'
    }

    Run 'AC4 lock check reads without deleting and only the runner clears a stale lock' {
        $project = Join-Path $script:tempRoot 'lockproject'
        $lock = Join-Path $project 'Temp/UnityLockfile'
        Write-Text $lock 'stale'
        $state = Get-UnityProjectLockState $project
        Assert ($state.Exists -and -not $state.Held -and [IO.File]::Exists($lock)) 'stale lock state or deletion'
        $stream = [IO.File]::Open($lock, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try {
            Assert ((Get-UnityProjectLockState $project).Held) 'held lock not detected'
            $threw = $false; try { Clear-UnityStaleProjectLock $project } catch { $threw = $true }
            Assert ($threw -and [IO.File]::Exists($lock)) 'held lock cleared'
        } finally { $stream.Dispose() }
        Clear-UnityStaleProjectLock $project
        Assert (-not [IO.File]::Exists($lock)) 'stale lock not cleared by the runner function'
        $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $repoUnderTest 'tools/run-tests.ps1'), [ref]$null, [ref]$null)
        Assert (@($ast.FindAll({ param($n) $n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -ceq 'Clear-UnityStaleProjectLock' }, $true)).Count -eq 1) 'runner lost its stale lock cleanup'
    }

    # --- run-tests.ps1 の子の起動 ---------------------------------------------
    Run 'Op4 AC5 the current step is accepted, the PID is recorded before the wait and environment goes to the child' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $x = New-ManualWorktree $fx 'x' -Register
        $script:inUseSeen = $null
        $global:OsmFake_onStart = New-RunnerStart -OnWait { param($p) if ($null -eq $script:inUseSeen) { $script:inUseSeen = Get-InUse $env:OSM_TEST_X } }
        $env:OSM_TEST_X = $x
        $s = New-Scenario -Name 'tr' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Patches 'patches/p1.patch' -Body { param($c) $r = Invoke-ScenarioTests $c -Filter 'Suite.A$' -ExpectedTotal 1 -Environment @{ OSM_CHILD='v' }; @{ status=$r.Status; total=$r.Total } }
        $res = Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x }
        $env:OSM_TEST_X = $null
        $start = $global:OsmFake_starts[0]
        Assert ($res.ExitCode -eq 0 -and $global:OsmFake_starts.Count -eq 1) "exit $($res.ExitCode)"
        Assert ($start.Executable -ceq $script:pwsh -and $start.Argv -contains (Join-Path $x 'tools/run-tests.ps1') -and $start.Argv[[array]::IndexOf($start.Argv, '-Filter') + 1] -ceq 'Suite.A$') 'runner argv'
        Assert ($start.Options.Environment.OSM_CHILD -ceq 'v' -and $null -eq $env:OSM_CHILD -and $start.Options.RedirectOutput) 'environment'
        Assert ($null -ne $script:inUseSeen -and @($script:inUseSeen.processes | Where-Object { $_.pid -eq 4200 -and $_.kind -ceq 'test-runner' }).Count -eq 1) 'PID not in the in-use record before the wait'
        $tr = @(Get-Events $res.RunRoot 'test-run')[0]
        Assert ($tr.stepIsCurrent -and $tr.terminationConfirmed -and $tr.total -eq 1 -and $tr.outputRoot -ceq (Join-Path $res.RunRoot 'base/a/1/runner-1') -and $tr.stepPath -like "$($tr.outputRoot)*") 'test-run record'
        Assert ((Read-Bytes (Join-Path $x 'data/value.txt')) -ceq "one`n" -and $null -eq (Get-InUse $x)) 'not restored'
    }

    Run 'AC5 AC3 a stale, missing or unconfirmed step quarantines the checkout without restoring it' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $old = Join-Path $fx.Fixture 'old/step.json'
        Write-Text $old '{"status":"passed","projectPath":"x","filter":"","counts":{"total":1},"process":{"exitCode":0}}'
        $variants = [ordered]@{
            stale = @{ Stale=$old }
            nomarker = @{ NoMarker=$true }
            nullexit = @{ Step=@{ process=@{ exitCode=$null } } }
            filter = @{ Step=@{ filter='Other$' } }
            project = @{ Step=@{ projectPath='C:\elsewhere\unity' } }
        }
        foreach ($name in $variants.Keys) {
            $x = New-ManualWorktree $fx "x-$name" -Register
            $v = $variants[$name]
            $global:OsmFake_onStart = New-RunnerStart -Step $(if ($v.ContainsKey('Step')) { $v.Step } else { @{} }) -NoMarker:([bool]$v['NoMarker']) -StaleStepPath $(if ($v.ContainsKey('Stale')) { $v.Stale } else { '' })
            $s = New-Scenario -Name "q$name" -Checkout 'X'
            Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Patches 'patches/p1.patch' -Body { param($c) Invoke-ScenarioTests $c -Filter 'Suite.A$' -ExpectedTotal 1 }
            Add-ScenarioCase $s -Name 'b' -Checkout 'X' -Body { throw 'must not run' }
            $res = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
            $events = Get-Events $res.Result.RunRoot
            Assert ($res.Result.ExitCode -eq 1) "$name exit"
            Assert ((Read-Bytes (Join-Path $x 'data/value.txt')) -ceq "two`n" -and $null -ne (Get-InUse $x)) "$name restored without a confirmed exit"
            Assert (@($events | Where-Object { $_.event -ceq 'quarantine' -and $_.pid -eq 4200 -and $_.restored -eq $false }).Count -eq 1 -and @($events | Where-Object { $_.event -ceq 'restore' }).Count -eq 0 -and @($events | Where-Object { $_.case -ceq 'b' }).Count -eq 0) "$name records"
            $next = Invoke-Captured { Invoke-Scenario $s -RunId 'r2' -CheckoutRoots @{ X=$x } }
            Assert ($next.Result.ExitCode -eq 1 -and $next.Err -like '*使用中の記録*') "$name next run was not refused"
        }
    }

    Run 'AC5 a preflight step is a confirmed exit but zero or unexpected counts fail the case' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $x = New-ManualWorktree $fx 'x' -Register
        $global:OsmFake_onStart = New-RunnerStart -Step @{ name='path-budget'; kind='preflight'; status='failed'; counts=$null; process=$null }
        $s = New-Scenario -Name 'pf' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Patches 'patches/p1.patch' -Body { param($c) Invoke-ScenarioTests $c }
        $res = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        $end = @(Get-Events $res.Result.RunRoot 'case-end')[0]
        Assert ($res.Result.ExitCode -eq 1 -and $end.base -ceq 'test-run' -and (Read-Bytes (Join-Path $x 'data/value.txt')) -ceq "one`n" -and $null -eq (Get-InUse $x)) 'preflight step handling'
        $global:OsmFake_onStart = New-RunnerStart -Step @{ counts=@{ total=2 } }
        $s2 = New-Scenario -Name 'pf2' -Checkout 'X'
        Add-ScenarioCase $s2 -Name 'a' -Checkout 'X' -Body { param($c) Invoke-ScenarioTests $c -Filter 'A$' -ExpectedTotal 1 }
        $r2 = Invoke-Captured { Invoke-Scenario $s2 -RunId 'r' -CheckoutRoots @{ X=$x } }
        Assert ($r2.Result.ExitCode -eq 1 -and $r2.Err -like '*実行件数 2*') 'count mismatch accepted'
        $global:OsmFake_onStart = New-RunnerStart -Step @{ status='failed' }
        $s3 = New-Scenario -Name 'pf3' -Checkout 'X'
        Add-ScenarioCase $s3 -Name 'a' -Checkout 'X' -Body { param($c) (Invoke-ScenarioTests $c -Filter 'A$' -ExpectedTotal 1).Status }
        $r3 = Invoke-Scenario $s3 -RunId 'r' -CheckoutRoots @{ X=$x }
        Assert ($r3.ExitCode -eq 0 -and @(Get-Events $r3.RunRoot 'case-end')[0].body -ceq 'failed') 'a failed product test counted as a base failure'
        $s4 = New-Scenario -Name 'pf4' -Checkout 'X'
        Add-ScenarioCase $s4 -Name 'a' -Checkout 'X' -Body { param($c) Invoke-ScenarioTests $c -Filter 'A$' -KillOnTimeout }
        $r4 = Invoke-Captured { Invoke-Scenario $s4 -RunId 'r' -CheckoutRoots @{ X=$x } }
        Assert ($r4.Result.ExitCode -eq 1 -and $r4.Err -like '*止めません*') 'kill request for the runner accepted'
    }

    # --- batch・外部 command・補助の process・任意の観測 -----------------------
    Run 'Op10 batch start passes -executeMethod, keeps the log in the attempt and records the exit code' {
        $fx = New-FixtureRepo; $exe = Use-FakeSeams $fx
        $x = New-ManualWorktree $fx 'x' -Register
        $global:OsmFake_onStart = { param($e, $a, $w, $o) New-FakeProcess -Id 4300 -ExitCode 0 -ExitAfter 1 }
        $s = New-Scenario -Name 'batch' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Body { param($c) $script:batch = Invoke-ScenarioBatch $c -Method 'Fixture.Build.Run' -Arguments @('-custom', 'v') -TimeoutMs 1000; $script:batch.ExitCode }
        $res = Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x }
        $argv = $global:OsmFake_starts[0].Argv
        $log = Join-Path $res.RunRoot 'base/a/1/batch-1.log'
        Assert ($res.ExitCode -eq 0 -and $global:OsmFake_starts[0].Executable -ceq $exe -and -not $global:OsmFake_starts[0].Options.CreateWindow) 'executable or window'
        Assert ((($argv[0..2]) -join ' ') -ceq '-batchmode -nographics -quit' -and $argv[[array]::IndexOf($argv, '-projectPath') + 1] -ceq (Join-Path $x 'unity') -and $argv[[array]::IndexOf($argv, '-executeMethod') + 1] -ceq 'Fixture.Build.Run' -and $argv[[array]::IndexOf($argv, '-logFile') + 1] -ceq $log -and $argv[-1] -ceq 'v') "argv $($argv -join ' ')"
        $exit = @(Get-Events $res.RunRoot 'process-exit')[0]
        Assert ($exit.pid -eq 4300 -and $exit.exitCode -eq 0 -and $exit.signaled -and -not $exit.forced -and $script:batch.LogPath -ceq $log) 'process-exit record'
    }

    Run 'AC3 an unexited batch is quarantined and only a declared deadline kills the held process tree' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $x = New-ManualWorktree $fx 'x' -Register
        $script:held = $null
        $global:OsmFake_onStart = { param($e, $a, $w, $o) $script:held = New-FakeProcess -Id 4400 -Never -ExitOnKill; $script:held }
        $s = New-Scenario -Name 'hang' -Checkout 'X'
        Add-ScenarioCase $s -Name 'a' -Checkout 'X' -Patches 'patches/p1.patch' -Body { param($c) Invoke-ScenarioBatch $c -Method 'M' -TimeoutMs 10 }
        $res = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        Assert ($res.Result.ExitCode -eq 1 -and $script:held.Kills -eq 0 -and (Read-Bytes (Join-Path $x 'data/value.txt')) -ceq "two`n") 'unexited batch was killed or restored'
        $inUse = Get-InUse $x
        Assert (@($inUse.processes | Where-Object { $_.pid -eq 4400 -and $_.kind -ceq 'batch' }).Count -eq 1) 'in-use record lacks the batch PID'
        Invoke-Git $x @('checkout', '--', '.') | Out-Null
        $rel = Invoke-ScenarioCheckoutOperation -Operation release -Path $x -RepoRoot $fx.Repo -ProcessQuery { @() }
        Assert $rel.Ok "release: $($rel.Problems -join '; ')"
        $s2 = New-Scenario -Name 'hang2' -Checkout 'X'
        Add-ScenarioCase $s2 -Name 'a' -Checkout 'X' -Patches 'patches/p1.patch' -Body { param($c) Invoke-ScenarioBatch $c -Method 'M' -TimeoutMs 10 -KillOnTimeout }
        $res2 = Invoke-Scenario $s2 -RunId 'r' -CheckoutRoots @{ X=$x }
        $exit = @(Get-Events $res2.RunRoot 'process-exit')[0]
        Assert ($res2.ExitCode -eq 0 -and $script:held.Kills -eq 1 -and $exit.forced -and $exit.exitCode -eq -1 -and (Read-Bytes (Join-Path $x 'data/value.txt')) -ceq "one`n") 'declared kill'
    }

    Run 'Op15 an external command records arguments, exit code, output and time' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $global:OsmFake_onStart = { param($e, $a, $w, $o) New-FakeProcess -Id 4500 -ExitCode 3 -Output 'hook output' }
        $s = New-Scenario -Name 'cmd' -Checkout @()
        Add-ScenarioCase $s -Name 'hook' -Body { param($c) $script:cmd = Invoke-ScenarioCommand $c -Executable 'hook.exe' -Arguments @('--flag', 'a b') -TimeoutMs 100; $script:cmd.ExitCode }
        $res = Invoke-Scenario $s -RunId 'r'
        $rec = @(Get-Events $res.RunRoot 'command')[0]
        Assert ($res.ExitCode -eq 0 -and $rec.executable -ceq 'hook.exe' -and (@($rec.argv) -join '|') -ceq '--flag|a b' -and $rec.exitCode -eq 3 -and $rec.startedUtc -and $rec.endedUtc) 'command record'
        Assert ((Read-Bytes $rec.outputPath) -ceq 'hook output' -and $script:cmd.Output -ceq 'hook output' -and $global:OsmFake_starts[0].Options.RedirectOutput) 'command output'
    }

    Run 'Op11 a helper lives for the invocation and its exit is confirmed at the end' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $script:helper = $null
        $global:OsmFake_onStart = { param($e, $a, $w, $o) $script:helper = New-FakeProcess -Id 4600 -ExitAfter 0; $script:helper }
        $s = New-Scenario -Name 'helper' -Checkout @()
        Add-ScenarioCase $s -Name 'start' -Body { param($c) (Start-ScenarioHelper $c -Executable 'helper.exe' -TimeoutMs 100).Pid }
        Add-ScenarioCase $s -Name 'later' -Body { param($c) $script:helper.Waits }
        $res = Invoke-Scenario $s -RunId 'r'
        $later = @(Get-Events $res.RunRoot 'case-end' | Where-Object { $_.case -ceq 'later' })[0]
        $exit = @(Get-Events $res.RunRoot 'process-exit')[-1]
        Assert ($res.ExitCode -eq 0 -and $later.body -eq 0 -and $exit.pid -eq 4600 -and $exit.signaled) 'helper waited per case or not confirmed at the end'
        $global:OsmFake_onStart = { param($e, $a, $w, $o) $script:helper = New-FakeProcess -Id 4601 -Never; $script:helper }
        $s2 = New-Scenario -Name 'helper2' -Checkout @()
        Add-ScenarioCase $s2 -Name 'start' -Body { param($c) [void](Start-ScenarioHelper $c -Executable 'helper.exe' -TimeoutMs 100) }
        $res2 = Invoke-Captured { Invoke-Scenario $s2 -RunId 'r' }
        $exit2 = @(Get-Events $res2.Result.RunRoot 'process-exit')[-1]
        Assert ($res2.Result.ExitCode -eq 1 -and -not $exit2.signaled -and $script:helper.Kills -eq 0) 'an unexited helper passed'
    }

    Run 'Op11 AC3 a helper started in a checkout case is confirmed before the restore and an unexited one quarantines the checkout' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $value = Join-Path $e 'data/value.txt'
        # 終わる補助の process: case の終わり（戻す前）に終了を確かめ、それから戻す。
        $global:OsmFake_onStart = { param($x, $a, $w, $o) $script:helper = New-FakeProcess -Id 4610 -ExitAfter 0; $script:helper }
        $s = New-Scenario -Name 'helperco' -Checkout 'E'
        Add-ScenarioCase $s -Name 'run' -Checkout 'E' -Patches 'patches/p1.patch' -Body { param($c) [void](Start-ScenarioHelper $c -Executable 'helper.exe' -WorkingDirectory $c.CheckoutRoot -TimeoutMs 100) }
        $ok = Invoke-Scenario $s -RunId 'ok' -CheckoutRoots @{ E=$e }
        $order = @(Get-Events $ok.RunRoot | ForEach-Object { $_.event })
        Assert ($ok.ExitCode -eq 0 -and (Read-Bytes $value) -ceq "one`n" -and $null -eq (Get-InUse $e)) "exited helper: $($ok.ExitCode)"
        Assert ([array]::IndexOf($order, 'process-exit') -ge 0 -and [array]::IndexOf($order, 'process-exit') -lt [array]::IndexOf($order, 'restore') -and @(Get-Events $ok.RunRoot 'process-exit').Count -eq 1) "order: $($order -join ',')"
        # 終わらない補助の process: 戻さず、使用中の記録に PID を残して止まり、次の run にも使わせない。
        $global:OsmFake_onStart = { param($x, $a, $w, $o) $script:helper = New-FakeProcess -Id 4611 -Never; $script:helper }
        $bad = Invoke-Captured { Invoke-Scenario $s -RunId 'bad' -CheckoutRoots @{ E=$e } }
        $events = @(Get-Events $bad.Result.RunRoot)
        $q = @($events | Where-Object { $_.event -ceq 'quarantine' })
        $inUse = Get-InUse $e
        Assert ($bad.Result.ExitCode -eq 1 -and (Read-Bytes $value) -ceq "two`n" -and $script:helper.Kills -eq 0) 'an unexited helper was restored over or killed'
        Assert ($null -ne $inUse -and @($inUse.processes | Where-Object { $_.pid -eq 4611 -and $_.kind -ceq 'helper' }).Count -eq 1) 'in-use record lost the helper'
        Assert ($q.Count -eq 1 -and $q[0].kind -ceq 'helper' -and $q[0].pid -eq 4611 -and $q[0].checkout -ceq 'E' -and -not $q[0].restored) 'quarantine record'
        Assert (@($events | Where-Object { $_.event -ceq 'restore' -or ($_.event -ceq 'in-use' -and $_.state -ceq 'cleared') }).Count -eq 0) 'restored or cleared after an unexited helper'
        Assert (@($events | Where-Object { $_.event -ceq 'process-exit' -and $_.pid -eq 4611 }).Count -eq 1) 'helper exit recorded twice or not at all'
        $next = Invoke-Captured { Invoke-Scenario $s -RunId 'next' -CheckoutRoots @{ E=$e } }
        Assert ($next.Result.ExitCode -eq 1 -and $next.Err -like '*使用中の記録*' -and (Read-Bytes $value) -ceq "two`n") 'the next run used the quarantined checkout'
    }

    Run 'Op11 AC3 an unexited helper from a case without a checkout quarantines every checkout of the invocation' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $x = New-ManualWorktree $fx 'x' -Register
        $global:OsmFake_onStart = { param($e, $a, $w, $o) $script:helper = New-FakeProcess -Id 4620 -Never; $script:helper }
        $s = New-Scenario -Name 'helperall' -Checkout 'X'
        Add-ScenarioCase $s -Name 'use' -Checkout 'X' -Patches 'patches/p1.patch'
        Add-ScenarioCase $s -Name 'start' -Body { param($c) [void](Start-ScenarioHelper $c -Executable 'helper.exe' -TimeoutMs 100) }
        $res = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ X=$x } }
        $inUse = Get-InUse $x
        $q = @(Get-Events $res.Result.RunRoot 'quarantine')
        Assert ($res.Result.ExitCode -eq 1 -and (Read-Bytes (Join-Path $x 'data/value.txt')) -ceq "one`n") 'case restore or exit code'
        Assert ($null -ne $inUse -and $inUse.case -ceq 'start' -and @($inUse.processes | Where-Object { $_.pid -eq 4620 }).Count -eq 1) 'checkout not quarantined for the helper'
        Assert ($q.Count -eq 1 -and $q[0].checkout -ceq 'X' -and $q[0].kind -ceq 'helper' -and $q[0].case -ceq 'start' -and $q[0].restored) 'quarantine record'
        $next = Invoke-Captured { Invoke-Scenario $s -RunId 'r2' -CheckoutRoots @{ X=$x } }
        Assert ($next.Result.ExitCode -eq 1 -and $next.Err -like '*使用中の記録*') 'the next run used the checkout'
        $released = Invoke-ScenarioCheckoutOperation -Operation release -Path $x -RepoRoot $fx.Repo -ProcessQuery { @() }
        Assert ($released.Ok) "release: $($released.Problems -join '; ')"
        # 例外で呼び出しを抜ける時も、補助の process の終了を確かめ、確かめられなければ隔離する。
        $script:recordLock = $null
        Add-ScenarioCase $s -Name 'boom' -Body { param($c) $script:recordLock = [IO.File]::Open((Join-Path $c.RunRoot 'driver.jsonl'), 'Open', 'ReadWrite', 'None') }
        $global:OsmFake_onStart = { param($e, $a, $w, $o) $script:helper = New-FakeProcess -Id 4621 -Never; $script:helper }
        try { $thrown = Invoke-Captured { Invoke-Scenario $s -RunId 'r3' -CheckoutRoots @{ X=$x } } } finally { if ($null -ne $script:recordLock) { $script:recordLock.Dispose() } }
        $inUse3 = Get-InUse $x
        Assert ($thrown.Result.ExitCode -eq 1 -and $null -ne $inUse3 -and @($inUse3.processes | Where-Object { $_.pid -eq 4621 }).Count -eq 1) "exception path did not quarantine: $($thrown.Err)"
    }

    Run 'Op18 a body observation is saved as evidence of the case' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $s = New-Scenario -Name 'observe' -Checkout @()
        Add-ScenarioCase $s -Name 'handles' -Body { param($c) $observed = & { 'handle-count=12' }; Save-ScenarioArtifact $c -Name 'handles.txt' -Content $observed }
        $res = Invoke-Scenario $s -RunId 'r'
        Assert ($res.ExitCode -eq 0 -and (Read-Bytes (Join-Path $res.RunRoot 'evidence/handles/1/handles.txt')) -ceq 'handle-count=12') 'observation evidence'
    }

    # --- 窓のある Editor -------------------------------------------------------
    function New-EditorScenario([string]$Name, [scriptblock]$Body, [string[]]$Patches = @()) {
        $s = New-Scenario -Name $Name -Checkout 'E'
        Add-ScenarioCase $s -Name 'editor' -Checkout 'E' -Patches $Patches -Body $Body
        return $s
    }

    Run 'Op8 Op19 AC12 start, ready by PID, read, one send, read back and exit by the held handle' {
        $fx = New-FixtureRepo; $exe = Use-FakeSeams $fx
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        $s = New-EditorScenario 'ed' {
            param($c)
            $session = Start-ScenarioEditor $c -PollMs 1000
            $script:identity = Read-ScenarioEditor $c $session -Code 'return UnityEngine.Application.unityVersion;'
            [void](Send-ScenarioEditor $c $session -Code 'UnityEditor.SessionState.SetString("k", "token-1"); return "set";')
            $script:readBack = Read-ScenarioEditor $c $session -Code 'return UnityEditor.SessionState.GetString("k", "");'
            $script:stop = Stop-ScenarioEditor $c $session
        }
        $res = Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ E=$e }
        Assert ($res.ExitCode -eq 0 -and $script:readBack -ceq 'token-1' -and $script:identity -ceq "6000.6.9f1|$e\unity" -and $script:stop.ExitCode -eq 0 -and -not $script:stop.Forced) "exit $($res.ExitCode)"
        $start = $global:OsmFake_starts[0]
        Assert ($start.Executable -ceq $exe -and $start.Options.CreateWindow -and $start.Argv -notcontains '-batchmode' -and $start.Argv[[array]::IndexOf($start.Argv, '-projectPath') + 1] -ceq (Join-Path $e 'unity') -and $start.Argv[[array]::IndexOf($start.Argv, '-logFile') + 1] -ceq (Join-Path $res.RunRoot 'base/editor/1/editor-1.log')) 'editor argv'
        $calls = $global:OsmFake_editor.Calls
        Assert ((@($calls | ForEach-Object { $_.Kind }) -join ',') -ceq 'ready,read,send,read,exit' -and @($calls | Where-Object { $_.Expected -ne 5100 -or $_.Verb -cne 'eval' }).Count -eq 0) "CLI calls $(@($calls | ForEach-Object { $_.Kind }) -join ',')"
        $events = @(Get-Events $res.RunRoot | Where-Object { $_.event -in 'editor-start', 'editor-ready', 'eval', 'process-exit', 'restore' } | ForEach-Object { if ($_.event -ceq 'eval') { "eval:$($_.kind)" } else { $_.event } })
        Assert (($events -join ',') -ceq 'editor-start,editor-ready,eval:read,eval:send,eval:read,eval:exit,process-exit,restore') "events $($events -join ',')"
        $evals = @(Get-Events $res.RunRoot 'eval')
        foreach ($ev in $evals) {
            $raw = ConvertFrom-Json (Read-Bytes $ev.responsePath) -AsHashtable
            Assert ($raw.callId -ceq $ev.callId -and (Read-Bytes $ev.codePath) -like "*__osmPid != 5100*") "eval evidence $($ev.kind)"
        }
        Assert (@($evals | Where-Object { $_.kind -ceq 'read' -and $_.answered -and $_.evalPid -eq 5100 }).Count -eq 2) 'read records'
        $ready = @(Get-Events $res.RunRoot 'editor-ready')[0]
        $exit = @(Get-Events $res.RunRoot 'process-exit')[0]
        Assert ($ready.pid -eq 5100 -and $ready.evalPid -eq 5100 -and $exit.pid -eq 5100 -and $exit.exitCode -eq 0 -and $exit.signaled -and -not $exit.forced) 'ready and exit records'
    }

    Run 'Op8 AC12 ready waits through busy responses and a stale lock, and refuses a held lock' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        foreach ($i in 1..3) { $global:OsmFake_editor.Plan.Enqueue({ param($call, $root) [pscustomobject]@{ ExitCode=6; Output='503 Service Unavailable: Server Busy'; TimedOut=$false } }) }
        $stale = Join-Path $e 'unity/Temp/UnityLockfile'
        Write-Text $stale 'stale'
        $s = New-EditorScenario 'busy' { param($c) $session = Start-ScenarioEditor $c -PollMs 1000; [void](Stop-ScenarioEditor $c $session) }
        $res = Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ E=$e }
        Assert ($res.ExitCode -eq 0 -and @($global:OsmFake_editor.Calls | Where-Object { $_.Kind -ceq 'ready' }).Count -eq 4 -and $global:OsmFake_sleeps -ge 3000 -and [IO.File]::Exists($stale)) 'busy ready or stale lock handling'
        $global:OsmFake_starts.Clear()
        $s2 = New-EditorScenario 'held' { param($c) [void](Start-ScenarioEditor $c) }
        $stream = [IO.File]::Open($stale, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try { $r2 = Invoke-Captured { Invoke-Scenario $s2 -RunId 'r' -CheckoutRoots @{ E=$e } } } finally { $stream.Dispose() }
        Assert ($r2.Result.ExitCode -eq 1 -and $global:OsmFake_starts.Count -eq 0) 'started against a held lock'
    }

    Run 'AC12 another Editor answering is a start failure and nothing is sent to it' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = { param($a1, $a2, $a3, $a4) $p = New-FakeProcess -Id 5100 -Never -ExitOnKill; $global:OsmFake_editor.Process = $p; $global:OsmFake_editor.Pid = 7777; $p }
        $s = New-EditorScenario 'other' { param($c) $session = Start-ScenarioEditor $c; [void](Send-ScenarioEditor $c $session -Code 'UnityEditor.SessionState.SetString("k", "x"); return "set";') } -Patches 'patches/p1.patch'
        $res = Invoke-Captured { Invoke-Scenario $s -RunId 'r' -CheckoutRoots @{ E=$e } }
        $kinds = @($global:OsmFake_editor.Calls | ForEach-Object { $_.Kind })
        Assert ($res.Result.ExitCode -eq 1 -and $kinds -notcontains 'send' -and $global:OsmFake_editor.Store.Count -eq 0 -and $res.Err -like '*7777*') "kinds $($kinds -join ',')"
        # 終了の要求も PID の照合で相手に効かないので、起動した process の終了は確かめられず、checkout は隔離される。
        Assert ((Read-Bytes (Join-Path $e 'data/value.txt')) -ceq "two`n" -and $null -ne (Get-InUse $e) -and $global:OsmFake_editor.Process.Kills -eq 0) 'checkout restored or process killed'
    }

    Run 'AC12 a send is never resent after a timeout or an error' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        $script:mode = 'executed-timeout'
        $body = {
            param($c)
            $session = Start-ScenarioEditor $c
            $global:OsmFake_editor.Plan.Enqueue({ param($call, $root)
                if ($script:mode -ceq 'executed-timeout') { [void](& $global:OsmFake_defaultEditor $call $root); return [pscustomobject]@{ ExitCode=$null; Output=''; TimedOut=$true } }
                return [pscustomobject]@{ ExitCode=1; Output='connection refused'; TimedOut=$false } })
            $script:sent = Send-ScenarioEditor $c $session -Code 'UnityEditor.SessionState.SetString("k", "v1"); return "set";'
            $script:after = Read-ScenarioEditor $c $session -Code 'return UnityEditor.SessionState.GetString("k", "");'
            [void](Stop-ScenarioEditor $c $session)
        }
        $res = Invoke-Scenario (New-EditorScenario 'once1' $body) -RunId 'r' -CheckoutRoots @{ E=$e }
        Assert ($res.ExitCode -eq 0 -and @($global:OsmFake_editor.Calls | Where-Object { $_.Kind -ceq 'send' }).Count -eq 1 -and -not $script:sent.Answered -and $script:after -ceq 'v1') 'executed-then-timeout send'
        Use-FakeSeams $fx | Out-Null
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        $script:mode = 'error'
        $res2 = Invoke-Scenario (New-EditorScenario 'once2' $body) -RunId 'r' -CheckoutRoots @{ E=$e }
        Assert ($res2.ExitCode -eq 0 -and @($global:OsmFake_editor.Calls | Where-Object { $_.Kind -ceq 'send' }).Count -eq 1 -and $script:after -ceq '') 'not-executed send was resent'
    }

    Run 'Op14 AC12 a read retries failures, ignores other call ids and stops at the deadline' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        $global:OsmFake_editor.Store['k'] = 'real'
        $body = {
            param($c)
            $session = Start-ScenarioEditor $c
            $global:OsmFake_editor.Plan.Enqueue({ param($call, $root) New-EvalResponse ('0' * 32) 5100 'ok' 'stale-answer' })
            $global:OsmFake_editor.Plan.Enqueue({ param($call, $root) [pscustomobject]@{ ExitCode=6; Output='connection refused (reload)'; TimedOut=$false } })
            $script:value = Read-ScenarioEditor $c $session -Code 'return UnityEditor.SessionState.GetString("k", "");'
            foreach ($i in 1..100) { $global:OsmFake_editor.Plan.Enqueue({ param($call, $root) [pscustomobject]@{ ExitCode=6; Output='busy'; TimedOut=$false } }) }
            $script:threw = $false
            try { [void](Read-ScenarioEditor $c $session -Code 'return "x";' -TimeoutMs 5000) } catch { $script:threw = $true }
            $global:OsmFake_editor.Plan.Clear()
            [void](Stop-ScenarioEditor $c $session)
        }
        $res = Invoke-Scenario (New-EditorScenario 'retry' $body) -RunId 'r' -CheckoutRoots @{ E=$e }
        Assert ($res.ExitCode -eq 0 -and $script:value -ceq 'real' -and $script:threw) "value $($script:value) threw $($script:threw)"
        $reads = @($global:OsmFake_editor.Calls | Where-Object { $_.Kind -ceq 'read' })
        Assert ($reads.Count -ge 5 -and $reads.Count -lt 100) "read count $($reads.Count)"
    }

    Run 'Op8 an Editor that exits before ready fails the start without waiting for the deadline' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = { param($a1, $a2, $a3, $a4) $p = New-FakeProcess -Id 5100 -ExitCode 1 -ExitAfter 0; $global:OsmFake_editor.Process = $p; $p }
        $res = Invoke-Captured { Invoke-Scenario (New-EditorScenario 'early' { param($c) [void](Start-ScenarioEditor $c) }) -RunId 'r' -CheckoutRoots @{ E=$e } }
        $exit = @(Get-Events $res.Result.RunRoot 'process-exit')[0]
        Assert ($res.Result.ExitCode -eq 1 -and $global:OsmFake_editor.Calls.Count -eq 0 -and $global:OsmFake_sleeps -eq 0 -and $exit.exitCode -eq 1 -and $null -eq (Get-InUse $e)) 'early exit handling'
    }

    Run 'Op13 a restart passes only after the first exit is confirmed' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        $body = {
            param($c)
            $first = Start-ScenarioEditor $c
            $script:refused = $false
            try { [void](Start-ScenarioEditor $c) } catch { $script:refused = $true }
            [void](Stop-ScenarioEditor $c $first)
            $global:OsmFake_editor.Pid = 5200
            $second = Start-ScenarioEditor $c
            $script:secondPid = $second.Pid
            [void](Stop-ScenarioEditor $c $second)
        }
        $res = Invoke-Scenario (New-EditorScenario 'restart' $body) -RunId 'r' -CheckoutRoots @{ E=$e }
        Assert ($res.ExitCode -eq 0 -and $script:refused -and $script:secondPid -eq 5200 -and $global:OsmFake_starts.Count -eq 2 -and @(Get-Events $res.RunRoot 'process-exit').Count -eq 2) 'restart rule'
    }

    Run 'Op12 a send returns at once and the state wait polls until the condition holds' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        $body = {
            param($c)
            $session = Start-ScenarioEditor $c
            [void](Send-ScenarioEditor $c $session -Code 'UnityEditor.SessionState.SetString("k", "ready-state"); return "set";')
            $global:OsmFake_editor.SendVisibleAfterReads = 3
            $script:faultBetween = 'fault-injected'
            $script:waited = Wait-ScenarioEditor $c $session -Code 'return UnityEditor.SessionState.GetString("k", "");' -Until { param($v) $v -ceq 'ready-state' } -TimeoutMs 60000
            [void](Stop-ScenarioEditor $c $session)
        }
        $res = Invoke-Scenario (New-EditorScenario 'waitstate' $body) -RunId 'r' -CheckoutRoots @{ E=$e }
        Assert ($res.ExitCode -eq 0 -and $script:waited -ceq 'ready-state' -and @($global:OsmFake_editor.Calls | Where-Object { $_.Kind -ceq 'read' }).Count -eq 4 -and $global:OsmFake_sleeps -ge 6000) 'state wait'
    }

    Run 'Op12b QuietMs sends no read until the quiet time has passed, and omitting it keeps the old behavior' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        $global:OsmFake_readTimes = [Collections.Generic.List[double]]::new()
        $body = {
            param($c)
            $session = Start-ScenarioEditor $c
            [void](Send-ScenarioEditor $c $session -Code 'UnityEditor.SessionState.SetString("k", "ready-state"); return "set";')
            $global:OsmFake_mark = $global:OsmFake_now
            foreach ($i in 1..5) { $global:OsmFake_editor.Plan.Enqueue({ param($call, $root) if ($call.Kind -ceq 'read') { $global:OsmFake_readTimes.Add(($global:OsmFake_now - $global:OsmFake_mark).TotalMilliseconds) }; $null }) }
            [void](Wait-ScenarioEditor $c $session -Code 'return UnityEditor.SessionState.GetString("k", "");' -Until { param($v) $v -ceq 'ready-state' } -QuietMs 45000 -TimeoutMs 120000)
            $global:OsmFake_editor.Plan.Clear()
            $global:OsmFake_mark = $global:OsmFake_now
            $global:OsmFake_editor.Plan.Enqueue({ param($call, $root) if ($call.Kind -ceq 'read') { $global:OsmFake_readTimes.Add(($global:OsmFake_now - $global:OsmFake_mark).TotalMilliseconds) }; $null })
            [void](Wait-ScenarioEditor $c $session -Code 'return UnityEditor.SessionState.GetString("k", "");' -Until { param($v) $v -ceq 'ready-state' })
            $global:OsmFake_editor.Plan.Clear()
            [void](Stop-ScenarioEditor $c $session)
        }
        $res = Invoke-Scenario (New-EditorScenario 'quiet' $body) -RunId 'r' -CheckoutRoots @{ E=$e }
        $times = @($global:OsmFake_readTimes)
        Assert ($res.ExitCode -eq 0 -and $times.Count -eq 2 -and $times[0] -ge 45000 -and $times[1] -eq 0) "read times $($times -join ',')"
    }

    Run 'AC2 a body exception with the Editor open asks it to exit before restoring' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        $res = Invoke-Captured { Invoke-Scenario (New-EditorScenario 'throwing' { param($c) [void](Start-ScenarioEditor $c); throw 'body failed' } -Patches 'patches/p1.patch') -RunId 'r' -CheckoutRoots @{ E=$e } }
        $events = @(Get-Events $res.Result.RunRoot | Where-Object { $_.event -in 'eval', 'process-exit', 'restore' } | ForEach-Object { if ($_.event -ceq 'eval') { "eval:$($_.kind)" } else { $_.event } })
        Assert ($res.Result.ExitCode -eq 1 -and ($events -join ',') -ceq 'eval:exit,process-exit,restore' -and (Read-Bytes (Join-Path $e 'data/value.txt')) -ceq "one`n") "events $($events -join ',')"
    }

    Run 'AC3 an exit not confirmed by the deadline quarantines the checkout until release' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $e = New-ManualWorktree $fx 'e' -Register
        $global:OsmFake_onStart = $global:OsmFake_editorStart
        $body = {
            param($c)
            $session = Start-ScenarioEditor $c
            foreach ($i in 1..200) { $global:OsmFake_editor.Plan.Enqueue({ param($call, $root) [pscustomobject]@{ ExitCode=6; Output='ignored'; TimedOut=$false } }) }
            $script:stop = Stop-ScenarioEditor $c $session -TimeoutMs 30000
            $global:OsmFake_editor.Plan.Clear()
        }
        $res = Invoke-Captured { Invoke-Scenario (New-EditorScenario 'stuck' $body -Patches 'patches/p1.patch') -RunId 'r' -CheckoutRoots @{ E=$e } }
        $q = @(Get-Events $res.Result.RunRoot 'quarantine')
        Assert ($res.Result.ExitCode -eq 1 -and -not $script:stop.Signaled -and $global:OsmFake_editor.Process.Kills -eq 0 -and $q.Count -eq 1 -and $q[0].pid -eq 5100) 'stuck exit handling'
        Assert ((Read-Bytes (Join-Path $e 'data/value.txt')) -ceq "two`n" -and @((Get-InUse $e).processes | Where-Object { $_.pid -eq 5100 -and $_.kind -ceq 'editor' }).Count -eq 1) 'restored or in-use missing'
        $project = Join-Path $e 'unity'
        Invoke-Git $e @('checkout', '--', '.') | Out-Null
        $blocked = Invoke-ScenarioCheckoutOperation -Operation release -Path $e -RepoRoot $fx.Repo -ProcessQuery { @([pscustomobject]@{ ProcessId=5100; ParentProcessId=1; CommandLine="Unity.exe -projectPath $project -logFile x"; CreationDate=(Get-Date) }) }
        Assert (-not $blocked.Ok) 'released while the Editor still runs'
        $released = Invoke-ScenarioCheckoutOperation -Operation release -Path $e -RepoRoot $fx.Repo -ProcessQuery { @() }
        Assert ($released.Ok -and $null -eq (Get-InUse $e)) "release: $($released.Problems -join '; ')"
    }

    Run 'AC15 a composite scenario in the shape of the next slices runs on public functions only' {
        $fx = New-FixtureRepo; [void](Use-FakeSeams $fx)
        $x = New-ManualWorktree $fx 'x' -Register
        $y = New-ManualWorktree $fx 'y' -Register
        # 後続（2 つの checkout、batch、受け渡し、設定 file、patch、Editor の操作と再起動、証拠、続きの実行）を模した
        # scenario。基盤の公開する関数だけで書き、driver の走査を通ることも確かめる。
        $scenarioPath = Join-Path $script:tempRoot 'composite-scenario.ps1'
        Write-Text $scenarioPath @'
param([Parameter(Mandatory)][string]$RunId, [string[]]$Only = @(), [string]$XRoot, [string]$YRoot, [string]$ModuleRoot, [int]$ExitTimeoutMs = 120000)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $ModuleRoot 'Scenario.psm1')
$scenario = New-Scenario -Name 'composite' -Checkout 'X', 'Y'
Add-ScenarioCase $scenario -Name 'build' -Checkout 'X' -Body {
    param($context)
    $batch = Invoke-ScenarioBatch $context -Method 'Fixture.Build.Run' -TimeoutMs 1000
    [void](Save-ScenarioArtifact $context -Name 'build.json' -Content (ConvertTo-Json -InputObject @{ exitCode=$batch.ExitCode }))
    return $batch.ExitCode
}
Add-ScenarioCase $scenario -Name 'handoff' -Body {
    param($context)
    $fixture = Get-ScenarioDirectory $context -Purpose 'fixture'
    Copy-Item -LiteralPath (Join-Path $context.Roots['X'] 'artifacts/out/build.txt') -Destination (Join-Path $fixture 'build.txt')
    return $fixture
}
Add-ScenarioCase $scenario -Name 'configure' -Body {
    param($context)
    $fixture = Get-ScenarioDirectory $context -Purpose 'fixture'
    [void](Set-ScenarioFile $context -Checkout 'Y' -Path 'unity/UserSettings/fixture.json' -Content (ConvertTo-Json -InputObject @{ fixture=$fixture }))
}
Add-ScenarioCase $scenario -Name 'edit' -Checkout 'Y' -Patches 'patches/p1.patch' -Body {
    param($context)
    $patched = Get-Content -Raw -LiteralPath (Join-Path $context.CheckoutRoot 'data/value.txt')
    $session = Start-ScenarioEditor $context
    [void](Send-ScenarioEditor $context $session -Code 'UnityEditor.SessionState.SetString("phase", "applied"); return "set";')
    $state = Wait-ScenarioEditor $context $session -Code 'return UnityEditor.SessionState.GetString("phase", "");' -Until { param($v) $v -ceq 'applied' }
    [void](Stop-ScenarioEditor $context $session -TimeoutMs $script:ExitTimeoutMs)
    $again = Start-ScenarioEditor $context
    $after = Read-ScenarioEditor $context $again -Code 'return UnityEditor.SessionState.GetString("phase", "");'
    [void](Stop-ScenarioEditor $context $again -TimeoutMs $script:ExitTimeoutMs)
    return @{ patched=$patched; state=$state; afterRestart=$after }
}
Add-ScenarioCase $scenario -Name 'collect' -Body {
    param($context)
    [void](Save-ScenarioArtifact $context -Name 'summary.json' -Content '{"collected":true}')
}
Add-ScenarioCase $scenario -Name 'cleanup' -Body {
    param($context)
    [void](Reset-ScenarioFiles $context -Checkout 'Y')
    return (Remove-ScenarioDirectories $context)
}
$result = Invoke-Scenario $scenario -RunId $RunId -Only $Only -CheckoutRoots @{ X=$XRoot; Y=$YRoot }
exit $result.ExitCode
'@
        Assert (@(Get-DriverScanFindings $scenarioPath).Count -eq 0) "composite scan: $(@(Get-DriverScanFindings $scenarioPath) -join '; ')"
        $global:OsmFake_onStart = {
            param($exe, $argv, $wd, $opt)
            if ($argv -contains '-executeMethod') {
                Write-Text (Join-Path $wd 'artifacts/out/build.txt') 'built'
                return New-FakeProcess -Id 4700 -ExitCode 0
            }
            $global:OsmFake_editor.Pid++
            $p = New-FakeProcess -Id $global:OsmFake_editor.Pid -Never -ExitOnKill
            $global:OsmFake_editor.Process = $p
            return $p
        }
        $moduleRoot = Join-Path $repoUnderTest 'tools/Harness'
        function Invoke-Composite([string]$RunId, [string[]]$Only, [int]$ExitTimeoutMs = 120000) {
            $global:LASTEXITCODE = 0
            & $scenarioPath -RunId $RunId -Only $Only -XRoot $x -YRoot $y -ModuleRoot $moduleRoot -ExitTimeoutMs $ExitTimeoutMs *>&1 | Out-Null
            return $global:LASTEXITCODE
        }
        $settings = Join-Path $y 'unity/UserSettings/fixture.json'
        $a1 = Invoke-Composite 'A' @('build', 'handoff', 'configure')
        $rootA = Join-Path $fx.Repo 'TestResults/scenario/composite/A'
        $fixtureDir = [IO.Path]::Combine($fx.Drive, 'ovr', 'composite', 'A', 'fixture')
        Assert ($a1 -eq 0 -and (Read-Bytes (Join-Path $fixtureDir 'build.txt')) -ceq 'built' -and [IO.File]::Exists($settings)) "first part of A: $a1"
        Assert ((Invoke-Git $x @('status', '--porcelain')) -eq $null -and (Invoke-Git $y @('status', '--porcelain')) -eq $null -and (Read-Bytes (Join-Path $rootA 'evidence/build/1/build.json')) -like '*0*') 'checkouts or evidence after the first part'
        $settingsBytes = Read-Bytes $settings
        $b1 = Invoke-Composite 'B' @()
        Assert ($b1 -ne 0 -and -not [IO.Directory]::Exists((Join-Path $fx.Repo 'TestResults/scenario/composite/B')) -and (Read-Bytes $settings) -ceq $settingsBytes -and (Read-Bytes (Join-Path $y 'data/value.txt')) -ceq "one`n") 'run B was not refused without changes'
        $global:OsmFake_editor.Pid = 6000
        $a2 = Invoke-Composite 'A' @('edit', 'collect', 'cleanup')
        $editEnd = @(Get-Events $rootA 'case-end' | Where-Object { $_.case -ceq 'edit' })[0]
        Assert ($a2 -eq 0 -and $editEnd.body.patched -ceq "two`n" -and $editEnd.body.state -ceq 'applied' -and $editEnd.body.afterRestart -ceq 'applied') "continuation of A: $a2"
        Assert (@(Get-Events $rootA 'editor-start').Count -eq 2 -and @(Get-Events $rootA 'process-exit' | Where-Object { $_.kind -ceq 'editor' -and $_.exitCode -eq 0 }).Count -eq 2) 'restart records'
        Assert (-not [IO.File]::Exists($settings) -and -not [IO.Directory]::Exists([IO.Path]::Combine($fx.Drive, 'ovr', 'composite', 'A')) -and (Read-Bytes (Join-Path $y 'data/value.txt')) -ceq "one`n" -and [IO.File]::Exists((Join-Path $rootA 'evidence/collect/1/summary.json'))) 'cleanup of A'
        Assert (@(Get-Events $rootA 'run-start' | Where-Object { $_.continued }).Count -eq 1) 'continuation record'
        $b2 = Invoke-Composite 'B' @()
        Assert ($b2 -eq 0) "run B after A: $b2"
        # Editor が終わらないまま呼び出しが終わる（lease は離れる）→ 次の呼び出しは隔離で拒まれる → 解除の入口は条件を満たす時だけ通る。
        $global:OsmFake_editor.Plan.Clear()
        $global:OsmFake_onStart = {
            param($exe, $argv, $wd, $opt)
            if ($argv -contains '-executeMethod') { Write-Text (Join-Path $wd 'artifacts/out/build.txt') 'built'; return New-FakeProcess -Id 4701 -ExitCode 0 }
            $p = New-FakeProcess -Id $global:OsmFake_editor.Pid -Never
            $global:OsmFake_editor.Process = $p
            return $p
        }
        $global:OsmFake_defaultEditorSaved = $global:OsmFake_defaultEditor
        $global:OsmFake_defaultEditor = { param($Call, [string]$Root) if ($Call.Kind -ceq 'exit') { return [pscustomobject]@{ ExitCode=6; Output='ignored'; TimedOut=$false } }; & $global:OsmFake_defaultEditorSaved $Call $Root }
        try { $c1 = Invoke-Composite 'C' @('edit') 1000 } finally { $global:OsmFake_defaultEditor = $global:OsmFake_defaultEditorSaved }
        Assert ($c1 -ne 0 -and (Read-Bytes (Join-Path $y 'data/value.txt')) -ceq "two`n" -and $null -ne (Get-InUse $y)) "quarantine of C: $c1"
        $c2 = Invoke-Composite 'C' @('edit')
        Assert ($c2 -ne 0) 'quarantined checkout accepted'
        $project = Join-Path $y 'unity'
        $notYet = Invoke-ScenarioCheckoutOperation -Operation release -Path $y -RepoRoot $fx.Repo -ProcessQuery { @() }
        Assert (-not $notYet.Ok) 'released before the checkout was put back'
        Invoke-Git $y @('checkout', '--', '.') | Out-Null
        $live = Invoke-ScenarioCheckoutOperation -Operation release -Path $y -RepoRoot $fx.Repo -ProcessQuery { @([pscustomobject]@{ ProcessId=$global:OsmFake_editor.Pid; ParentProcessId=1; CommandLine="Unity.exe -projectPath $project"; CreationDate=(Get-Date) }) }
        Assert (-not $live.Ok) 'released while the Editor process remains'
        $ok = Invoke-ScenarioCheckoutOperation -Operation release -Path $y -RepoRoot $fx.Repo -ProcessQuery { @() }
        Assert ($ok.Ok -and $null -eq (Get-InUse $y)) "release: $($ok.Problems -join '; ')"
        $global:OsmFake_onStart = { param($exe, $argv, $wd, $opt) $global:OsmFake_editor.Pid++; $p = New-FakeProcess -Id $global:OsmFake_editor.Pid -Never -ExitOnKill; $global:OsmFake_editor.Process = $p; $p }
        $c3 = Invoke-Composite 'C' @('edit')
        $rootC = Join-Path $fx.Repo 'TestResults/scenario/composite/C'
        Assert ($c3 -eq 0 -and @(Get-Events $rootC 'case-start' | Where-Object { $_.case -ceq 'edit' -and $_.attempt -eq 2 }).Count -eq 1) "C after release: $c3"
        # 解除の後の続きの実行は、前の「戻していない」record を指す行を残す。
        $startsC = @(Get-Events $rootC 'run-start')
        Assert ($startsC.Count -eq 2 -and -not $startsC[0].continued -and @($startsC[0].priorQuarantines).Count -eq 0 -and $startsC[1].continued) "run-start lines of C: $(ConvertTo-Json $startsC -Compress -Depth 6)"
        $prior = @($startsC[1].priorQuarantines)
        Assert ($prior.Count -eq 1 -and $prior[0].case -ceq 'edit' -and $prior[0].attempt -eq 1 -and $prior[0].checkout -ceq 'Y' -and $prior[0].kind -ceq 'editor' -and $prior[0].restored -eq $false -and $prior[0].baseDirectory -ceq 'base/edit/1') "prior quarantine fields: $(ConvertTo-Json $prior -Compress)"
        $pointed = ConvertFrom-Json -InputObject ([IO.File]::ReadAllLines((Join-Path $rootC 'driver.jsonl'))[$prior[0].line - 1]) -AsHashtable
        Assert ($pointed.event -ceq 'quarantine' -and $pointed.case -ceq 'edit' -and $pointed.attempt -eq 1 -and $pointed.pid -eq $prior[0].pid -and -not $pointed.restored) 'the pointed line is not the earlier quarantine'
        Assert ((Read-Bytes (Join-Path $rootC "$($prior[0].baseDirectory)/quarantine.status.txt")) -like '*data/value.txt*') 'the pointed base directory has no quarantine snapshot'
    }

    Run 'AC12 eval code checks the PID first and the response is read only by its call id' {
        $code = New-ScenarioEvalCode -ExpectedPid 4321 -CallId ('a' * 32) -Body 'return "v";'
        Assert ($code.IndexOf('__osmPid != 4321') -lt $code.IndexOf('return "v";') -and $code -like '*wrong-target*') 'PID guard is not first'
        $ok = ConvertFrom-ScenarioEvalResponse -Output "x OSMEVAL|$('a' * 32)|4321|ok|$([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('値')))" -CallId ('a' * 32)
        $other = ConvertFrom-ScenarioEvalResponse -Output "OSMEVAL|$('b' * 32)|4321|ok|dg==" -CallId ('a' * 32)
        Assert ($ok.Matched -and $ok.Value -ceq '値' -and $ok.Pid -eq 4321 -and -not $other.Matched) 'response parsing'
        # 値が空の ok（未設定の SessionState を読んだ時）は、空の Base64 から空文字になる。
        $empty = ConvertFrom-ScenarioEvalResponse -Output "OSMEVAL|$('a' * 32)|4321|ok|" -CallId ('a' * 32)
        Assert ($empty.Matched -and $empty.Status -ceq 'ok' -and $empty.Value -ceq '') 'an empty value is not read as an empty string'
    }
} finally {
    $env:OSM_TEST_X = $null
    Complete-ScenarioTestRun 'Scenario Unity'
}
if ($failed.Count -gt 0) { exit 1 }
