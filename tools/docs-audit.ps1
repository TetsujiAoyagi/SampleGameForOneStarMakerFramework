#requires -Version 7.0
<#
.SYNOPSIS
    ドキュメント3層（公開面 / 作業台 / 手元）の境界が破れていないか検査する。

.DESCRIPTION
    方針は docs/README.md。実際に起きた3つの失敗だけを検査する。

      検査0  tracked なのに実体が無い（部分 checkout 失敗）  → エラー
             （実例: 長いパスへの clone が MAX_PATH で半分だけ失敗し、緑に見えた）
      検査1  tracked な md のリンク先が git に無い          → エラー
             （実例: README.md が gitignore 済みの docs/reference/ を案内していた）
      検査2  層をまたぐ参照                                  → エラー
             手元層（git 管理外）は**ディレクトリ名の言及だけでアウト**。clone に
             存在しない先を指すため。
             作業台 docs/handoff/ は tracked なのでリンクは切れない。問題は一時
             文書への依存なので、**個別 HANDOFF ファイルの参照だけアウト**とし、
             方針としてのディレクトリ言及は許す（ルート README は3層を説明する
             必要がある）。
             slice / program HANDOFF も worktree へ渡すので、手元層へ依存させない。
             research HANDOFF はローカル運用自体を調査対象にできるため例外とする。
             （実例: elastic/queries/README.md が HANDOFF §7 を指していた）
      検査3  §7 と §8 が両方埋まった HANDOFF が残っている    → 警告
             （マージ済みなのに harvest されていない）

    exit 0 = 検査1・2 に違反なし。警告は exit code に影響しない。

.PARAMETER Root
    検査対象のリポジトリルート。既定はこのスクリプトの親の親。
    shallow clone を検査するときに使う。

.EXAMPLE
    pwsh tools/docs-audit.ps1
.EXAMPLE
    git clone --depth 1 . /tmp/pubcheck; pwsh tools/docs-audit.ps1 -Root /tmp/pubcheck
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent (Split-Path -Parent $PSCommandPath))
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $Root '.git'))) {
    Write-Error "git リポジトリではない: $Root"
}

# --- tracked ファイル一覧（検査1 の「存在する」の定義そのもの） --------------
$tracked = @(git -C $Root ls-files)
if ($LASTEXITCODE -ne 0) { Write-Error "git ls-files に失敗した: $Root" }

$trackedSet = [System.Collections.Generic.HashSet[string]]::new(
    [string[]]$tracked, [System.StringComparer]::OrdinalIgnoreCase)

# ディレクトリへのリンクを許すため、tracked なファイルの親を全て集める
$trackedDirs = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase)
foreach ($f in $tracked) {
    $parts = $f -split '/'
    for ($i = 1; $i -lt $parts.Count; $i++) {
        [void]$trackedDirs.Add(($parts[0..($i - 1)] -join '/'))
    }
}

if ($tracked.Count -eq 0) {
    Write-Error "tracked ファイルが 0 件。checkout に失敗しているか、リポジトリが空: $Root"
}

$mdFiles = @($tracked | Where-Object { $_ -like '*.md' })
if ($mdFiles.Count -eq 0) {
    # 「検査対象が無いので違反も無い」を成功と報告しない（実測: clone 失敗を緑と誤報した）
    Write-Error "tracked な md が 0 件。検査対象が存在しない: $Root"
}

# ベンダー同梱の README は対象外（3rd party のもので、こちらの方針は適用しない）。
# `.agents/skills/unity-cli/` は公式スキル。references/*.md は未 tracked で、検査1 が常に赤になる。
$vendorPrefixes = @(
    'unity/Assets/Packages/',
    'unity/Assets/MobileDependencyResolver/',
    '.agents/skills/unity-cli/'
)
$ownMd = @($mdFiles | Where-Object {
    $p = $_
    -not ($vendorPrefixes | Where-Object { $p.StartsWith($_, 'OrdinalIgnoreCase') })
})

$errors = @()
$warnings = @()

# --- 検査1: リンク切れ ------------------------------------------------------
foreach ($rel in $ownMd) {
    $full = Join-Path $Root $rel
    if (-not (Test-Path $full)) {
        # ls-files にあるのに実体が無い = checkout が部分的に失敗している。
        # 黙って飛ばすと「残ったファイルに違反が無いので緑」になり、
        # 検証手段が壊れていることを成功と誤報する（0 件エラーと同じ理由）。
        $errors += "[検査0] $rel は ls-files にあるが checkout されていない（部分 checkout 失敗）"
        continue
    }
    $dir = Split-Path -Parent $rel   # リポジトリ相対
    $lineNo = 0
    foreach ($line in (Get-Content -LiteralPath $full -Encoding utf8)) {
        $lineNo++
        foreach ($m in [regex]::Matches($line, '\]\(([^)]+)\)')) {
            $target = $m.Groups[1].Value.Trim()

            # 外部 URL・アンカーのみ・mailto は対象外
            if ($target -match '^(https?:|mailto:|#)') { continue }
            $target = ($target -split '#')[0]
            if ([string]::IsNullOrWhiteSpace($target)) { continue }

            # リポジトリ相対へ正規化
            $combined = if ($dir) { "$dir/$target" } else { $target }
            $stack = [System.Collections.Generic.List[string]]::new()
            foreach ($seg in ($combined -replace '\\', '/' -split '/')) {
                if ($seg -eq '' -or $seg -eq '.') { continue }
                if ($seg -eq '..') {
                    if ($stack.Count -gt 0) { $stack.RemoveAt($stack.Count - 1) }
                    continue
                }
                $stack.Add($seg)
            }
            $norm = ($stack -join '/')
            if ([string]::IsNullOrWhiteSpace($norm)) { continue }

            if (-not ($trackedSet.Contains($norm) -or $trackedDirs.Contains($norm))) {
                $errors += "[検査1] ${rel}:${lineNo} リンク先が git に無い → $norm"
            }
        }
    }
}

