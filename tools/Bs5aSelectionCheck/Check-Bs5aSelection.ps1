#Requires -Version 7.0
<#
.SYNOPSIS
BS5a（docs/handoff/BS5A_PURPOSE_SELECTION.md）の判定 C 用の照合 script。
content build が出した preflight.json の selected を、A3 で凍結した期待値と突き合わせる。

.DESCRIPTION
offline の pwsh だけで動く。Unity には依存しない。
期待値は expected/ の 3 種類で、出所を分けて保存する（HANDOFF §5）:
  (i)   <contentSet>.baseline.keys   840b6609 の Editor で実測した旧 policy の selected（Save-Bs5aBaseline.ps1 が書く）
  (ii)  attached-assets.keys         付随 asset 3 件の stable key（規則と GUID から導出）
  (iii) spring-planner-whitebox.keys 新しい宣言の期待（到達規則を手で適用した一覧）
Mode:
  Baseline: (i) だけと比べる。A3 の採取直後に、保存した baseline が preflight と一致することを確かめる用。
  Judgment: 既存 5 宣言は (i) + (ii)、spring-planner-whitebox は (iii) と比べる。requirements[] の到達経路も検査する。
bs4-spring-full は spring-full の baseline に、logical key `bs4:fixture:prefab` の候補 1 件（GUID は build ごとに変わる）を加えたものと比べる。
終了コードは全件 PASS で 0、1 件でも FAIL で 1。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)] [string[]] $Preflight,
    [ValidateSet('Baseline', 'Judgment')] [string] $Mode = 'Judgment',
    [string] $ExpectedDir = (Join-Path $PSScriptRoot 'expected'),
    [string[]] $Roots = @('OutGameScene', 'InGameScene')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:planner = 'spring-planner-whitebox'
$script:bs4 = 'bs4-spring-full'
$script:bs4ProbeLogicalKey = 'bs4:fixture:prefab'
$script:forbiddenForPlanner = @('_Environment_', '_Lighting_\d+_\d+$', '_VFX_\d+_\d+$')

function Read-KeyFile([string] $Path) {
    if (-not (Test-Path -LiteralPath $Path)) { throw "期待値 file が無い: $Path" }
    @(Get-Content -LiteralPath $Path -Encoding utf8 | Where-Object { $_ -ne '' -and -not $_.StartsWith('#') })
}

