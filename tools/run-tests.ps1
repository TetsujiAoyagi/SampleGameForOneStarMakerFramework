<#
.SYNOPSIS
    Unity EditMode テストをバッチモードで実行し、機械判定可能な結果を返す。

.DESCRIPTION
    docs/planning 配下に散っていた Unity.exe -batchmode -runTests の呼び出しを1本に固定化したもの。
    過去の実測で判明した以下の罠を全て内包している:

      * Unity.exe は即座に制御を返すため WaitForExit が必要
        （SCENE_STREAMING_T02_HANDOFF_2026-07-06.md）
      * テスト0件実行を成功扱いしてはならない（コンパイルエラーは0件として現れる）
        （CAMERA_SYSTEM_BOOTSTRAP_EXECUTION_PLAN_2026-07-11.md）
      * Editor を開いたままだとプロジェクトロックで失敗する
        （SCENE_STREAMING_T02_HANDOFF_2026-07-06.md）
      * マシンに複数の Unity が入っているため、パスは ProjectVersion.txt から導出する

.PARAMETER Filter
    -testFilter に渡す値。既定は空 = 全 EditMode テストを実行する。

    既存ドキュメントは全体回帰に -testFilter "OneStarMaker.Tests" を使っていたが踏襲しない。
    OneStarMaker.Tests.Editor は別 asmdef でありながら namespace が入れ子のため
    部分一致でたまたま拾えているだけで、namespace を切り出した瞬間に静かに漏れる。
    フィルタは絞り込みたいときだけのオプトインとする。

.PARAMETER Platform
    -testPlatform に渡す値。既定 EditMode。
    OneStarMaker.Tests / OneStarMaker.Tests.Editor は共に includePlatforms: ["Editor"] のため
    EditMode で全件がカバーされる。

.PARAMETER UnityRoot
    Unity のインストール親ディレクトリ。環境差分はここだけ。

.PARAMETER UnityExe
    Unity.exe を直接指定して UnityRoot / ProjectVersion 導出を上書きする。

.PARAMETER WithGraphics
    -nographics を付けない。グラフィックスデバイスを要求するテストがある場合のみ。

.EXAMPLE
    ./tools/run-tests.ps1
    全 EditMode テストを実行する（Phase C の回帰判定はこれ）。

.EXAMPLE
    ./tools/run-tests.ps1 -Filter OneStarMaker.Tests.AssetManagement
    AssetManagement のテストだけ実行する。

.OUTPUTS
    exit 0 = 成功（1件以上実行され failed 0）
    exit 1 = 失敗（実行0件 / failed>0 / 起動不能 / ロック）
#>

[CmdletBinding()]
param(
    [string]$Filter    = "",
    [string]$Platform  = "EditMode",
    [string]$UnityRoot = "D:\UnityEditor",
    [string]$UnityExe  = "",
    [switch]$WithGraphics
)

$ErrorActionPreference = 'Stop'

$repoRoot    = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'unity'
$resultsDir  = Join-Path $repoRoot 'TestResults'

function Write-Section($text) {
    Write-Host ""
    Write-Host "=== $text ===" -ForegroundColor Cyan
}

