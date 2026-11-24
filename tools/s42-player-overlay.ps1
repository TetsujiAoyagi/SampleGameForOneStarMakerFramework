<#
.SYNOPSIS
    S4-2（遊べる Player に手元の上書きを重ねる）の判定の process の列を実行する。
.DESCRIPTION
    Phase C の実行担当が呼ぶ driver。checkout X（Player と base を build する）と checkout Y（上書きを作る別の Library）の 2 つの
    worktree を受け取り、HANDOFF §5 の順で `run-tests.ps1 -Filter <case>$` を 1 case ずつ別の Unity process で走らせる。
    編集を伴う case は Unity の起動の前に PlayerOverlayEdits の patch を git apply し、case の後に git checkout と git clean -fd
    （-x は付けない。artifacts/ の Player・上書き・記録を消さない）で戻し、git status --porcelain が case の前と同じことを確かめる。
    違えば以後の case を止める。上書きの artifacts と入口の記録は case の間で消さない（後の case が前の結果を使う）。

    この driver は合否を判定しない。各 case の XML の total が 1 であること（-Filter の取り違えの検出）と、戻しの確認だけを見る。
    受け入れ条件の合否は TestResults/s42/<RunId>/ の record・log・画像で判定の担当が読む。
.PARAMETER XRoot
    checkout X の repo root（新しい worktree）。
.PARAMETER YRoot
    checkout Y の repo root（X と別の worktree、別の Library）。
.PARAMETER RunId
    run の id（file 名に使える値）。S42_RUN_ID と S41_RUN_ID に同じ値を置く。
.PARAMETER Only
    走らせる step の名前（既定は全て）。途中から再開する時に使う。
.PARAMETER DryRun
    Unity を起動せず、各 case の patch の適用と戻し、git status の確認だけを行う。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$XRoot,
    [Parameter(Mandatory)][string]$YRoot,
    [Parameter(Mandatory)][string]$RunId,
    [string[]]$Only = @(),
    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$XRoot = (Resolve-Path -LiteralPath $XRoot).Path
$YRoot = (Resolve-Path -LiteralPath $YRoot).Path
if ($XRoot -ieq $YRoot) { throw 'X and Y must be different checkouts (each with its own Library).' }
if ($RunId -notmatch '^[A-Za-z0-9._-]+$') { throw "RunId must be file-name-safe: $RunId" }

$overlayTests = 'SampleGame.Tests.Editor.Build.PlayerOverlayEndToEndTests'
$kindTests = 'OneStarMaker.Tests.Editor.Build.PlayerKindEndToEndTests'
$patchDirectory = 'unity/Assets/SampleGame/Tests/Editor/Build/PlayerOverlayEdits'
# 新しい worktree の最初の Unity の起動が書き換える file（D11 の指紋に当たる）。Unity の起動の前に毎回戻す。
$urpGlobalSettings = 'unity/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset'

