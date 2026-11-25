param([string]$ResultPath = '', [string]$Filter = '', [string]$S42Driver = '')
# 基盤の上の driver の offline test: S4-2 の driver の互換（偽の runner で -DryRun でない実行を通す）、
# 後続の形の複合 scenario、driver と基盤の code の走査。
# -S42Driver に base の driver の写しを渡すと、同じ fixture と期待で旧の driver を走らせられる
# （移し替えの前に期待が旧の挙動と合うことを確かめるための入口。旧の driver だけが持たない挙動は飛ばす）。
. (Join-Path $PSScriptRoot 'ScenarioTestSupport.ps1')
$script:driverUnderTest = if ($S42Driver) { [IO.Path]::GetFullPath($S42Driver) } else { Join-Path $repoUnderTest 'tools/s42-player-overlay.ps1' }
$script:legacy = [bool]$S42Driver

$script:s42Patches = [ordered]@{ 'ac01-title.patch'='title'; 'ac12-uicommon.patch'='uicommon'; 'ac16-map.patch'='map'; 'ac15-tag.patch'='tag'; 'ac05-field.patch'='field' }
$script:s42Cases = @(
    @{ Name='X1'; Root='X'; Test='PlayerOverlayEndToEndTests.X1_BuildPlayableAndCompareInputs'; Patches=@() }
    @{ Name='X2'; Root='X'; Test='PlayerOverlayEndToEndTests.X2_CompareInputsInANewProcess'; Patches=@() }
    @{ Name='X-LaunchPlayable'; Root='X'; Test='PlayerKindEndToEndTests.LaunchPlayable'; Patches=@() }
    @{ Name='X-BuildSmoke'; Root='X'; Test='PlayerKindEndToEndTests.BuildSmoke'; Patches=@() }
    @{ Name='X-LaunchSmoke'; Root='X'; Test='PlayerKindEndToEndTests.LaunchSmoke'; Patches=@() }
    @{ Name='X3'; Root='X'; Test='PlayerOverlayEndToEndTests.X3_SmokeRejectsOverlayKeys'; Patches=@() }
    @{ Name='Y01'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y01_EntryBeforeInstall'; Patches=@() }
    @{ Name='Y02'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y02_InstallTransport'; Patches=@() }
    @{ Name='Y03'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y03_EntryWithoutEdits'; Patches=@() }
    @{ Name='Y04'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y04_TitleEdits'; Patches=@('ac01-title.patch') }
    @{ Name='Y05'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y05_ProductObservation'; Patches=@('ac01-title.patch') }
    @{ Name='Y06'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y06_ReuseBuilt'; Patches=@('ac01-title.patch') }
    @{ Name='Y07'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y07_ReuseReused'; Patches=@('ac01-title.patch') }
    @{ Name='Y08'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y08_ReuseAfterAddingAFile'; Patches=@('ac01-title.patch') }
    @{ Name='Y09'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y09_DirectDigestRejections'; Patches=@('ac01-title.patch') }
    @{ Name='Y10'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y10_SecondPlayerWhileFirstRuns'; Patches=@('ac01-title.patch') }
    @{ Name='Y11'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y11_UiCommonAndMapEdits'; Patches=@('ac12-uicommon.patch', 'ac16-map.patch') }
    @{ Name='Y12'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y12_PlayerInputEdit'; Patches=@('ac15-tag.patch') }
    @{ Name='Y13'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y13_ScriptFieldEdit'; Patches=@('ac05-field.patch') }
    @{ Name='Y14'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y14_OverlayForAnotherBase'; Patches=@() }
    @{ Name='Y15'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y15_PairlessPlayer'; Patches=@() }
    @{ Name='Y16'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y16_InheritedEnvironment'; Patches=@() }
    @{ Name='Y17'; Root='Y'; Test='PlayerOverlayEndToEndTests.Y17_UnbuildableOnly'; Patches=@() }
)
$script:s42StepOrder = @('X1', 'X2', 'X-LaunchPlayable', 'X-BuildSmoke', 'X-LaunchSmoke', 'X3', 'copy', 'cache') + @($script:s42Cases | Where-Object { $_.Root -eq 'Y' } | ForEach-Object { $_.Name }) + @('cleanup')

# 偽の runner。checkout の tools/run-tests.ps1 として fixture の repo に入れる。driver の新旧どちらも
# 子の pwsh でこれを起動する。呼ばれた時点の X と Y の編集対象の bytes、環境変数、引数を log に残し、
# plan（filter の末尾の名前ごとの status・件数・marker の有無・nested repo の作成）に従って step を書く。
$script:fakeRunner = @'
param([string]$Filter = '', [string]$OutputRoot = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$plan = @{}
if ($env:FAKE_RUNNER_PLAN -and [IO.File]::Exists($env:FAKE_RUNNER_PLAN)) { $plan = [IO.File]::ReadAllText($env:FAKE_RUNNER_PLAN) | ConvertFrom-Json -AsHashtable }
$name = $Filter.TrimEnd('$').Split('.')[-1]
$entry = @{ status='passed'; total=1; marker=$true; nested=$false }
if ($plan.ContainsKey($name)) { foreach ($k in $plan[$name].Keys) { $entry[$k] = $plan[$name][$k] } }
function Read-Edits([string]$r) { $m = [ordered]@{}; foreach ($f in [IO.Directory]::GetFiles((Join-Path $r 'edits')) | Sort-Object) { $m[[IO.Path]::GetFileName($f)] = [IO.File]::ReadAllText($f) }; return $m }
$settings = Join-Path $env:FAKE_Y_ROOT 'unity/UserSettings/OSMContentDelivery.json'
$call = [ordered]@{ root=$root; filter=$Filter; outputRoot=$OutputRoot; s42=$env:S42_RUN_ID; s41=$env:S41_RUN_ID
    x=(Read-Edits $env:FAKE_X_ROOT); y=(Read-Edits $env:FAKE_Y_ROOT); ySettings=$(if ([IO.File]::Exists($settings)) { [IO.File]::ReadAllText($settings) } else { $null }) }
[IO.File]::AppendAllText($env:FAKE_RUNNER_LOG, ($call | ConvertTo-Json -Compress -Depth 5) + "`n")
if ($name -like 'X1_*') {
    $d = Join-Path $root "TestResults/s42/$env:S42_RUN_ID/$name"
    [void][IO.Directory]::CreateDirectory($d); [IO.File]::WriteAllText((Join-Path $d 'record.json'), '{"x1":true}')
    foreach ($kind in 'players', 'transport') { $p = Join-Path $root "artifacts/playable/$kind/id-0001"; [void][IO.Directory]::CreateDirectory($p); [IO.File]::WriteAllText((Join-Path $p 'file.bin'), $kind) }
}
if ($entry.nested) { & git init -q (Join-Path $root 'nested') | Out-Null }
if (-not $entry.marker) { exit 1 }
$out = if ($OutputRoot) { $OutputRoot } else { Join-Path $root 'TestResults' }
$dir = Join-Path $out ([Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($dir)
$step = [ordered]@{ schemaVersion=1; recordKind='unity-test-step'; name='unity-tests'; kind='test'; status=$entry.status; exitCode=0; projectPath=(Join-Path $root 'unity'); filter=$Filter; counts=@{ total=$entry.total }; process=@{ exitCode=0 } }
$stepPath = Join-Path $dir 'step.json'
[IO.File]::WriteAllText($stepPath, ($step | ConvertTo-Json -Depth 4))
[Console]::Out.WriteLine("UNITY_TEST_RESULT $stepPath")
'@

function New-S42Fixture {
    $fx = New-FixtureRepo -Extra {
        param($repo)
        Write-Text (Join-Path $repo 'tools/run-tests.ps1') $script:fakeRunner
        Write-Text (Join-Path $repo 'unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset') "urp: 1`n"
        foreach ($patch in $script:s42Patches.Keys) { Write-Text (Join-Path $repo "edits/$($script:s42Patches[$patch]).txt") "one`n" }
    }
    $patchDirectory = 'unity/Assets/SampleGame/Tests/Editor/Build/PlayerOverlayEdits'
    foreach ($patch in $script:s42Patches.Keys) {
        $text = New-PatchFromEdit $fx.Repo "edits/$($script:s42Patches[$patch]).txt" "$($script:s42Patches[$patch])`n"
        Write-Text (Join-Path $fx.Repo "$patchDirectory/$patch") $text
    }
    Invoke-Git $fx.Repo @('add', '-A') | Out-Null
    Invoke-Git $fx.Repo @('commit', '-q', '-m', 's42 fixture') | Out-Null
    $fx | Add-Member -NotePropertyName Head -NotePropertyValue ((Invoke-Git $fx.Repo @('rev-parse', 'HEAD')).Trim()) -Force
    $x = New-ManualWorktree $fx 'x' -Register
    $y = New-ManualWorktree $fx 'y' -Register
    # 2 つの checkout の中身を ignore される file で変えておく（どちらの checkout で走ったかを取り違えない）。
    Write-Text (Join-Path $x 'artifacts/which.txt') 'X'
    Write-Text (Join-Path $y 'artifacts/which.txt') 'Y'
    $fx | Add-Member -NotePropertyName X -NotePropertyValue $x
    $fx | Add-Member -NotePropertyName Y -NotePropertyValue $y
    $fx | Add-Member -NotePropertyName Log -NotePropertyValue (Join-Path $fx.Fixture 'runner.jsonl')
    $fx | Add-Member -NotePropertyName Plan -NotePropertyValue (Join-Path $fx.Fixture 'plan.json')
    $env:FAKE_X_ROOT = $x; $env:FAKE_Y_ROOT = $y; $env:FAKE_RUNNER_LOG = $fx.Log; $env:FAKE_RUNNER_PLAN = $fx.Plan
    return $fx
}

function Set-S42Plan([psobject]$Fx, [hashtable]$Plan) { [IO.File]::WriteAllText($Fx.Plan, (ConvertTo-Json -InputObject $Plan -Depth 4)) }
function Read-RunnerLog([psobject]$Fx) { if (-not [IO.File]::Exists($Fx.Log)) { return @() }; return @([IO.File]::ReadAllLines($Fx.Log) | Where-Object { $_ } | ForEach-Object { ConvertFrom-Json -InputObject $_ -AsHashtable }) }
function Clear-RunnerLog([psobject]$Fx) { if ([IO.File]::Exists($Fx.Log)) { [IO.File]::Delete($Fx.Log) } }

function Invoke-S42([psobject]$Fx, [string]$RunId, [string[]]$Only = @(), [switch]$DryRun) {
    # driver を同じ process で走らせる（基盤の seam が効くように）。旧の driver は失敗を例外で、新は exit で返す。
    $arguments = @{ XRoot=$Fx.X; YRoot=$Fx.Y; RunId=$RunId }
    if ($Only.Count -gt 0) { $arguments.Only = $Only }
    if ($DryRun) { $arguments.DryRun = $true }
    $global:LASTEXITCODE = 0
    $original = [Console]::Error
    $capture = [IO.StringWriter]::new()
    try {
        [Console]::SetError($capture)
        & $script:driverUnderTest @arguments *>&1 | Out-Null
        $code = $global:LASTEXITCODE
    } catch { $code = 'throw' } finally { [Console]::SetError($original) }
    return [pscustomobject]@{ Code=$code; Ok=($code -eq 0); Err=$capture.ToString(); RunRoot=(Join-Path $Fx.Y "TestResults/s42/$RunId") }
}

function Get-S42Lines([string]$RunRoot) { return @(Read-Records $RunRoot | Where-Object { $_.Contains('step') -and -not $_.Contains('event') }) }

function Assert-CheckoutsClean([psobject]$Fx, [string]$Label) {
    foreach ($root in $Fx.X, $Fx.Y) {
        Assert ((Invoke-Git $root @('status', '--porcelain')) -eq $null) "$Label status not clean in $root"
        foreach ($name in $script:s42Patches.Values) { Assert ((Read-Bytes (Join-Path $root "edits/$name.txt")) -ceq "one`n") "$Label edit $name left in $root" }
    }
}

try {
    Run 'AC8 S4-2 driver runs every case with the fake runner, patches only its checkout and restores' {
        $fx = New-S42Fixture
        Set-S42Plan $fx @{}
        $r = Invoke-S42 $fx 'full'
        Assert $r.Ok "driver failed: $($r.Code) $($r.Err)"
        $lines = Get-S42Lines $r.RunRoot
        Assert ((@($lines | ForEach-Object { $_.step }) -join ',') -ceq ($script:s42StepOrder -join ',')) "step order $(@($lines | ForEach-Object { $_.step }) -join ',')"
        $calls = @(Read-RunnerLog $fx)
        Assert ($calls.Count -eq $script:s42Cases.Count) "runner calls $($calls.Count)"
        for ($i = 0; $i -lt $script:s42Cases.Count; $i++) {
            $case = $script:s42Cases[$i]
            $ns = if ($case.Test -like 'PlayerKind*') { 'OneStarMaker.Tests.Editor.Build.' } else { 'SampleGame.Tests.Editor.Build.' }
            $filter = $ns + $case.Test
            $line = @($lines | Where-Object { $_.step -ceq $case.Name })[0]
            $call = $calls[$i]
            Assert ($line.root -ceq $case.Root -and $line.filter -ceq $filter -and (@($line.patches) -join ',') -ceq ($case.Patches -join ',')) "$($case.Name) line identity"
            Assert ($line.status -ceq 'passed' -and $line.total -eq 1 -and $line.gitStatusSame -eq $true -and @($line.gitStatusBefore).Count -eq 0 -and @($line.gitStatusAfter).Count -eq 0) "$($case.Name) line result"
            Assert ($line.stepPath -and [IO.File]::Exists($line.stepPath) -and $line.utc) "$($case.Name) stepPath"
            Assert ($line.patches -is [object[]] -and $line.gitStatusBefore -is [object[]] -and $line.total -is [long] -and $line.gitStatusSame -is [bool]) "$($case.Name) key types"
            $own = if ($case.Root -eq 'X') { $fx.X } else { $fx.Y }
            Assert ($call.root -ceq $own -and $call.filter -ceq ($filter + '$') -and $call.s42 -ceq 'full' -and $call.s41 -ceq 'full') "$($case.Name) runner call"
            $mine = if ($case.Root -eq 'X') { $call.x } else { $call.y }
            $other = if ($case.Root -eq 'X') { $call.y } else { $call.x }
            foreach ($patch in $script:s42Patches.Keys) {
                $file = "$($script:s42Patches[$patch]).txt"
                $expected = if ($case.Patches -contains $patch) { "$($script:s42Patches[$patch])`n" } else { "one`n" }
                Assert ($mine[$file] -ceq $expected) "$($case.Name) saw $file = $($mine[$file])"
                Assert ($other[$file] -ceq "one`n") "$($case.Name) changed the other checkout ($file)"
            }
        }
        Assert-CheckoutsClean $fx 'full'
        Assert ([IO.File]::Exists((Join-Path $fx.Y 'artifacts/playable/players/id-0001/file.bin')) -and [IO.File]::Exists((Join-Path $fx.Y 'artifacts/playable/transport/id-0001/file.bin'))) 'copy to Y'
        $copy = @($lines | Where-Object { $_.step -ceq 'copy' })[0]
        $cache = @($lines | Where-Object { $_.step -ceq 'cache' })[0]
        $cleanup = @($lines | Where-Object { $_.step -ceq 'cleanup' })[0]
        Assert ($copy.identity -ceq 'id-0001' -and $cache.settingsPath -ceq (Join-Path $fx.Y 'unity/UserSettings/OSMContentDelivery.json') -and $cleanup.cacheRoot -ceq $cache.cacheRoot -and $cleanup.dryRun -eq $false) 'copy/cache/cleanup lines'
        $y01 = @($calls | Where-Object { $_.filter -like '*Y01_*' })[0]
        Assert ((ConvertFrom-Json $y01.ySettings).cacheRoot -ceq $cache.cacheRoot) 'Y cases did not see the cache settings'
        Assert (-not [IO.Directory]::Exists($cache.cacheRoot)) 'cache root not removed by cleanup'
        if (-not $script:legacy) {
            Assert ($cache.cacheRoot -ceq [IO.Path]::Combine($fx.Drive, 'ovr', 's42', 'full', 'cache')) "cache root rule $($cache.cacheRoot)"
            Assert (-not [IO.File]::Exists($cache.settingsPath) -and -not [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $fx.Y) 'osm-scenario/reservation.json'))) 'cleanup did not put the settings file back'
            Assert ([IO.File]::Exists((Join-Path $r.RunRoot 'evidence/CopyToY/1/x1-record.json'))) 'x1 record evidence'
            foreach ($case in $script:s42Cases) { Assert ($calls[[array]::IndexOf(@($script:s42Cases | ForEach-Object { $_.Name }), $case.Name)].outputRoot -like "$($r.RunRoot)\base\$($case.Name)\1\runner-1") "$($case.Name) output root" }
            Assert (@(Read-Records $r.RunRoot | Where-Object { $_.Contains('event') -and $_.event -ceq 'case-end' -and $_.base -cne 'ok' }).Count -eq 0) 'base failure in a passing run'
        }
    }

    Run 'AC8 S4-2 driver continues after a failed test with one result' {
        $fx = New-S42Fixture
        Set-S42Plan $fx @{ X2_CompareInputsInANewProcess=@{ status='failed'; total=1 } }
        $r = Invoke-S42 $fx 'failedone'
        $lines = Get-S42Lines $r.RunRoot
        Assert ($r.Ok -and @($lines | Where-Object { $_.step -ceq 'X2' })[0].status -ceq 'failed' -and @($lines).Count -eq $script:s42StepOrder.Count) "failed test stopped the run: $($r.Code)"
    }

    Run 'AC8 S4-2 driver writes the line and stops when the count is not one' {
        $fx = New-S42Fixture
        Set-S42Plan $fx @{ X2_CompareInputsInANewProcess=@{ total=0 } }
        $r = Invoke-S42 $fx 'zero'
        $lines = Get-S42Lines $r.RunRoot
        Assert (-not $r.Ok -and (@($lines | ForEach-Object { $_.step }) -join ',') -ceq 'X1,X2' -and @($lines)[1].total -eq 0) "count mismatch: $($r.Code) $(@($lines | ForEach-Object { $_.step }) -join ',')"
        Assert-CheckoutsClean $fx 'zero'
    }

    Run 'AC8 S4-2 driver writes the line and stops when the marker is missing' {
        $fx = New-S42Fixture
        Set-S42Plan $fx @{ Y04_TitleEdits=@{ marker=$false } }
        $r = Invoke-S42 $fx 'nomarker'
        $lines = Get-S42Lines $r.RunRoot
        $last = @($lines)[-1]
        Assert (-not $r.Ok -and $last.step -ceq 'Y04' -and $last.status -ceq 'no-marker') "no-marker: $($r.Code) last=$($last.step)/$($last.status)"
        if (-not $script:legacy) {
            # marker が無ければ Unity の終了を確かめられないので、基盤は Y を戻さずに隔離する（旧の driver は戻していた）。
            Assert ((Read-Bytes (Join-Path $fx.Y 'edits/title.txt')) -ceq "title`n" -and [IO.File]::Exists((Join-Path (Get-WorktreeGitDir $fx.Y) 'osm-scenario/in-use.json'))) 'checkout was restored without a confirmed exit'
            Assert ($last.gitStatusSame -eq $false) 'line claims the status is unchanged'
            Assert (@(Read-Records $r.RunRoot | Where-Object { $_.Contains('event') -and $_.event -ceq 'quarantine' -and $_.checkout -ceq 'Y' -and $_.restored -eq $false }).Count -ge 1) 'quarantine record'
        }
    }

    Run 'AC8 S4-2 driver stops when the restore does not return to the previous status' {
        $fx = New-S42Fixture
        Set-S42Plan $fx @{ X2_CompareInputsInANewProcess=@{ nested=$true } }
        $r = Invoke-S42 $fx 'nested'
        $lines = Get-S42Lines $r.RunRoot
        Assert (-not $r.Ok -and (@($lines | ForEach-Object { $_.step }) -join ',') -ceq 'X1,X2' -and @($lines)[1].gitStatusSame -eq $false) "restore mismatch: $($r.Code)"
    }

    Run 'AC8 S4-2 driver dry-run applies and restores patches without the runner, copy or cache' {
        $fx = New-S42Fixture
        Set-S42Plan $fx @{}
        $r = Invoke-S42 $fx 'dry' -DryRun
        $lines = Get-S42Lines $r.RunRoot
        Assert ($r.Ok -and @(Read-RunnerLog $fx).Count -eq 0) "dry-run ran the runner: $($r.Code) $($r.Err)"
        $expected = @($script:s42StepOrder | Where-Object { $_ -notin 'copy', 'cache' })
        Assert ((@($lines | ForEach-Object { $_.step }) -join ',') -ceq ($expected -join ',')) 'dry-run step order'
        foreach ($line in @($lines | Where-Object { $_.step -notin 'cleanup' })) { Assert ($line.status -ceq 'dry-run' -and $null -eq $line.total -and $line.stepPath -ceq '' -and $line.gitStatusSame) "dry-run line $($line.step)" }
        Assert (@($lines | Where-Object { $_.step -ceq 'cleanup' })[0].dryRun -eq $true) 'dry-run cleanup line'
        Assert-CheckoutsClean $fx 'dry'
        Assert (-not [IO.File]::Exists((Join-Path $fx.Y 'unity/UserSettings/OSMContentDelivery.json'))) 'dry-run wrote the settings'
    }

    Run 'AC8 S4-2 driver continues a stopped run with -Only' {
        $fx = New-S42Fixture
        Set-S42Plan $fx @{ Y05_ProductObservation=@{ total=2 } }
        $r = Invoke-S42 $fx 'resume'
        Assert (-not $r.Ok) 'first part did not stop'
        Set-S42Plan $fx @{}
        Clear-RunnerLog $fx
        $r2 = Invoke-S42 $fx 'resume' -Only 'Y05', 'Y06'
        $lines = Get-S42Lines $r2.RunRoot
        $calls = @(Read-RunnerLog $fx)
        Assert ($r2.Ok -and $calls.Count -eq 2 -and (@($lines | ForEach-Object { $_.step }) | Select-Object -Last 2) -join ',' -ceq 'Y05,Y06') "resume: $($r2.Code) $($r2.Err)"
        Assert ((ConvertFrom-Json $calls[0].ySettings).cacheRoot -ceq @($lines | Where-Object { $_.step -ceq 'cache' })[0].cacheRoot) 'the resumed case lost the cache settings'
        if (-not $script:legacy) {
            Assert (@(Read-Records $r2.RunRoot | Where-Object { $_.Contains('event') -and $_.event -ceq 'case-start' -and $_.case -ceq 'Y05' -and $_.attempt -eq 2 }).Count -eq 1) 'resume attempt number'
        }
    }

    if (-not $script:legacy) {
        Run 'AC8 S4-2 driver refuses what the base allowed silently' {
            $fx = New-S42Fixture
            Set-S42Plan $fx @{}
            $unknown = Invoke-S42 $fx 'u1' -Only 'Nope'
            Assert (-not $unknown.Ok -and -not [IO.Directory]::Exists($unknown.RunRoot)) 'unknown -Only accepted'
            $first = Invoke-S42 $fx 'twice' -DryRun
            $again = Invoke-S42 $fx 'twice' -DryRun
            Assert ($first.Ok -and -not $again.Ok) 'same RunId without -Only accepted'
            $prod = Invoke-S42 $fx 'twice' -Only 'X1'
            Assert (-not $prod.Ok) 'production continued a dry-run root'
            $badId = Invoke-S42 $fx '..'
            Assert (-not $badId.Ok) 'RunId .. accepted'
            Write-Text (Join-Path $fx.Y 'unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset') "urp: 2`n"
            $drift = Invoke-S42 $fx 'drift'
            Assert (-not $drift.Ok -and (Read-Bytes (Join-Path $fx.Y 'unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset')) -ceq "urp: 2`n" -and @(Read-RunnerLog $fx).Count -eq 0) 'a drifted file was reverted or accepted'
        }
    }
} finally {
    foreach ($name in 'FAKE_X_ROOT', 'FAKE_Y_ROOT', 'FAKE_RUNNER_LOG', 'FAKE_RUNNER_PLAN') { [Environment]::SetEnvironmentVariable($name, $null) }
    Complete-ScenarioTestRun 'Scenario driver'
}
if ($failed.Count -gt 0) { exit 1 }
