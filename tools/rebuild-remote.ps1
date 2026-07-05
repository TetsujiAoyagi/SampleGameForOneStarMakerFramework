<#
.SYNOPSIS
  git pull 後に Unity バッチモードで RemoteFull Addressables ビルドを実行する。

.DESCRIPTION
  リモート PC 上で Addressables リモート配信コンテンツを再生成する自動化スクリプト。
  リポジトリを fast-forward で更新し、VariantRemoteBuildBatch.Build を
  Unity バッチモードから呼び出して ServerData/[BuildTarget]/ を出力する。

  配信自体は tools/serve-addressables.ps1 を別プロセスで起動して行う。
  本スクリプトはビルドのみを担当する。

.PARAMETER UnityPath
  Unity Editor 実行ファイルのフルパス (例: C:\Program Files\Unity\Hub\Editor\6000.0.0f1\Editor\Unity.exe)。
  空の場合は環境変数 UNITY_PATH を参照する。

.PARAMETER ProjectPath
  Unity プロジェクトディレクトリ。リポジトリルートからの相対パス。既定値 unity。

.PARAMETER VariantProfile
  ビルドに使用する BuildVariantProfile のアセットパス。
  既定値は RemoteFull (Assets/OneStarMaker/Editor/BuildProfiles/RemoteFull.asset)。

.NOTES
  前提:
  - Git リポジトリが clone 済みで、リモートから pull 可能であること。
  - Unity Editor (Addressables 2.x) がインストール済みであること。
  - Addressables Settings に VariantFilteringBuildScript が登録済みであること。
  - RemoteFull プロファイルおよび Remote Addressables 構成がセットアップ済みであること
    (OneStarMaker/Addressables/Setup Remote Distribution)。

  使い方 (リポジトリルートから):
    $env:UNITY_PATH = "C:\Program Files\Unity\Hub\Editor\6000.0.0f1\Editor\Unity.exe"
    .\tools\rebuild-remote.ps1
    .\tools\rebuild-remote.ps1 -UnityPath "C:\...\Unity.exe"

  ビルド成功後:
    .\tools\serve-addressables.ps1

  アトミック切替について:
  Unity ビルドは Addressables 設定に従い unity/ServerData/ に直接出力される。
  本スクリプトでは ServerData.staging への退避や ServerData.prev への
  アトミック切替は行わない。
  中間状態を配信しないため、配信プロセス (serve-addressables.ps1) と
  ビルドは別タイミングで行うか、ビルド完了を待って配信を再起動すること。
#>

param(
    [string]$UnityPath = "",
    [string]$ProjectPath = "unity",
    [string]$VariantProfile = "Assets/OneStarMaker/Editor/BuildProfiles/RemoteFull.asset"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $RepoRoot

Write-Host ""
Write-Host "=== Addressables リモート配信ビルド ===" -ForegroundColor Cyan
Write-Host "リポジトリ: $RepoRoot"
Write-Host ""

Write-Host "[1/3] git pull --ff-only ..." -ForegroundColor Green
try {
    git pull --ff-only
    if ($LASTEXITCODE -ne 0) {
        Write-Error "git pull --ff-only が失敗しました (exit $LASTEXITCODE)。ビルドを中断します。"
    }
} catch {
    Write-Error "git pull --ff-only でエラーが発生しました: $_"
}

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    $UnityPath = $env:UNITY_PATH
}

if ([string]::IsNullOrWhiteSpace($UnityPath)) {
    Write-Error @"
Unity Editor のパスが指定されていません。
-UnityPath パラメータで Unity.exe のフルパスを指定するか、
環境変数 UNITY_PATH を設定してください。

例:
  `$env:UNITY_PATH = "C:\Program Files\Unity\Hub\Editor\6000.0.0f1\Editor\Unity.exe"
  .\tools\rebuild-remote.ps1
"@
}

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    Write-Error "Unity 実行ファイルが見つかりません: $UnityPath"
}

$FullProjectPath = if ([System.IO.Path]::IsPathRooted($ProjectPath)) {
    $ProjectPath
} else {
    Join-Path $RepoRoot $ProjectPath
}

if (-not (Test-Path -LiteralPath $FullProjectPath -PathType Container)) {
    Write-Error "Unity プロジェクトが見つかりません: $FullProjectPath"
}

Write-Host ""
Write-Host "[2/3] Unity バッチモードで Addressables ビルド ..." -ForegroundColor Green
Write-Host "  Unity       : $UnityPath"
Write-Host "  ProjectPath : $FullProjectPath"
Write-Host "  Profile     : $VariantProfile"
Write-Host ""

& $UnityPath `
    -batchmode `
    -quit `
    -projectPath $FullProjectPath `
    -executeMethod OneStarMaker.Editor.Build.VariantRemoteBuildBatch.Build `
    -variantProfile $VariantProfile `
    -logFile -

$unityExitCode = $LASTEXITCODE

Write-Host ""
if ($unityExitCode -ne 0) {
    Write-Warning "ビルド失敗 (Unity exit code: $unityExitCode)。"
    Write-Warning "前回の ServerData を配信し続けてください。"
    Write-Warning "serve-addressables.ps1 を再起動する必要はありません (配信中の場合)。"
    exit 1
}

Write-Host "[3/3] ビルド成功。" -ForegroundColor Green
Write-Host ""
Write-Host "出力先: $(Join-Path $FullProjectPath 'ServerData')" -ForegroundColor Yellow
Write-Host ""
Write-Host "次のコマンドで HTTP 配信を開始 (または再起動) してください:" -ForegroundColor Cyan
Write-Host "  .\tools\serve-addressables.ps1"
Write-Host ""