# 判定の process の列。Root は X か Y、Patches は Unity の起動の前に当てる patch。
$steps = @(
    @{ Name='X1'; Root='X'; Filter="$overlayTests.X1_BuildPlayableAndCompareInputs"; Patches=@() }
    @{ Name='X2'; Root='X'; Filter="$overlayTests.X2_CompareInputsInANewProcess"; Patches=@() }
    @{ Name='X-LaunchPlayable'; Root='X'; Filter="$kindTests.LaunchPlayable"; Patches=@() }
    @{ Name='X-BuildSmoke'; Root='X'; Filter="$kindTests.BuildSmoke"; Patches=@() }
    @{ Name='X-LaunchSmoke'; Root='X'; Filter="$kindTests.LaunchSmoke"; Patches=@() }
    @{ Name='X3'; Root='X'; Filter="$overlayTests.X3_SmokeRejectsOverlayKeys"; Patches=@() }
    @{ Name='CopyToY'; Root='copy' }
    @{ Name='Y01'; Root='Y'; Filter="$overlayTests.Y01_EntryBeforeInstall"; Patches=@() }
    @{ Name='Y02'; Root='Y'; Filter="$overlayTests.Y02_InstallTransport"; Patches=@() }
    @{ Name='Y03'; Root='Y'; Filter="$overlayTests.Y03_EntryWithoutEdits"; Patches=@() }
    @{ Name='Y04'; Root='Y'; Filter="$overlayTests.Y04_TitleEdits"; Patches=@('ac01-title.patch') }
    @{ Name='Y05'; Root='Y'; Filter="$overlayTests.Y05_ProductObservation"; Patches=@('ac01-title.patch') }
    @{ Name='Y06'; Root='Y'; Filter="$overlayTests.Y06_ReuseBuilt"; Patches=@('ac01-title.patch') }
    @{ Name='Y07'; Root='Y'; Filter="$overlayTests.Y07_ReuseReused"; Patches=@('ac01-title.patch') }
    @{ Name='Y08'; Root='Y'; Filter="$overlayTests.Y08_ReuseAfterAddingAFile"; Patches=@('ac01-title.patch') }
    @{ Name='Y09'; Root='Y'; Filter="$overlayTests.Y09_DirectDigestRejections"; Patches=@('ac01-title.patch') }
    @{ Name='Y10'; Root='Y'; Filter="$overlayTests.Y10_SecondPlayerWhileFirstRuns"; Patches=@('ac01-title.patch') }
    @{ Name='Y11'; Root='Y'; Filter="$overlayTests.Y11_UiCommonAndMapEdits"; Patches=@('ac12-uicommon.patch', 'ac16-map.patch') }
    @{ Name='Y12'; Root='Y'; Filter="$overlayTests.Y12_PlayerInputEdit"; Patches=@('ac15-tag.patch') }
    @{ Name='Y13'; Root='Y'; Filter="$overlayTests.Y13_ScriptFieldEdit"; Patches=@('ac05-field.patch') }
    @{ Name='Y14'; Root='Y'; Filter="$overlayTests.Y14_OverlayForAnotherBase"; Patches=@() }
    @{ Name='Y15'; Root='Y'; Filter="$overlayTests.Y15_PairlessPlayer"; Patches=@() }
    @{ Name='Y16'; Root='Y'; Filter="$overlayTests.Y16_InheritedEnvironment"; Patches=@() }
    @{ Name='Y17'; Root='Y'; Filter="$overlayTests.Y17_UnbuildableOnly"; Patches=@() }
    @{ Name='CleanupCache'; Root='cleanup' }
)

$recordDirectory = Join-Path $YRoot "TestResults/s42/$RunId"
New-Item -ItemType Directory -Force -Path $recordDirectory | Out-Null
$driverLog = Join-Path $recordDirectory 'driver.jsonl'

function Write-DriverRecord([hashtable]$Value) {
    $Value.utc = [DateTime]::UtcNow.ToString('o')
    Add-Content -LiteralPath $driverLog -Value ($Value | ConvertTo-Json -Compress -Depth 6) -Encoding utf8
}

function Get-Porcelain([string]$Root) {
    $lines = & git -C $Root status --porcelain
    if ($LASTEXITCODE -ne 0) { throw "git status failed in $Root" }
    return @($lines | Where-Object { $_ })
}

function Restore-Checkout([string]$Root) {
    & git -C $Root checkout -- . | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git checkout failed in $Root" }
    & git -C $Root clean -fd -q | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git clean failed in $Root" }
}

function Restore-UrpGlobalSettings([string]$Root) {
    & git -C $Root checkout -- $urpGlobalSettings | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Restoring $urpGlobalSettings failed in $Root" }
}

# run ごとの install の cache。既定の %LOCALAPPDATA%/OSM/ContentCache を他の run や作業と共有しないため、repo の外の短い path に作る。
$cacheRoot = Join-Path ([IO.Path]::GetPathRoot($YRoot)) "s42c\$RunId"

function Set-YCacheRoot {
    New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
    $settingsPath = Join-Path $YRoot 'unity/UserSettings/OSMContentDelivery.json'
    $settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json -AsHashtable } else { @{} }
    $settings.cacheRoot = $cacheRoot
    New-Item -ItemType Directory -Force -Path (Split-Path $settingsPath) | Out-Null
    [IO.File]::WriteAllText($settingsPath, ($settings | ConvertTo-Json -Depth 4), [Text.UTF8Encoding]::new($false))
    Write-DriverRecord @{ step='cache'; cacheRoot=$cacheRoot; settingsPath=$settingsPath }
}

