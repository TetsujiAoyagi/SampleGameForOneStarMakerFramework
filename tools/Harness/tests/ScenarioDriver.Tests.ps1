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
    if (-not $script:legacy) {
        Run 'AC6 foundation code names no product identifier' {
            $findings = foreach ($file in $script:FoundationFiles) {
                Get-ScanFindings -Path $file -Commands @('SampleGame', 'OneStarMaker') -Types @('SampleGame', 'OneStarMaker') `
                    -Strings @('SampleGame', 'OneStarMaker', 'OSMContent', 'artifacts/', 'artifacts\', 'unity/Assets/', 'unity\Assets\', 'PlayerOverlayEndToEndTests', 'PlayerKindEndToEndTests') |
                    ForEach-Object { "$([IO.Path]::GetFileName($file)) $_" }
            }
            Assert (@($findings).Count -eq 0) "product identifiers: $(@($findings) -join '; ')"
        }

        Run 'AC6 the scan sees literals and commands but not comments' {
            $probe = Join-Path $script:tempRoot 'probe.ps1'
            Write-Text $probe "# SampleGame in a comment`n<# git help #>`n`$a = 'x'`ngit status`n`$b = `"artifacts/x`"`n[Diagnostics.Process]::Start('a')`n"
            $f = Get-ScanFindings -Path $probe -Commands @('git') -Strings @('SampleGame', 'artifacts/') -StaticMembers @('Diagnostics.Process::Start')
            Assert (@($f).Count -eq 3 -and -not @($f | Where-Object { $_ -like '*SampleGame*' })) "scan findings $(@($f) -join '; ')"
        }

        Run 'AC8 AC9 drivers hold only cases and declared evidence' {
            foreach ($driver in @((Join-Path $repoUnderTest 'tools/s42-player-overlay.ps1'), (Join-Path $repoUnderTest 'tools/Harness/scenario-editor-smoke.ps1'),
                    (Join-Path $repoUnderTest 'tools/s43a-local-origin.ps1'))) {
                $findings = Get-DriverScanFindings $driver
                Assert (@($findings).Count -eq 0) "$([IO.Path]::GetFileName($driver)): $(@($findings) -join '; ')"
            }
            $private = Get-FoundationPrivateFunctions
            Assert ($private -ccontains 'Set-ScenarioSeam' -and $private -ccontains 'Restore-ScenarioCheckout' -and $private -cnotcontains 'Invoke-ScenarioTests') 'private function list'
        }

        Run 'AC9 the smoke scenario dry-run uses only a prepared checkout and starts no Unity' {
            $fx = New-FixtureRepo
            $place = Join-Path $fx.Fixture 'osm-v'
            $smoke = Join-Path $repoUnderTest 'tools/Harness/scenario-editor-smoke.ps1'
            $global:LASTEXITCODE = 0
            # 用意されていない checkout では -DryRun は止まる（-DryRun は checkout を作らない）。
            & $smoke -RunId 'd1' -DryRun -Token 'token-0001' *>&1 | Out-Null
            Assert ($LASTEXITCODE -ne 0 -and -not [IO.Directory]::Exists((Join-Path $place 'editor-smoke-E'))) 'dry-run created a checkout'
            Invoke-Git $fx.Repo @('worktree', 'add', '-q', '--detach', (Join-Path $place 'editor-smoke-E')) | Out-Null
            $r = Invoke-ScenarioCheckoutOperation -Operation register -Path (Join-Path $place 'editor-smoke-E') -RepoRoot $fx.Repo
            Assert $r.Ok 'register'
            $global:LASTEXITCODE = 0
            & $smoke -RunId 'd2' -DryRun -Token 'token-0001' *>&1 | Out-Null
            $records = Read-Records (Get-RunRoot $fx 'editor-smoke' 'd2')
            Assert ($LASTEXITCODE -eq 0 -and @($records | Where-Object { $_.Contains('event') -and $_.event -ceq 'case-end' -and $_.base -ceq 'ok' }).Count -eq 2) "smoke dry-run exit $LASTEXITCODE"
        }

        Run 'AC9 the S4-3a scenario dry-run passes every case on prepared checkouts and starts no Unity' {
            # E6 の固定 patch と同じ path に、fixture の tracked の file を変える patch を置く（dry-run でも patch は当てて戻す）。
            $patchPath = 'unity/Assets/SampleGame/Tests/Editor/Build/PlayerOverlayEdits/ac01-title.patch'
            $fx = New-FixtureRepo { param($repo) Write-Text (Join-Path $repo $patchPath) "diff --git a/data/value.txt b/data/value.txt`n--- a/data/value.txt`n+++ b/data/value.txt`n@@ -1 +1 @@`n-one`n+two`n" }
            $place = Join-Path $fx.Fixture 'osm-v'
            foreach ($name in 's43a-X', 's43a-Y') {
                Invoke-Git $fx.Repo @('worktree', 'add', '-q', '--detach', (Join-Path $place $name)) | Out-Null
                $r = Invoke-ScenarioCheckoutOperation -Operation register -Path (Join-Path $place $name) -RepoRoot $fx.Repo
                Assert $r.Ok "register $name"
            }
            $driver = Join-Path $repoUnderTest 'tools/s43a-local-origin.ps1'
            $global:LASTEXITCODE = 0
            & $driver -RunId 'd1' -DryRun *>&1 | Out-Null
            $code = $LASTEXITCODE
            $records = Read-Records (Get-RunRoot $fx 's43a' 'd1')
            $ends = @($records | Where-Object { $_.Contains('event') -and $_.event -ceq 'case-end' })
            $order = ($ends | ForEach-Object { $_.case }) -join ','
            Assert ($code -eq 0 -and $order -ceq 'S0,P1,E1,P2,E2,E3,E4,E5,E6,Cleanup' -and @($ends | Where-Object { $_.base -cne 'ok' }).Count -eq 0) "s43a dry-run exit $code order $order"
            foreach ($name in 's43a-X', 's43a-Y') {
                $root = Join-Path $place $name
                Assert ((Invoke-Git $root @('status', '--porcelain')) -eq $null -and (Read-Bytes (Join-Path $root 'data/value.txt')) -ceq "one`n") "$name not restored"
                Assert (-not [IO.File]::Exists((Join-Path $root 'unity/UserSettings/OSMContentOrigin.json'))) "$name dry-run wrote the origin settings"
            }
            Assert (-not [IO.Directory]::Exists((Join-Path (Get-RunRoot $fx 's43a' 'd1') 'evidence'))) 'dry-run saved evidence'
        }

        Run 'AC9 the S4-3a driver saves receipt copies under names within the foundation limit' {
            # -DryRun は証拠を保存しないので、driver の証拠を保存する関数だけを取り出し、基盤の本物の保存で走らせる。
            $ast = Get-ScriptAst (Join-Path $repoUnderTest 'tools/s43a-local-origin.ps1')
            foreach ($name in 'Save-Evidence', 'Get-CacheListing', 'Save-YState') {
                $definition = @($ast.FindAll({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] }, $true) | Where-Object { $_.Name -ceq $name })
                Assert ($definition.Count -eq 1) "driver function $name"
                . ([scriptblock]::Create($definition[0].Extent.Text))
            }
            # 時点の名前は driver の呼び出しから拾う（長い時点を足しても、この test が上限を見る）。
            $points = @($ast.FindAll({ param($n) $n -is [Management.Automation.Language.CommandAst] }, $true) |
                Where-Object { $_.GetCommandName() -ceq 'Save-YState' } | ForEach-Object { $_.CommandElements[2].Value } | Sort-Object -Unique)
            Assert ($points -ccontains 'after-acquire') "points $($points -join ',')"

            $fx = New-FixtureRepo
            $purpose = 'spring-full'
            $selectionRecord = 'unity/UserSettings/OSMContentBaseSelection.json'
            $run = [pscustomobject]@{ Root=(Join-Path $fx.Fixture 'run'); ExternalRoot=(Join-Path $fx.Fixture 'external') }
            $evidence = Join-Path $run.Root 'evidence/E1/1'
            $context = [pscustomobject]@{ DryRun=$false; Roots=@{ Y=$fx.Repo }; State=[pscustomobject]@{ Run=$run }; Attempt=[pscustomobject]@{ EvidenceDirectory=$evidence } }
            # 実際の build の identity の長さと、それより長い名前。receipt の無い directory は写しを作らない。
            $revisions = @('20261126T150018464Z-690b81d7359d46c2828215ed2fb10f46', ('r' * 100))
            $installed = Join-Path $run.ExternalRoot "cache/installed/$purpose"
            foreach ($revision in $revisions) { Write-Text (Join-Path $installed "$revision/receipt.json") "{`"revision`":`"$revision`"}`n" }
            [void][IO.Directory]::CreateDirectory((Join-Path $installed 'no-receipt'))
            Write-Text (Join-Path $fx.Repo $selectionRecord) "{}`n"

            foreach ($point in $points) {
                Save-YState $context $point
                $copies = @(ConvertFrom-Json -InputObject ([IO.File]::ReadAllText((Join-Path $evidence "$point-receipts.json"))))
                Assert ((@($copies | ForEach-Object { $_.installed } | Sort-Object) -join ',') -ceq (@($revisions | ForEach-Object { "$purpose/$_" } | Sort-Object) -join ',')) "$point copied revisions"
                Assert (@($copies | ForEach-Object { $_.evidence } | Sort-Object -Unique).Count -eq $revisions.Count) "$point evidence names are not distinct"
                foreach ($copy in $copies) {
                    $revision = $copy.installed.Substring($purpose.Length + 1)
                    Assert ((Read-Bytes (Join-Path $evidence $copy.evidence)) -ceq (Read-Bytes (Join-Path $installed "$revision/receipt.json"))) "$point $($copy.evidence) is not the receipt of $revision"
                }
            }
        }

        Run 'AC9 AC10 the S4-3a driver observes the Play targets of HANDOFF section 5 in order' {
            # -DryRun は Play の後の観測を飛ばすので、観測の関数と eval の code だけを取り出し、偽の Editor の応答で走らせる。
            $ast = Get-ScriptAst (Join-Path $repoUnderTest 'tools/s43a-local-origin.ps1')
            foreach ($name in 'Read-LoadRecord', 'Send-AddScene', 'Invoke-Play') {
                $definition = @($ast.FindAll({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] }, $true) | Where-Object { $_.Name -ceq $name })
                Assert ($definition.Count -eq 1) "driver function $name"
                . ([scriptblock]::Create($definition[0].Extent.Text))
            }
            $variables = 'recordKey', 'readRecord', 'directorPrelude', 'addSceneTemplate', 'readLoadTemplate', 'readTitleToken'
            foreach ($statement in $ast.EndBlock.Statements) {
                if ($statement -is [Management.Automation.Language.AssignmentStatementAst] -and $variables -ccontains $statement.Left.VariablePath.UserPath) { . ([scriptblock]::Create($statement.Extent.Text)) }
            }

            # 偽の Editor: 送った・読んだ code を順に残し、Scene の名前ごとに load の記録を返す（null のままの Scene は Wait が期限切れを投げる）。
            $script:fakeCalls = [Collections.Generic.List[string]]::new()
            $script:fakeSaved = @{}
            $script:fakeLoads = @{}
            $script:fakePlaying = 'True'
            function Send-Marker { }
            function Write-ScenarioRecord { }
            function Wait-PlayRecord { return @{ value="{`"record`":{`"state`":`"EnteredPlay`"}}`n@@20"; expired=$false } }
            function Save-Evidence([psobject]$Context, [string]$Name, [object]$Content) { $script:fakeSaved[$Name] = "$Content" }
            function Get-FakeScene([string]$Code) { if ($Code -match 'new object\[\] \{ "([^"]+)" \}|\? "([^"]+)" :') { return "$($Matches[1])$($Matches[2])" }; return '' }
            function Get-FakeAnswer([string]$Code) {
                if ($Code -like '*isPlaying.ToString*') { return $script:fakePlaying }
                if ($Code -like '*GetContentLoadRecord*') { $scene = Get-FakeScene $Code; return $(if ($script:fakeLoads.ContainsKey($scene)) { $script:fakeLoads[$scene] } else { '' }) }
                if ($Code -like '*_panelRoot*') { return 'S42-AC1-UXML-TOKEN' }
                if ($Code -like '*SessionState.GetString*') { return "{`"record`":{`"errorCount`":0}}`n@@30" }
                return ''
            }
            function Send-ScenarioEditor([psobject]$Context, [psobject]$Session, [string]$Code) {
                $kind = if ($Code -like '*GetMethod("AddScene")*') { "add:$(Get-FakeScene $Code)" } else { 'send' }
                $script:fakeCalls.Add($kind)
                return [pscustomobject]@{ Answered=$true; Value=$(if ($kind -like 'add:*') { 'requested' } else { 'ok' }) }
            }
            function Read-ScenarioEditor([psobject]$Context, [psobject]$Session, [string]$Code) {
                $answer = Get-FakeAnswer $Code
                if ($Code -like '*GetContentLoadRecord*') { $script:fakeCalls.Add("read:$(Get-FakeScene $Code)") }
                return $answer
            }
            function Wait-ScenarioEditor([psobject]$Context, [psobject]$Session, [string]$Code, [scriptblock]$Until, [int]$TimeoutMs, [int]$QuietMs) {
                $answer = Get-FakeAnswer $Code
                if ($Code -like '*GetContentLoadRecord*') { $script:fakeCalls.Add("wait:$(Get-FakeScene $Code)") }
                elseif ($Code -like '*_panelRoot*') { $script:fakeCalls.Add('wait:title') }
                if (-not (& $Until $answer)) { throw 'expired' }
                return $answer
            }
            $context = [pscustomobject]@{ DryRun=$false; Case='E1' }
            $r1 = '20261126T150018464Z-690b81d7359d46c2828215ed2fb10f46|' + ('a' * 64)

            # 経路 Built（E1・E2・E4・E5）: AddScene を送らず、初回 Scene Title の記録を読む。
            $script:fakeLoads = @{ Title="$r1|ContentDirectory|registered-content-directory|VerifiedDigest" }
            [void](Invoke-Play $context $null 600000)
            $observations = ConvertFrom-Json -InputObject $script:fakeSaved['play-observations.json']
            Assert (@($script:fakeCalls | Where-Object { $_ -like 'add:*' }).Count -eq 0) "built route sent AddScene: $($script:fakeCalls -join ',')"
            Assert ((@($script:fakeCalls | Where-Object { $_ -like 'wait:*' -or $_ -like 'read:*' }) -join ',') -ceq 'wait:Title') "built route reads $($script:fakeCalls -join ',')"
            $title = $observations.loads.Title
            Assert ($title.observed -and $title.revision -ceq $r1.Split('|')[0] -and $title.digest -ceq ('a' * 64) -and $title.sourceKind -ceq 'ContentDirectory' -and $title.providerId -ceq 'registered-content-directory' -and $title.acceptanceBasis -ceq 'VerifiedDigest') "built route Title $($script:fakeSaved['play-observations.json'])"
            Assert (-not $script:fakeSaved.ContainsKey('play-record-after.txt')) 'built route re-read the record'

            # 経路 Built + local edits（E6）: Title を要求して読み → title → HpGauge を要求して読み → OutGameScene を 1 回読む → record の読み直し。
            # Title の記録が期限まで null でも、後の項目を続けて読み、理由を残す。
            $script:fakeCalls.Clear(); $script:fakeSaved = @{}
            $script:fakeLoads = @{ HpGauge="$r1|ContentDirectory|registered-content-directory|VerifiedDigest"; OutGameScene="$r1|EditorSnapshot|editor-snapshot|EditorSnapshot" }
            [void](Invoke-Play $context $null 1800000 -Overlay)
            $observations = ConvertFrom-Json -InputObject $script:fakeSaved['play-observations.json']
            Assert ((@($script:fakeCalls | Where-Object { $_ -cne 'send' }) -join ',') -ceq 'add:Title,wait:Title,wait:title,add:HpGauge,wait:HpGauge,read:OutGameScene') "overlay order $($script:fakeCalls -join ',')"
            Assert (-not $observations.loads.Title.observed -and "$($observations.loads.Title.reason)".Length -gt 0) 'overlay Title without a reason'
            Assert ($observations.title.observed -and $observations.title.value -ceq 'S42-AC1-UXML-TOKEN') 'overlay title value'
            Assert ($observations.loads.HpGauge.observed -and $observations.loads.OutGameScene.observed -and $observations.loads.OutGameScene.sourceKind -ceq 'EditorSnapshot') 'overlay HpGauge / OutGameScene'
            Assert ($observations.addTitle.value -ceq 'requested' -and $observations.addHpGauge.value -ceq 'requested') 'overlay AddScene answers'
            Assert ($script:fakeSaved['play-record-after.txt'] -like '*errorCount*') 'overlay record re-read'

            # Play に届かない時: 観測の項目を理由つきで残し、Scene を要求しない。
            $script:fakeCalls.Clear(); $script:fakeSaved = @{}; $script:fakePlaying = 'False'
            [void](Invoke-Play $context $null 1800000 -Overlay)
            $observations = ConvertFrom-Json -InputObject $script:fakeSaved['play-observations.json']
            Assert (@($script:fakeCalls | Where-Object { $_ -cne 'send' }).Count -eq 0) "not playing sent or read $($script:fakeCalls -join ',')"
            Assert (@(@('Title', 'HpGauge', 'OutGameScene') | Where-Object { $observations.loads.$_.observed -or "$($observations.loads.$_.reason)" -notlike 'not playing*' }).Count -eq 0 -and "$($observations.title.reason)" -like 'not playing*') 'not playing reasons'
        }
    }
} finally {
    foreach ($name in 'FAKE_X_ROOT', 'FAKE_Y_ROOT', 'FAKE_RUNNER_LOG', 'FAKE_RUNNER_PLAN') { [Environment]::SetEnvironmentVariable($name, $null) }
    Complete-ScenarioTestRun 'Scenario driver'
}
if ($failed.Count -gt 0) { exit 1 }
