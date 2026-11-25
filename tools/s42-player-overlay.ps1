<#
.SYNOPSIS
    S4-2（遊べる Player に手元の上書きを重ねる）の判定の process の列を実行する。
.DESCRIPTION
    Phase C の実行担当が呼ぶ driver。実機の検証の基盤（tools/Harness/Scenario.psm1）の上の scenario `s42` で、
    checkout X（Player と base を build する）と checkout Y（上書きを作る別の Library）の 2 つの検証用の checkout を使い、
    HANDOFF §5 の順で `run-tests.ps1 -Filter <case>$` を 1 case ずつ別の Unity process で走らせる。
    checkout の事前確認、patch の適用と復元、Unity の終了の確認、record と証拠の置き場は基盤が持つ
    （契約は tools/Harness/README.md の「実機の検証の scenario」）。この file が持つのは S4-2 に固有の物だけ:
    case の列、test の class の名前、環境変数、X から Y への Player の複写、Y の cache の設定、片付け。

    この driver は合否を判定しない。各 case の XML の total が 1 であること（-Filter の取り違えの検出）と、戻しの確認だけを見る。
    exit 0 は製品の合格を意味しない。受け入れ条件の合否は TestResults/s42/<RunId>/ の record・log・画像で判定の担当が読む。
.PARAMETER XRoot
    checkout X の repo root（登録した検証用の checkout）。省けば基盤が用意する。
.PARAMETER YRoot
    checkout Y の repo root（X と別の検証用の checkout、別の Library）。省けば基盤が用意する。
.PARAMETER RunId
    run の id（英数字と間の . _ -、32 文字まで）。S42_RUN_ID と S41_RUN_ID に同じ値を置く。
.PARAMETER Only
    走らせる step の名前（既定は全て）。止まった run を同じ RunId で続ける時に使う。
.PARAMETER DryRun
    Unity を起動せず、各 case の patch の適用と戻し、git status の確認だけを行う。
.PARAMETER Commit
    対象の commit。省けば、X と Y を渡した時は確かめず、基盤が用意する時はこの checkout の HEAD。
