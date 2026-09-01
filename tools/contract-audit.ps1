#requires -Version 7.0
<#
.SYNOPSIS
    AGENTS.md の常時契約のうち、機械で判定できるものだけを検査する。

.DESCRIPTION
    存在理由は Phase B の制約そのもの。実装担当は Unity.exe を起動しないので、
    **コンパイラが必ず捕まえる違反ですら、発見が Phase C まで遅れる**。
    その遅れを埋めるのがこのスクリプトで、狙いは条文を増やすことではなく、
    AGENTS.md 側の「読んで守る」条文を「書いたら弾かれる」に置き換えて
    常時契約の本数を増やさずに済ませること。

    したがって、人間のレビューでしか判定できない契約は対象にしない。
    責務配置、寿命スコープの妥当性、asmdef 参照の追加可否、UpdateSystem への
    登録漏れ、参照 0 の削除可否は、どれも設計判断なので Phase A / C に残す。

      検査1  #nullable enable が無い          差分に入った Unity側 .cs    → エラー
      検査2  record 宣言                      Unity側 .cs 全体            → エラー
      検査3  テストの実時間待ち               Task.Delay / Thread.Sleep   → エラー
      検査4  SceneState の14値と順序          減らす・並べ替えるを禁止    → エラー
      検査5  Runtime アセンブリの Editor 依存 asmdef 単位で判定           → エラー
      検査6  GetInstanceID()                  Unity 6.5 で CS0619         → エラー
      検査7  公開面への ZLogger 型漏れ        判断が要るので              → 警告

    検査1 だけが差分限定。未対応の既存ファイルが 71 件残っており（一括整備は
    別スライス）、全体検査にすると常時赤になって他の検査ごと無視されるため。
    「既存から外さない」側は、外したファイルが差分に入る以上、同じ検査で捕まる。

    検査2 と 検査6 は Unity のコンパイラも捕まえる。それでも入れているのは
    上の「Phase B は Unity を起動しない」ため。実測でどちらも Phase C まで
    生き残った実績がある（record は IsExternalInit が無くプロジェクト全体が
    コンパイル不能、GetInstanceID は obsolete-as-error で CS0619）。
    どちらも静的レビューでは指摘が出ない種類の違反。

    意図的に対象外：
      - Unity の偽 null（`== null` を使う契約）。型情報が要る。`?.` の grep は
        誤検知が多く、赤を無視する習慣を作るほうが害が大きい。Roslyn
        アナライザに載せるまで Phase C の目視に残す。
      - UniTask.Delay を使ったテストの待機。実質は Task.Delay と同じ失敗だが、
        AGENTS.md の条文は Task.Delay / Thread.Sleep しか禁じていない。
        条文に無いものをここで足すと、正本が2箇所になって腐る。
      - PR の base が develop か。ここは git の状態ではなく GitHub 側の属性。

    exit 0 = エラー 0 件。警告は exit code に影響しない。

.PARAMETER Root
    検査対象のリポジトリルート。既定はこのスクリプトの親の親。

.PARAMETER BaseRef
    検査1 の差分の基点。既定は origin/develop、無ければ develop。
    解決できない場合、検査1 を飛ばして緑にはせず、エラーとして報告する。

.EXAMPLE
    pwsh tools/contract-audit.ps1
.EXAMPLE
    pwsh tools/contract-audit.ps1 -BaseRef origin/main
#>
[CmdletBinding()]
param(
    [string]$Root = (Split-Path -Parent (Split-Path -Parent $PSCommandPath)),
    [string]$BaseRef = ''
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path (Join-Path $Root '.git'))) {
    Write-Error "git リポジトリではない: $Root"
}

$errors = @()
$warnings = @()

# --- 検査対象の確定 ---------------------------------------------------------
# ベンダー / Unity テンプレート同梱は対象外。こちらの契約を適用する筋合いが無く、
# TutorialInfo/ は asmdef を持たないので検査5 が構造的に赤くなる。
$vendorPrefixes = @(
    'unity/Assets/Packages/',
    'unity/Assets/MobileDependencyResolver/',
    'unity/Assets/TutorialInfo/'
)

