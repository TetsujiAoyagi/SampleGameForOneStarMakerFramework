<#
.SYNOPSIS
    実機の検証の基盤の疎通用の最小の scenario。scenario の書き方の例を兼ねる。
.DESCRIPTION
    editor: 窓のある Editor を起動し、ready → 読む eval（Unity の版と project の path）→ 送る eval（SessionState に
            token を置く。1 回だけ）→ 読む eval（token を読み直す）→ 終了、を 1 回通す。
    tests : 基盤を通して run-tests.ps1 を起動する（-TestFilter を省けば全 EditMode）。
    検証用の checkout（E）は基盤が対象の commit で用意する（<主の worktree の親>\osm-v\editor-smoke-E）。
    exit 0 は基盤の操作が通ったことだけを示す。判定の担当は run の root の record と原記録を読む。
.PARAMETER RunId
    run の id。同じ RunId で -Only を付けると続きの実行になる。
.PARAMETER Commit
    対象の commit。省けばこの checkout の HEAD。
.PARAMETER Only
    走らせる case（editor、tests）。
.PARAMETER DryRun
    Unity を起動しない。checkout が既に用意されている時だけ使える。
.PARAMETER Token
    editor の case で SessionState に置く値。実行のたびに新しく作る（例: GUID）。
.PARAMETER TestFilter
    tests の case の -testFilter。省けば全 EditMode。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunId,
    [string]$Commit = '',
    [string[]]$Only = @(),
    [switch]$DryRun,
    [string]$Token = '',
    [string]$TestFilter = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Scenario.psm1')

$scenario = New-Scenario -Name 'editor-smoke' -Checkout 'E'

Add-ScenarioCase $scenario -Name 'editor' -Checkout 'E' -Body {
    param($context)
    # token は C# の文字列に埋めるので、英数字と - だけに限る。
    if ($script:Token -cnotmatch '^[A-Za-z0-9-]{8,64}$') { throw '-Token に、実行のたびに新しく作る値（英数字と -、8〜64 文字）を渡します' }
    $session = Start-ScenarioEditor $context
    $identity = Read-ScenarioEditor $context $session -Code 'return UnityEngine.Application.unityVersion + "|" + System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);'
    [void](Send-ScenarioEditor $context $session -Code "UnityEditor.SessionState.SetString(`"osm.scenario.smoke.token`", `"$($script:Token)`"); return `"set`";")
    $readBack = Read-ScenarioEditor $context $session -Code 'return UnityEditor.SessionState.GetString("osm.scenario.smoke.token", "");'
    $exit = Stop-ScenarioEditor $context $session
    $summary = [ordered]@{ token=$script:Token; identity=$identity; readBack=$readBack; exitCode=$exit.ExitCode; forced=$exit.Forced }
    [void](Save-ScenarioArtifact $context -Name 'smoke.json' -Content (ConvertTo-Json -InputObject $summary))
    return $summary
}

Add-ScenarioCase $scenario -Name 'tests' -Checkout 'E' -Body {
    param($context)
    $result = Invoke-ScenarioTests $context -Filter $script:TestFilter
    return @{ status=$result.Status; total=$result.Total; stepPath=$result.StepPath }
}

$result = Invoke-Scenario $scenario -RunId $RunId -Commit $Commit -Only $Only -DryRun:$DryRun
if ($result.RunRoot) { Write-Host "[editor-smoke] done: $(Join-Path $result.RunRoot 'driver.jsonl')" }
exit $result.ExitCode