function Get-Sha256Hex([string[]] $SortedKeys) {
    $text = ($SortedKeys -join "`n") + "`n"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    ([System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''
}

function ConvertFrom-SelectedEntry([string] $Entry) {
    # 形式は stable|logical|physicalGuid。stable 自身が `|` を含むので末尾 2 つの区切りで分ける。
    $last = $Entry.LastIndexOf('|')
    if ($last -lt 0) { throw "selected の形式が違う: $Entry" }
    $guid = $Entry.Substring($last + 1)
    $head = $Entry.Substring(0, $last)
    $second = $head.LastIndexOf('|')
    if ($second -lt 0) { throw "selected の形式が違う: $Entry" }
    [pscustomobject]@{ Stable = $head.Substring(0, $second); Logical = $head.Substring($second + 1); Guid = $guid }
}

function Get-ExpectedKeys([string] $ContentSet) {
    if ($ContentSet -eq $script:planner) {
        if ($Mode -eq 'Baseline') { return $null }
        return [string[]](Read-KeyFile (Join-Path $ExpectedDir "$ContentSet.keys"))
    }
    $baselineName = if ($ContentSet -eq $script:bs4) { 'spring-full' } else { $ContentSet }
    $keys = [System.Collections.Generic.List[string]](Read-KeyFile (Join-Path $ExpectedDir "$baselineName.baseline.keys"))
    if ($Mode -eq 'Judgment') {
        foreach ($k in Read-KeyFile (Join-Path $ExpectedDir 'attached-assets.keys')) { $keys.Add($k) }
    }
    return [string[]]$keys.ToArray()
}

$results = New-Object System.Collections.Generic.List[object]
foreach ($path in $Preflight) {
    $fail = New-Object System.Collections.Generic.List[string]
    $note = New-Object System.Collections.Generic.List[string]
    $json = Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json
    $contentSet = [string]$json.contentSet
    $entries = @($json.selected | ForEach-Object { ConvertFrom-SelectedEntry $_ })
    if (@($json.issues).Count -gt 0) { $fail.Add("issues が空でない: " + (@($json.issues) -join '; ')) }

    $actualEntries = $entries
    if ($contentSet -eq $script:bs4) {
        $probe = @($entries | Where-Object { $_.Logical -eq $script:bs4ProbeLogicalKey })
        if ($probe.Count -ne 1) { $fail.Add("bs4 の probe 候補が 1 件でない: $($probe.Count)") }
        $actualEntries = @($entries | Where-Object { $_.Logical -ne $script:bs4ProbeLogicalKey })
    }
    $actual = @($actualEntries | ForEach-Object { $_.Stable } | Sort-Object -Culture '' -CaseSensitive)
    $actualSha = Get-Sha256Hex $actual

    $expected = Get-ExpectedKeys $contentSet
    if ($null -eq $expected) {
        $note.Add("Baseline mode では $contentSet の期待値を比べない")
    }
    else {
        $expectedSorted = @($expected | Sort-Object -Culture '' -CaseSensitive -Unique)
        $expectedSha = Get-Sha256Hex $expectedSorted
        if ($actual.Count -ne $expectedSorted.Count) { $fail.Add("件数が違う: actual $($actual.Count) / expected $($expectedSorted.Count)") }
        if ($actualSha -ne $expectedSha) {
            $missing = @($expectedSorted | Where-Object { $_ -notin $actual })
            $extra = @($actual | Where-Object { $_ -notin $expectedSorted })
            $fail.Add("sorted stable key の SHA-256 が違う。欠け $($missing.Count) 件、余分 $($extra.Count) 件")
            foreach ($m in $missing | Select-Object -First 10) { $note.Add("欠け: $m") }
            foreach ($e in $extra | Select-Object -First 10) { $note.Add("余分: $e") }
        }
    }

    if ($contentSet -eq $script:planner) {
        foreach ($pattern in $script:forbiddenForPlanner) {
            $hit = @($entries | Where-Object { $_.Logical -match $pattern })
            if ($hit.Count -gt 0) { $fail.Add("禁止 pattern '$pattern' に一致する selected が $($hit.Count) 件: " + (($hit | Select-Object -First 3 | ForEach-Object { $_.Logical }) -join ', ')) }
        }
    }

    $hasRequirements = $null -ne ($json.PSObject.Properties['requirements'])
    if ($Mode -eq 'Judgment') {
        if (-not $hasRequirements) { $fail.Add("preflight に requirements[] が無い（D4 の追加項目）") }
        else {
            $reqs = @($json.requirements)
            if ($reqs.Count -eq 0) { $fail.Add("requirements[] が空") }
            $badPath = @($reqs | Where-Object { @($_.path).Count -eq 0 -or ($_.path[0] -notin $Roots) })
            if ($badPath.Count -gt 0) { $fail.Add("到達経路が root から始まらない requirement が $($badPath.Count) 件") }
            $sortedOk = $true
            for ($i = 1; $i -lt $reqs.Count; $i++) {
                $prev = $reqs[$i - 1]; $cur = $reqs[$i]
                $cmp = [string]::CompareOrdinal([string]$prev.logicalKey, [string]$cur.logicalKey)
                if ($cmp -gt 0 -or ($cmp -eq 0 -and [string]::CompareOrdinal([string]$prev.representation, [string]$cur.representation) -gt 0)) { $sortedOk = $false; break }
            }
            if (-not $sortedOk) { $fail.Add("requirements[] が logicalKey, representation の ordinal で sort されていない") }
        }
    }
    elseif ($hasRequirements) { $note.Add("requirements[] がある（Baseline mode では検査しない）") }

    $results.Add([pscustomobject]@{
        Preflight = $path; ContentSet = $contentSet; Identity = [string]$json.identity
        Selected = $entries.Count; Compared = $actual.Count; Sha256 = $actualSha
        Result = $(if ($fail.Count -eq 0) { 'PASS' } else { 'FAIL' })
        Failures = $fail.ToArray(); Notes = $note.ToArray()
    })
}

foreach ($r in $results) {
    Write-Output ("[{0}] {1} identity={2} selected={3} compared={4} sha256={5}" -f $r.Result, $r.ContentSet, $r.Identity, $r.Selected, $r.Compared, $r.Sha256)
    foreach ($f in $r.Failures) { Write-Output "    FAIL: $f" }
    foreach ($n in $r.Notes) { Write-Output "    note: $n" }
}
$failed = @($results | Where-Object { $_.Result -eq 'FAIL' }).Count
Write-Output ("CHECK_RESULT mode={0} files={1} failed={2}" -f $Mode, $results.Count, $failed)
exit $(if ($failed -eq 0) { 0 } else { 1 })