function Test-InScope {
    param([string]$Rel)
    if (-not $Rel.EndsWith('.cs', 'OrdinalIgnoreCase')) { return $false }
    if (-not $Rel.StartsWith('unity/', 'OrdinalIgnoreCase')) { return $false }  # tools/DebugStudio は .NET 8
    foreach ($v in $vendorPrefixes) {
        if ($Rel.StartsWith($v, 'OrdinalIgnoreCase')) { return $false }
    }
    return $true
}

$tracked = @(git -C $Root ls-files)
if ($LASTEXITCODE -ne 0) { Write-Error "git ls-files に失敗した: $Root" }

$csFiles = @($tracked | Where-Object { Test-InScope $_ })
if ($csFiles.Count -eq 0) {
    # 「検査対象が無いので違反も無い」を成功と報告しない（docs-audit.ps1 と同じ理由）
    Write-Error "検査対象の Unity側 .cs が 0 件。checkout に失敗している: $Root"
}

# --- asmdef の解決 ----------------------------------------------------------
# .cs が属するアセンブリは、最も近い祖先ディレクトリの .asmdef で決まる。
# 検査5 の「Runtime アセンブリか否か」はこの対応でしか判定できない
# （実測: パス名に Editor が含まれるかで判定すると、Editor 専用 asmdef を
#   持つ SampleGame.DependOnAll.Editor/ が丸ごと誤検知になる）。
$asmdefByDir = @{}
foreach ($rel in @($tracked | Where-Object { $_.EndsWith('.asmdef', 'OrdinalIgnoreCase') })) {
    $full = Join-Path $Root $rel
    if (-not (Test-Path $full)) { continue }
    $json = Get-Content -LiteralPath $full -Encoding utf8 -Raw | ConvertFrom-Json
    $dir = ($rel -replace '/[^/]+$', '')
    $asmdefByDir[$dir] = [pscustomobject]@{
        Name       = [string]$json.name
        EditorOnly = (@($json.includePlatforms) -contains 'Editor')
    }
}

function Get-Assembly {
    param([string]$Rel)
    $parts = $Rel -split '/'
    for ($i = $parts.Count - 2; $i -ge 0; $i--) {
        $dir = ($parts[0..$i] -join '/')
        if ($asmdefByDir.ContainsKey($dir)) { return $asmdefByDir[$dir] }
    }
    # asmdef が無ければ Assembly-CSharp = Runtime 扱い（Editor 隔離が効いていない）
    return [pscustomobject]@{ Name = 'Assembly-CSharp'; EditorOnly = $false }
}

# --- 行の正規化 -------------------------------------------------------------
# コメントと文字列リテラルを落としてから検査する。落とさないと、契約を説明した
# コメント自身が違反として出る（実測: `GetInstanceID は使わない` と書いた
# XML doc コメントが検査6 に引っかかった）。
function Get-CodeLines {
    param([string[]]$Lines)
    $out = [System.Collections.Generic.List[string]]::new()
    $inBlock = $false
    foreach ($raw in $Lines) {
        $l = $raw
        if ($inBlock) {
            $end = $l.IndexOf('*/')
            if ($end -lt 0) { $out.Add(''); continue }
            $l = $l.Substring($end + 2)
            $inBlock = $false
        }
        $l = $l -replace '/\*.*?\*/', ' '
        $start = $l.IndexOf('/*')
        if ($start -ge 0) { $l = $l.Substring(0, $start); $inBlock = $true }
        $l = $l -replace '@"[^"]*"', '""'
        $l = $l -replace '"(\\.|[^"\\])*"', '""'
        $slash = $l.IndexOf('//')
        if ($slash -ge 0) { $l = $l.Substring(0, $slash) }
        $out.Add($l)
    }
    return $out.ToArray()
}

# --- 検査1: #nullable enable（差分限定） ------------------------------------
$baseCandidates = if ($BaseRef) { @($BaseRef) } else { @('origin/develop', 'develop') }
$baseCommit = $null
foreach ($cand in $baseCandidates) {
    $mb = git -C $Root merge-base $cand HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $mb) { $baseCommit = $mb.Trim(); $baseName = $cand; break }
}