# --- 検査2: 層をまたぐ参照 --------------------------------------------------
# 「手元」層は git 管理外。公開面が指すと clone した人には存在しない先になる。
# ディレクトリ名の言及自体を禁止する。
$localTiers = @(
    'docs/planning/',
    'docs/reference/',
    'docs/slides/',
    'docs/debugstudio/',
    'docs/agents/'
)
# docs/README.md は方針そのものを説明する文書なので、層名の言及を許可する
$tierMentionAllowed = @('docs/README.md')

foreach ($rel in $ownMd) {
    $isHandoff = $rel.StartsWith('docs/handoff/', 'OrdinalIgnoreCase')
    if ($tierMentionAllowed -contains $rel) { continue }

    $full = Join-Path $Root $rel
    if (-not (Test-Path $full)) { continue }
    $handoffType = ''
    if ($isHandoff) {
        foreach ($metaLine in (Get-Content -LiteralPath $full -Encoding utf8 | Select-Object -First 20)) {
            if ($metaLine -match '^[>\s-]*type:\s*(slice|program|research)\s*$') {
                $handoffType = $Matches[1].ToLowerInvariant()
                break
            }
        }
    }
    if ($isHandoff -and $handoffType -eq 'research') { continue }
    $lineNo = 0
    foreach ($line in (Get-Content -LiteralPath $full -Encoding utf8)) {
        $lineNo++

        # 手元層: ディレクトリ名の言及だけでアウト（clone に存在しない先を指すため）
        $hit = $false
        foreach ($tier in $localTiers) {
            if ($line -like "*$tier*") {
                $errors += "[検査2] ${rel}:${lineNo} 公開面が手元層（git 管理外）を参照している → $tier"
                $hit = $true
                break
            }
        }
        if ($hit) { continue }

        # HANDOFF 同士の参照は作業台内の一時的な関係なので許す。
        if ($isHandoff) { continue }

        # 作業台: tracked なのでリンク切れにはならない。問題は「一時文書に依存すること」。
        # したがって個別の HANDOFF ファイルを指すのはアウト、方針としてディレクトリに
        # 言及するのはセーフ（ルート README は3層を説明する必要がある）。
        if ($line -match 'docs/handoff/[^/\s)\]`"]+\.md') {
            $errors += "[検査2] ${rel}:${lineNo} 公開面が個別の HANDOFF に依存している → $($Matches[0])"
        }
    }
}

# --- 検査3: harvest されていない HANDOFF ------------------------------------
$handoffs = @($mdFiles | Where-Object { $_.StartsWith('docs/handoff/', 'OrdinalIgnoreCase') })
foreach ($rel in $handoffs) {
    $full = Join-Path $Root $rel
    if (-not (Test-Path $full)) { continue }
    $lines = @(Get-Content -LiteralPath $full -Encoding utf8)

    # 「## 7.」「## §7.」形式の節を拾い、次の「## 」までの中身が空でないかを見る
    function Get-SectionBodyCount {
        param([string[]]$Lines, [int]$Number)
        $inSection = $false
        $count = 0
        foreach ($l in $Lines) {
            if ($l -match '^##\s+§?(\d+)[\.\s]') {
                $inSection = ([int]$Matches[1] -eq $Number)
                continue
            }
            if (-not $inSection) { continue }
            $t = $l.Trim()
            if ($t -eq '') { continue }
            if ($t -match '^-{3,}$') { continue }                       # 水平線
            if ($t -match '^[（(]?(未記入|未実施|未着手|なし|TBD|N/?A)[)）]?$') { continue }
            if ($t -match '^-[\s]+[^:]+:[\s]*$') { continue }         # テンプレートの未記入フィールド
            $count++
        }
        return $count
    }

    $c7 = Get-SectionBodyCount -Lines $lines -Number 7
    $c8 = Get-SectionBodyCount -Lines $lines -Number 8

    if ($c7 -gt 0 -and $c8 -gt 0) {
        $warnings += "[検査3] $rel は §7(C) / §8(C') が両方埋まっている。harvest して git rm する頃合い"
    }
}

# --- 規模の報告（増減を見るため） -------------------------------------------
$totalLines = 0
foreach ($rel in $ownMd) {
    $full = Join-Path $Root $rel
    if (Test-Path $full) {
        $totalLines += @(Get-Content -LiteralPath $full -Encoding utf8).Count
    }
}

Write-Host ''
Write-Host "対象: $Root"
Write-Host ("tracked な md（ベンダー同梱を除く）: {0} ファイル / {1} 行" -f $ownMd.Count, $totalLines)
Write-Host ("  うち作業台 docs/handoff/: {0} ファイル" -f $handoffs.Count)
Write-Host ''

foreach ($w in $warnings) { Write-Host "WARN  $w" -ForegroundColor Yellow }
foreach ($e in $errors)   { Write-Host "ERROR $e" -ForegroundColor Red }

if ($errors.Count -gt 0) {
    Write-Host ''
    Write-Host ("違反 {0} 件。方針は docs/README.md。" -f $errors.Count) -ForegroundColor Red
    exit 1
}

Write-Host '検査1・2 に違反なし。' -ForegroundColor Green
if ($warnings.Count -gt 0) {
    Write-Host ("警告 {0} 件（exit code には影響しない）。" -f $warnings.Count) -ForegroundColor Yellow
}
exit 0