function Fail($message) {
    Write-Host ""
    Write-Host "FAILED: $message" -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------------------
# 1. Unity.exe の解決（ProjectVersion.txt から導出。ベタ書きしない）
# ---------------------------------------------------------------------------
Write-Section "Unity の解決"

if ($UnityExe) {
    if (-not (Test-Path $UnityExe)) { Fail "指定された Unity.exe が存在しません: $UnityExe" }
    $unity = $UnityExe
    Write-Host "Unity      : $unity （-UnityExe で明示指定）"
}
else {
    $versionFile = Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt'
    if (-not (Test-Path $versionFile)) { Fail "ProjectVersion.txt が見つかりません: $versionFile" }

    $versionLine = Select-String -Path $versionFile -Pattern '^m_EditorVersion:\s*(.+)$' | Select-Object -First 1
    if (-not $versionLine) { Fail "ProjectVersion.txt から m_EditorVersion を読めませんでした" }

    $editorVersion = $versionLine.Matches[0].Groups[1].Value.Trim()
    $unity = Join-Path $UnityRoot "$editorVersion/Editor/Unity.exe"

    if (-not (Test-Path $unity)) {
        Write-Host "プロジェクトが要求する Unity が見つかりません: $unity" -ForegroundColor Red
        Write-Host "要求バージョン: $editorVersion"
        Write-Host "$UnityRoot にインストール済みのもの:"
        if (Test-Path $UnityRoot) {
            Get-ChildItem $UnityRoot -Directory | ForEach-Object { Write-Host "  - $($_.Name)" }
        } else {
            Write-Host "  （$UnityRoot が存在しない。-UnityRoot か -UnityExe を指定してください）"
        }
        Fail "バージョン不一致のまま実行すると原因不明の失敗になるため停止します"
    }

    Write-Host "Unity      : $unity"
    Write-Host "バージョン : $editorVersion （ProjectVersion.txt から導出）"
}

Write-Host "プロジェクト: $projectPath"
Write-Host "プラットフォーム: $Platform"
Write-Host "フィルタ    : $(if ($Filter) { $Filter } else { '(なし = 全件)' })"

# ---------------------------------------------------------------------------
# 2. Editor 起動中の検出（ロックで落ちる前に明示エラーにする）
# ---------------------------------------------------------------------------
# UnityLockfile の「存在」は Editor 起動中の代理指標にすぎない。
# Unity はバッチ実行の終了時にアクセス違反でクラッシュすることがあり（実測: 終了コード
# -1073741819）、その場合ロックファイルが残骸として残って以降の実行を全部塞ぐ。
# そのため存在ではなく「誰かが掴んでいるか」を排他オープンで実地に判定する。
$lockFile = Join-Path $projectPath 'Temp/UnityLockfile'
if (Test-Path $lockFile) {
    $heldByProcess = $false
    try {
        $fs = [System.IO.File]::Open($lockFile, 'Open', 'ReadWrite', 'None')
        $fs.Close()
        $fs.Dispose()
    }
    catch [System.IO.IOException] {
        $heldByProcess = $true
    }

    if ($heldByProcess) {
        Write-Host ""
        Write-Host "Unity Editor がこのプロジェクトを開いています（$lockFile をプロセスが保持中）。" -ForegroundColor Red
        Write-Host "バッチモードはプロジェクトロックを取得できないため失敗します。Editor を閉じてから再実行してください。"
        Fail "Editor 起動中"
    }

    Write-Host "残留していた UnityLockfile を削除しました（前回実行のクラッシュ残骸。プロセスは保持していない）。" -ForegroundColor Yellow
    Remove-Item $lockFile -Force
}

# ---------------------------------------------------------------------------
# 3. 出力先
# ---------------------------------------------------------------------------
if (-not (Test-Path $resultsDir)) { New-Item -ItemType Directory -Path $resultsDir | Out-Null }

$stamp     = Get-Date -Format 'yyyyMMdd-HHmmss'
$slug      = if ($Filter) { ($Filter -replace '[^A-Za-z0-9]+', '-').Trim('-') } else { 'all' }
$resultXml = Join-Path $resultsDir "results-$slug-$stamp.xml"
$logFile   = Join-Path $resultsDir "unity-$slug-$stamp.log"

# ---------------------------------------------------------------------------
# 4. 実行
# ---------------------------------------------------------------------------
Write-Section "実行"

$unityArgs = @(
    '-batchmode'
    '-projectPath'; $projectPath
    '-runTests'
    '-testPlatform'; $Platform
    '-testResults'; $resultXml
    '-logFile'; $logFile
)
if (-not $WithGraphics) { $unityArgs = @('-nographics') + $unityArgs }
# 空フィルタで -testFilter を渡すと 0 件になるため、値があるときだけ付ける
if ($Filter) { $unityArgs += @('-testFilter'; $Filter) }

Write-Host "全 EditMode 回帰は数分〜10分程度かかります（Timeout 系テストが実時間を消費するため）。"
Write-Host "ログ: $logFile"
Write-Host ""

$sw = [System.Diagnostics.Stopwatch]::StartNew()

# Unity.exe は起動直後に制御を返すため、プロセスハンドルを掴んで明示的に待つ。
$proc = Start-Process -FilePath $unity -ArgumentList $unityArgs -PassThru
$proc.WaitForExit()

$sw.Stop()
$unityExit = $proc.ExitCode
Write-Host "Unity 終了コード: $unityExit （所要 $([math]::Round($sw.Elapsed.TotalMinutes, 1)) 分）"

# ---------------------------------------------------------------------------
# 5. コンパイルエラーの抽出（テスト0件の原因はたいていこれ）
# ---------------------------------------------------------------------------
$compileErrors = @()
if (Test-Path $logFile) {
    $compileErrors = Select-String -Path $logFile -Pattern 'error CS\d+' |
        ForEach-Object { $_.Line.Trim() } |
        Select-Object -Unique
}

if ($compileErrors.Count -gt 0) {
    Write-Section "コンパイルエラー ($($compileErrors.Count) 件)"
    $compileErrors | Select-Object -First 30 | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
    if ($compileErrors.Count -gt 30) { Write-Host "  … 他 $($compileErrors.Count - 30) 件（$logFile 参照）" }
}

# ---------------------------------------------------------------------------
# 6. 結果の判定
# ---------------------------------------------------------------------------
Write-Section "結果"

if (-not (Test-Path $resultXml)) {
    Write-Host "結果 XML が生成されませんでした: $resultXml" -ForegroundColor Red
    Write-Host "Unity がテストを開始する前に落ちています。ログを確認してください: $logFile"
    Fail "結果 XML なし"
}

[xml]$xml = Get-Content $resultXml
$run = $xml.'test-run'

$total   = [int]$run.total
$passed  = [int]$run.passed
$failed  = [int]$run.failed
$skipped = [int]$run.skipped

Write-Host "  total   : $total"
Write-Host "  passed  : $passed"
Write-Host "  failed  : $failed"
Write-Host "  skipped : $skipped"
Write-Host "  XML     : $resultXml"

if ($failed -gt 0) {
    Write-Section "失敗したテスト"
    $xml.SelectNodes("//test-case[@result='Failed']") | ForEach-Object {
        Write-Host "  ✗ $($_.fullname)" -ForegroundColor Red
        $msg = $_.failure.message.'#cdata-section'
        if ($msg) { Write-Host "      $(($msg -split "`n")[0].Trim())" -ForegroundColor DarkGray }
    }
}

# テスト0件を成功扱いしない。コンパイルエラーもフィルタの打ち間違いも0件として現れるため、
# ここを緩めると「緑に見える壊れたビルド」を通してしまう。
if ($total -eq 0) {
    Write-Host ""
    Write-Host "テストが1件も実行されていません。" -ForegroundColor Red
    if ($compileErrors.Count -gt 0) {
        Write-Host "原因: 上記のコンパイルエラー。"
    } elseif ($Filter) {
        Write-Host "原因: -Filter '$Filter' に一致するテストが無い可能性があります。"
    } else {
        Write-Host "原因: ログを確認してください: $logFile"
    }
    Fail "実行件数 0（成功扱いしない）"
}

if ($failed -gt 0) { Fail "$failed 件のテストが失敗" }

Write-Host ""
Write-Host "OK: $passed / $total 件成功" -ForegroundColor Green
exit 0