function Copy-PlayerToY {
    $players = Join-Path $XRoot 'artifacts/playable/players'
    $identity = Get-ChildItem -LiteralPath $players -Directory | Sort-Object Name -CaseSensitive | Select-Object -Last 1 -ExpandProperty Name
    if (-not $identity) { throw "No playable Player in $players" }
    foreach ($kind in 'players', 'transport') {
        $source = Join-Path $XRoot "artifacts/playable/$kind/$identity"
        $target = Join-Path $YRoot "artifacts/playable/$kind/$identity"
        if (Test-Path -LiteralPath $target) { throw "The Y checkout already has $target" }
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath $source -Destination $target -Recurse
    }
    # AC11 の X の側（Library の path と build の Unity の版）を Y の run の record の隣に写す。
    $xRecord = Join-Path $XRoot "TestResults/s42/$RunId/X1_BuildPlayableAndCompareInputs/record.json"
    if (Test-Path -LiteralPath $xRecord) { Copy-Item -LiteralPath $xRecord -Destination (Join-Path $recordDirectory 'x1-record.json') -Force }
    Write-DriverRecord @{ step='copy'; identity=$identity }
    Set-YCacheRoot
}

function Invoke-Case([hashtable]$Step) {
    $root = if ($Step.Root -eq 'X') { $XRoot } else { $YRoot }
    $before = Get-Porcelain $root
    Restore-UrpGlobalSettings $root
    foreach ($patch in $Step.Patches) {
        & git -C $root apply --whitespace=nowarn (Join-Path $root "$patchDirectory/$patch")
        if ($LASTEXITCODE -ne 0) { throw "git apply $patch failed in $root" }
    }
    $total = $null; $status = 'dry-run'; $stepPath = ''
    try {
        if (-not $DryRun) {
            $env:S42_RUN_ID = $RunId
            $env:S41_RUN_ID = $RunId
            $output = & pwsh -NoLogo -NoProfile -File (Join-Path $root 'tools/run-tests.ps1') -Filter ($Step.Filter + '$') 2>&1
            $marker = @($output | Where-Object { "$_" -like 'UNITY_TEST_RESULT *' }) | Select-Object -Last 1
            if ($marker) {
                $stepPath = "$marker".Substring('UNITY_TEST_RESULT '.Length).Trim()
                $result = Get-Content -Raw -LiteralPath $stepPath | ConvertFrom-Json
                $status = $result.status
                $total = $result.counts.total
            } else { $status = 'no-marker' }
        }
    } finally {
        Restore-Checkout $root
        $after = Get-Porcelain $root
        $same = (($before -join "`n") -ceq ($after -join "`n"))
        Write-DriverRecord @{ step=$Step.Name; root=$Step.Root; filter=$Step.Filter; patches=@($Step.Patches); status=$status; total=$total
            stepPath=$stepPath; gitStatusSame=$same; gitStatusBefore=@($before); gitStatusAfter=@($after) }
        if (-not $same) { throw "git status changed after $($Step.Name); stopping the remaining cases." }
    }
    if (-not $DryRun -and $total -ne 1) { throw "$($Step.Name) ran $total tests (expected exactly 1). Check the filter." }
}

foreach ($root in $XRoot, $YRoot) {
    Restore-UrpGlobalSettings $root
    $dirty = Get-Porcelain $root
    if ($dirty.Count -ne 0) { throw "$root is not clean: $($dirty -join '; ')" }
}

foreach ($step in $steps) {
    if ($Only.Count -ne 0 -and $Only -notcontains $step.Name) { continue }
    Write-Host "[s42] $($step.Name)"
    switch ($step.Root) {
        'copy' { if (-not $DryRun) { Copy-PlayerToY } }
        'cleanup' {
            # その run の cache だけを消す（既定の cache には触らない）。
            if (-not $DryRun -and (Test-Path -LiteralPath $cacheRoot)) { Remove-Item -LiteralPath $cacheRoot -Recurse -Force }
            Write-DriverRecord @{ step='cleanup'; cacheRoot=$cacheRoot; dryRun=[bool]$DryRun }
        }
        default { Invoke-Case $step }
    }
}
Write-Host "[s42] done: $driverLog"