$changed = @()
if (-not $baseCommit) {
    # 基点が無いのを「差分 0 件 = 違反 0 件」にすると、検査していないことを緑と誤報する
    $errors += "[検査1] 差分の基点を解決できなかった（試した: $($baseCandidates -join ', ')）。-BaseRef を指定する"
} else {
    # git diff <commit> は作業ツリーまで含む。コミット前の Phase B でも効かせるため。
    $diff = @(git -C $Root diff --name-only --diff-filter=ACMR $baseCommit --)
    if ($LASTEXITCODE -ne 0) { Write-Error "git diff に失敗した: $baseCommit" }
    $untracked = @(git -C $Root ls-files --others --exclude-standard)
    $changed = @(($diff + $untracked) | Sort-Object -Unique | Where-Object { Test-InScope $_ })

    foreach ($rel in $changed) {
        $full = Join-Path $Root $rel
        if (-not (Test-Path $full)) { continue }
        # 先頭 5 行まで許容（BOM 直後の空行とファイル冒頭の空行を通すため）
        $head = @(Get-Content -LiteralPath $full -Encoding utf8 -TotalCount 5)
        if (-not ($head | Where-Object { $_.Trim() -eq '#nullable enable' })) {
            $errors += "[検査1] $rel の先頭に #nullable enable が無い"
        }
    }
}

# --- 検査2・3・5・6・7（全体を1パスで） -------------------------------------
$recordPattern  = [regex]'(^|[^\w.])record\s+(class\s+|struct\s+)?[A-Za-z_]\w*\s*($|[({:<])'
$realWaitPattern = [regex]'(^|[^\w.])(Task\.Delay|Thread\.Sleep)\s*\('
$editorPattern  = [regex]'(^|[^\w.])UnityEditor\s*[.;]'
$instanceIdPattern = [regex]'\.GetInstanceID\s*\('
$zloggerPublicPattern = [regex]'^\s*(public|protected)\b.*\bI?ZLogger\w*\b'

# 検査7 の対象外。ZLogger を実装・適合させている層そのものなので、ここに型名が
# 出るのは当然。契約が守りたいのは「この層より外へ出さない」ほう。
$zloggerAdapterPrefixes = @(
    'unity/Assets/OneStarMaker/Scripts/Foundation/Logging/',
    'unity/Assets/OneStarMaker/Scripts/Foundation/Telemetry/'
)

$testAssemblies = 0
foreach ($rel in $csFiles) {
    $full = Join-Path $Root $rel
    if (-not (Test-Path $full)) {
        $errors += "[検査0] $rel は ls-files にあるが checkout されていない（部分 checkout 失敗）"
        continue
    }
    $asm = Get-Assembly $rel
    $isTest = ($asm.Name -match 'Tests|TestSupport')
    if ($isTest) { $testAssemblies++ }
    $isZLoggerAdapter = [bool](@($zloggerAdapterPrefixes | Where-Object { $rel.StartsWith($_, 'OrdinalIgnoreCase') }))

    $code = Get-CodeLines -Lines @(Get-Content -LiteralPath $full -Encoding utf8)
    for ($i = 0; $i -lt $code.Count; $i++) {
        $line = $code[$i]
        if ($line -eq '') { continue }
        $lineNo = $i + 1

        if ($recordPattern.IsMatch($line)) {
            $errors += "[検査2] ${rel}:${lineNo} Unity側で record を宣言している（IsExternalInit が無くコンパイル不能）"
        }
        if ($isTest -and $realWaitPattern.IsMatch($line)) {
            $errors += "[検査3] ${rel}:${lineNo} テストが実時間で待っている（シグナル待機か時間注入にする）"
        }
        if (-not $asm.EditorOnly -and $editorPattern.IsMatch($line)) {
            $errors += "[検査5] ${rel}:${lineNo} Runtime アセンブリ $($asm.Name) が UnityEditor に依存している"
        }
        if ($instanceIdPattern.IsMatch($line)) {
            $errors += "[検査6] ${rel}:${lineNo} GetInstanceID は Unity 6.5 で CS0619。EntityId.ToULong(GetEntityId()) を使う"
        }
        if (-not $isTest -and -not $isZLoggerAdapter -and $zloggerPublicPattern.IsMatch($line)) {
            $warnings += "[検査7] ${rel}:${lineNo} 公開面に ZLogger 型が出ている。ILogger<T> に留められないか確認する"
        }
    }
}

