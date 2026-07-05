<#
.SYNOPSIS
  Addressables リモート配信用 ServerData を HTTP 静的配信する。

.DESCRIPTION
  Unity Addressables のリモート配信ビルド出力 (catalog.json / *.bundle 等) が
  配置される ServerData ディレクトリを、Python 標準ライブラリの http.server で
  ローカルネットワーク上に公開する。

  開発 PC や QA 端末から、BuildVariantProfile に設定した Remote Catalog URL
  (例: http://192.168.x.x:8080/StandaloneWindows64/catalog.json) で
  カタログとバンドルを取得できるようにするための簡易配信サーバー。

.PARAMETER Port
  待ち受け TCP ポート。既定値は 8080。

.PARAMETER Root
  配信ルートディレクトリ。既定値はリポジトリルートからの相対パス unity/ServerData。
  配下に [BuildTarget] フォルダ (例: StandaloneWindows64) が存在する想定。

.NOTES
  前提:
  - Python 3 がインストールされ、PATH 上で python または py -3 が利用可能であること。
  - rebuild-remote.ps1 でビルド済みの ServerData が存在すること。
  - ファイアウォールで指定 Port の受信を許可すること。

  使い方 (リポジトリルートから):
    .\tools\serve-addressables.ps1
    .\tools\serve-addressables.ps1 -Port 9090
    .\tools\serve-addressables.ps1 -Root "unity/ServerData"

  停止: Ctrl+C

  注意:
  - ビルド中に ServerData が上書きされると、配信中のクライアントが
    不整合なカタログ / バンドルを取得する可能性がある。
  - ビルドと配信は別タイミングで行うか、ビルド完了後に配信を再起動すること。
#>

param(
    [int]$Port = 8080,
    [string]$Root = "unity/ServerData"
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ServeRoot = if ([System.IO.Path]::IsPathRooted($Root)) { $Root } else { Join-Path $RepoRoot $Root }

if (-not (Test-Path -LiteralPath $ServeRoot -PathType Container)) {
    Write-Warning "配信ルートが存在しません: $ServeRoot"
    Write-Warning "先に tools/rebuild-remote.ps1 で Addressables ビルドを実行してください。"
    exit 1
}

Write-Host ""
Write-Host "=== Addressables HTTP 配信サーバー ===" -ForegroundColor Cyan
Write-Host "配信ルート: $ServeRoot"
Write-Host "ポート    : $Port"
Write-Host ""

Write-Host "この PC からアクセス可能な URL (IPv4):" -ForegroundColor Yellow
$ipAddresses = @(Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -ne "127.0.0.1" -and $_.PrefixOrigin -ne "WellKnown" } |
    Select-Object -ExpandProperty IPAddress -Unique)

if ($ipAddresses.Count -eq 0) {
    Write-Host "  (非ループバック IPv4 が見つかりませんでした)"
    Write-Host "  http://127.0.0.1:$Port/"
} else {
    foreach ($ip in $ipAddresses) {
        Write-Host "  http://${ip}:$Port/"
    }
}

Write-Host ""
Write-Host "例: http://<上記IP>:$Port/StandaloneWindows64/catalog.json"
Write-Host "停止: Ctrl+C"
Write-Host ""

$pythonCmd = Get-Command python -ErrorAction SilentlyContinue
if ($pythonCmd) {
    Write-Host "Python: $(python --version 2>&1)" -ForegroundColor DarkGray
    Set-Location -LiteralPath $ServeRoot
    python -m http.server $Port
    exit $LASTEXITCODE
}

$pyCmd = Get-Command py -ErrorAction SilentlyContinue
if ($pyCmd) {
    Write-Host "python コマンドが見つかりません。py -3 を使用します。" -ForegroundColor DarkYellow
    Write-Host "Python: $(py -3 --version 2>&1)" -ForegroundColor DarkGray
    Set-Location -LiteralPath $ServeRoot
    py -3 -m http.server $Port
    exit $LASTEXITCODE
}

Write-Error @"
Python 3 が見つかりません。
Python 3 をインストールするか、PATH に python / py を追加してください。
インストール後は次のいずれかで配信できます:
  python -m http.server $Port
  py -3 -m http.server $Port
"@