#>
[CmdletBinding()]
param(
    [string]$XRoot = '',
    [string]$YRoot = '',
    [Parameter(Mandatory)][string]$RunId,
    [string[]]$Only = @(),
    [switch]$DryRun,
    [string]$Commit = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Harness/Scenario.psm1')

$overlayTests = 'SampleGame.Tests.Editor.Build.PlayerOverlayEndToEndTests'
$kindTests = 'OneStarMaker.Tests.Editor.Build.PlayerKindEndToEndTests'
$patchDirectory = 'unity/Assets/SampleGame/Tests/Editor/Build/PlayerOverlayEdits'
$settingsRelative = 'unity/UserSettings/OSMContentDelivery.json'

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
$stepByName = @{}
foreach ($step in $steps) { $stepByName[$step.Name] = $step }

# 1 case = 1 Unity process。filter は末尾に $ を付けて渡す（付けないと部分一致になり 1 case に絞れない）。
# record の filter は今までどおり $ なしの値にする。
$runCase = {
    param($context)
    $step = $script:stepByName[$context.Case]
    $result = Invoke-ScenarioTests $context -Filter ($step.Filter + '$') -ExpectedTotal 1 -Environment @{ S42_RUN_ID=$context.RunId; S41_RUN_ID=$context.RunId }
    return @{ status=$result.Status; total=$result.Total; stepPath=$result.StepPath }
}

# 基盤が戻した後に、今までと同じ形の行を driver.jsonl に足す（event の key を持たない行）。
$writeCaseLine = {
    param($context, $result)
    $step = $script:stepByName[$context.Case]
    $value = $result.BodyValue
    $status = if ($null -ne $value) { $value.status } else { 'error' }
    $total = if ($null -ne $value) { $value.total } else { $null }
    $stepPath = if ($null -ne $value) { $value.stepPath } else { '' }
    Write-ScenarioRecord $context @{ step=$step.Name; root=$step.Root; filter=$step.Filter; patches=@($step.Patches); status=$status; total=$total
        stepPath=$stepPath; gitStatusSame=$result.GitStatusSame; gitStatusBefore=@($result.GitStatusBefore); gitStatusAfter=@($result.GitStatusAfter) }
}

function Set-YCacheRoot([psobject]$Context) {
    # run ごとの install の cache。既定の %LOCALAPPDATA%/OSM/ContentCache を他の run や作業と共有しないため、
    # run の外部 root の下に作り、Y の設定 file（管理する file）に書く。設定 file は片付けの case が元へ戻す。
    $cacheRoot = Get-ScenarioDirectory $Context -Purpose 'cache'
    $settingsPath = Join-Path $Context.Roots['Y'] $settingsRelative
    $settings = if (Test-Path -LiteralPath $settingsPath) { Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json -AsHashtable } else { @{} }
    $settings.cacheRoot = $cacheRoot
    [void](Set-ScenarioFile $Context -Checkout 'Y' -Path $settingsRelative -Content ($settings | ConvertTo-Json -Depth 4))
    Write-ScenarioRecord $Context @{ step='cache'; cacheRoot=$cacheRoot; settingsPath=$settingsPath }
}

function Copy-PlayerToY([psobject]$Context) {
    $xRoot = $Context.Roots['X']
    $yRoot = $Context.Roots['Y']
    $players = Join-Path $xRoot 'artifacts/playable/players'
    $identity = Get-ChildItem -LiteralPath $players -Directory | Sort-Object Name -CaseSensitive | Select-Object -Last 1 -ExpandProperty Name
    if (-not $identity) { throw "No playable Player in $players" }
    foreach ($kind in 'players', 'transport') {
        $source = Join-Path $xRoot "artifacts/playable/$kind/$identity"
        $target = Join-Path $yRoot "artifacts/playable/$kind/$identity"
        if (Test-Path -LiteralPath $target) { throw "The Y checkout already has $target" }
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath $source -Destination $target -Recurse
    }
    # AC11 の X の側（Library の path と build の Unity の版）を、この case の証拠として写す。
    $xRecord = Join-Path $xRoot "TestResults/s42/$($Context.RunId)/X1_BuildPlayableAndCompareInputs/record.json"
    if (Test-Path -LiteralPath $xRecord) { [void](Save-ScenarioArtifact $Context -Name 'x1-record.json' -Path $xRecord) }
    Write-ScenarioRecord $Context @{ step='copy'; identity=$identity }
    Set-YCacheRoot $Context
}

$scenario = New-Scenario -Name 's42' -Checkout 'X', 'Y' -RecordCheckout 'Y' -RecordDirectory 'TestResults/s42'
foreach ($step in $steps) {
    switch ($step.Root) {
        'copy' { Add-ScenarioCase $scenario -Name $step.Name -Body { param($context) if (-not $context.DryRun) { Copy-PlayerToY $context } } }
        'cleanup' {
            # その run の cache と設定 file だけを片付ける（既定の cache には触らない）。
            Add-ScenarioCase $scenario -Name $step.Name -Body {
                param($context)
                $cacheRoot = Get-ScenarioDirectory $context -Purpose 'cache' -PathOnly
                [void](Reset-ScenarioFiles $context -Checkout 'Y')
                [void](Remove-ScenarioDirectories $context)
                Write-ScenarioRecord $context @{ step='cleanup'; cacheRoot=$cacheRoot; dryRun=[bool]$context.DryRun }
            }
        }
        default {
            Add-ScenarioCase $scenario -Name $step.Name -Checkout $step.Root -Patches @($step.Patches | ForEach-Object { "$patchDirectory/$_" }) -Body $runCase -After $writeCaseLine
        }
    }
}

$checkoutRoots = @{}
if ($XRoot) { $checkoutRoots.X = $XRoot }
if ($YRoot) { $checkoutRoots.Y = $YRoot }
$result = Invoke-Scenario $scenario -RunId $RunId -Only $Only -DryRun:$DryRun -CheckoutRoots $checkoutRoots -Commit $Commit
if ($result.RunRoot) { Write-Host "[s42] done: $(Join-Path $result.RunRoot 'driver.jsonl')" }
exit $result.ExitCode
