#Requires -Version 7.0
<#
.SYNOPSIS
BS5a の期待値 (i) を preflight.json から保存する（HANDOFF §5「判定 C の照合 script の方式」）。

.DESCRIPTION
840b6609 の Editor で旧 menu が出した preflight.json の selected の stable key を sorted して
expected/<contentSet>.baseline.keys に書き、出所を expected/<contentSet>.baseline.json に残す。
実装後の出力を期待値にしないため、-SourceCommit は旧 policy の commit を明示し、
unity/ がその commit と同一であること（git diff --stat <commit> -- unity が空）を呼ぶ側が先に確かめる。
既存の baseline は -Force が無ければ上書きしない。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory, Position = 0)] [string[]] $Preflight,
    [Parameter(Mandatory)] [string] $SourceCommit,
    [string] $ExpectedDir = (Join-Path $PSScriptRoot 'expected'),
    [switch] $Force
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Sha256Hex([string[]] $SortedKeys) {
    $text = ($SortedKeys -join "`n") + "`n"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    ([System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes) | ForEach-Object { $_.ToString('x2') }) -join ''
}

New-Item -ItemType Directory -Force -Path $ExpectedDir | Out-Null
foreach ($path in $Preflight) {
    $json = Get-Content -LiteralPath $path -Raw -Encoding utf8 | ConvertFrom-Json
    $contentSet = [string]$json.contentSet
    if ([string]::IsNullOrWhiteSpace($contentSet)) { throw "contentSet が空: $path" }
    if (@($json.issues).Count -gt 0) { throw "issues がある preflight は baseline にしない: $path" }
    $keys = @($json.selected | ForEach-Object {
        $last = $_.LastIndexOf('|'); $head = $_.Substring(0, $last); $head.Substring(0, $head.LastIndexOf('|'))
    } | Sort-Object -Culture '' -CaseSensitive -Unique)
    $keysPath = Join-Path $ExpectedDir "$contentSet.baseline.keys"
    $metaPath = Join-Path $ExpectedDir "$contentSet.baseline.json"
    if ((Test-Path -LiteralPath $keysPath) -and -not $Force) { throw "baseline が既にある（上書きは -Force）: $keysPath" }
    [System.IO.File]::WriteAllText($keysPath, (($keys -join "`n") + "`n"), [System.Text.UTF8Encoding]::new($false))
    $meta = [ordered]@{
        contentSet = $contentSet
        sourceCommit = $SourceCommit
        preflightIdentity = [string]$json.identity
        selectedCount = @($json.selected).Count
        sha256 = Get-Sha256Hex $keys
        capturedUtc = [DateTime]::UtcNow.ToString('o')
        note = '(i) 旧 policy の実測。実装の出力ではなく、sourceCommit の Editor で menu を実行して得た preflight.json の selected。'
    }
    [System.IO.File]::WriteAllText($metaPath, (($meta | ConvertTo-Json -Depth 3) + "`n"), [System.Text.UTF8Encoding]::new($false))
    Write-Output ("saved {0}: {1} keys sha256={2}" -f $contentSet, $keys.Count, $meta.sha256)
}