if ($testAssemblies -eq 0) {
    Write-Error "テストアセンブリの .cs が 0 件。検査3 が実行されていない: $Root"
}

# --- 検査4: SceneState の14値と順序 -----------------------------------------
# enum 順序は整数比較のガードに使われる。減らす・並べ替えるは、コンパイルも
# 通りテストも通ったうえで、実行時のガードだけが静かに壊れる。
$sceneStateRel = 'unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneState.cs'
$expectedStates = @(
    'None=0', 'PreLoading=1', 'PreLoaded=2', 'Loading=3', 'Loaded=4',
    'WaitLoadChildScene=5', 'Initializing=6', 'LoadCanceled=7', 'Stable=8',
    'PreUnloading=9', 'PreUnloaded=10', 'Unloading=11', 'Unloaded=12',
    'AfterUnloading=13'
)
$sceneStateFull = Join-Path $Root $sceneStateRel
if (-not (Test-Path $sceneStateFull)) {
    $errors += "[検査4] $sceneStateRel が無い。移動したなら contract-audit.ps1 の \$sceneStateRel も直す"
} else {
    $actual = @()
    $inEnum = $false
    foreach ($line in (Get-CodeLines -Lines @(Get-Content -LiteralPath $sceneStateFull -Encoding utf8))) {
        if (-not $inEnum) {
            if ($line -match '\benum\s+SceneState\b') { $inEnum = $true }
            continue
        }
        if ($line -match '^\s*\}') { break }
        if ($line -match '^\s*([A-Za-z_]\w*)\s*=\s*(\d+)\s*,?\s*$') {
            $actual += "$($Matches[1])=$($Matches[2])"
        }
    }
    if ($actual.Count -eq 0) {
        $errors += "[検査4] $sceneStateRel から enum の値を1件も読めなかった（検査が壊れている）"
    } else {
        $head = @($actual | Select-Object -First $expectedStates.Count)
        for ($i = 0; $i -lt $expectedStates.Count; $i++) {
            $got = if ($i -lt $head.Count) { $head[$i] } else { '(欠落)' }
            if ($got -ne $expectedStates[$i]) {
                $errors += "[検査4] SceneState の $($i + 1) 番目が $($expectedStates[$i]) でなく $got。既存14値は減らさず並べ替えない"
            }
        }
        if ($actual.Count -gt $expectedStates.Count) {
            $warnings += "[検査4] SceneState が $($actual.Count) 値ある。追加は Phase A の設計判断。HANDOFF に根拠があるか確認する"
        }
    }
}

# --- 報告 -------------------------------------------------------------------
Write-Host ''
Write-Host "対象: $Root"
Write-Host ("Unity側 .cs（ベンダー同梱を除く）: {0} ファイル" -f $csFiles.Count)
if ($baseCommit) {
    Write-Host ("検査1 の差分: {0} ファイル（基点 {1} = {2}）" -f $changed.Count, $baseName, $baseCommit.Substring(0, 7))
}
Write-Host ''

foreach ($w in $warnings) { Write-Host "WARN  $w" -ForegroundColor Yellow }
foreach ($e in $errors)   { Write-Host "ERROR $e" -ForegroundColor Red }

if ($errors.Count -gt 0) {
    Write-Host ''
    Write-Host ("違反 {0} 件。契約は AGENTS.md。" -f $errors.Count) -ForegroundColor Red
    exit 1
}

Write-Host '機械で判定できる契約に違反なし。' -ForegroundColor Green
Write-Host '設計判断（責務配置・寿命・依存・UpdateSystem 登録・偽 null）は対象外。' -ForegroundColor DarkGray
if ($warnings.Count -gt 0) {
    Write-Host ("警告 {0} 件（exit code には影響しない）。" -f $warnings.Count) -ForegroundColor Yellow
}
exit 0
