#requires -Version 7.0
<#
.SYNOPSIS
    contract-audit.ps1 の作業ツリー走査と既知の境界条件を一時 Git fixture で検証する。
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$auditPath = Join-Path $PSScriptRoot 'contract-audit.ps1'
$pwshPath = (Get-Process -Id $PID).Path
$fixturePrefix = 'osm-contract-audit-tests-'
$fixtureBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$fixtures = [System.Collections.Generic.List[string]]::new()
$passed = 0

function Write-FixtureFile {
    param(
        [Parameter(Mandatory)] [string]$Root,
        [Parameter(Mandatory)] [string]$RelativePath,
        [Parameter(Mandatory)] [string]$Content
    )

    $full = Join-Path $Root $RelativePath
    $parent = Split-Path -Parent $full
    [void](New-Item -ItemType Directory -Force -Path $parent)
    Set-Content -LiteralPath $full -Value $Content -Encoding utf8 -NoNewline
}

function New-AuditFixture {
    $root = Join-Path $fixtureBase ($fixturePrefix + [guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Path $root)
    $fixtures.Add($root)

    & git -C $root init -b develop --quiet
    if ($LASTEXITCODE -ne 0) { throw "fixture の git init に失敗した: $root" }
    & git -C $root config user.name 'Contract Audit Tests'
    & git -C $root config user.email 'contract-audit-tests@example.invalid'
    & git -C $root config core.autocrlf false

    Write-FixtureFile $root 'unity/Assets/Game/Game.asmdef' @'
{
  "name": "Game.Runtime"
}
'@
    Write-FixtureFile $root 'unity/Assets/Game/RuntimeMarker.cs' @'
#nullable enable
public sealed class RuntimeMarker { }
'@
    Write-FixtureFile $root 'unity/Assets/Game/Tests/Game.Tests.asmdef' @'
{
  "name": "Game.Tests"
}
'@
    Write-FixtureFile $root 'unity/Assets/Game/Tests/BaselineTests.cs' @'
#nullable enable
public sealed class BaselineTests { }
'@
    Write-FixtureFile $root 'unity/Assets/OneStarMaker/Scripts/Runtime/SceneSystem/SceneState.cs' @'
#nullable enable
public enum SceneState
{
    None = 0,
    PreLoading = 1,
    PreLoaded = 2,
    Loading = 3,
    Loaded = 4,
    WaitLoadChildScene = 5,
    Initializing = 6,
    LoadCanceled = 7,
    Stable = 8,
    PreUnloading = 9,
    PreUnloaded = 10,
    Unloading = 11,
    Unloaded = 12,
    AfterUnloading = 13,
}
'@

    & git -C $root add -A
    if ($LASTEXITCODE -ne 0) { throw "fixture の git add に失敗した: $root" }
    & git -C $root commit --quiet -m 'baseline'
    if ($LASTEXITCODE -ne 0) { throw "fixture の git commit に失敗した: $root" }
    return $root
}

function Invoke-FixtureAudit {
    param([Parameter(Mandatory)] [string]$Root)

    $output = & $pwshPath -NoProfile -File $auditPath -Root $Root -BaseRef develop 2>&1 |
        Out-String
    return [pscustomobject]@{
        ExitCode = $LASTEXITCODE
        Output = $output
    }
}

function Assert-AuditResult {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [int]$ExpectedExitCode,
        [Parameter(Mandatory)] [pscustomobject]$Result,
        [string]$ExpectedPattern = ''
    )

    if ($Result.ExitCode -ne $ExpectedExitCode) {
        throw "$Name`: exit $($Result.ExitCode), expected $ExpectedExitCode`n$($Result.Output)"
    }
    if ($ExpectedPattern -and $Result.Output -notmatch $ExpectedPattern) {
        throw "$Name`: expected pattern '$ExpectedPattern' was not found`n$($Result.Output)"
    }
    $script:passed++
    Write-Host "PASS $Name"
}

try {
    $root = New-AuditFixture
    Assert-AuditResult 'baseline' 0 (Invoke-FixtureAudit $root)

    $root = New-AuditFixture
    Write-FixtureFile $root 'unity/Assets/Game/UntrackedRecord.cs' @'
#nullable enable
public record UntrackedRecord { }
'@
    Assert-AuditResult 'untracked C# participates in full scan' 1 `
        (Invoke-FixtureAudit $root) '\[検査2\].*UntrackedRecord\.cs'

    $root = New-AuditFixture
    Write-FixtureFile $root 'unity/Assets/Game/EmptyRecord.cs' @'
#nullable enable
public record EmptyRecord;
'@
    Assert-AuditResult 'semicolon record declaration' 1 `
        (Invoke-FixtureAudit $root) '\[検査2\].*EmptyRecord\.cs'

    $root = New-AuditFixture
    Write-FixtureFile $root 'unity/Assets/Game/Tests/FqdnWaitTests.cs' @'
#nullable enable
public sealed class FqdnWaitTests
{
    public async System.Threading.Tasks.Task WaitAsync()
    {
        await System.Threading.Tasks.Task.Delay(10);
    }
}
'@
    Assert-AuditResult 'fully-qualified Task.Delay' 1 `
        (Invoke-FixtureAudit $root) '\[検査3\].*FqdnWaitTests\.cs'

    $root = New-AuditFixture
    Write-FixtureFile $root 'unity/Assets/Game/NewEditor/NewEditor.asmdef' @'
{
  "name": "Game.NewEditor",
  "includePlatforms": ["Editor"]
}
'@
    Write-FixtureFile $root 'unity/Assets/Game/NewEditor/NewEditorTool.cs' @'
#nullable enable
using UnityEditor;
public sealed class NewEditorTool { }
'@
    Assert-AuditResult 'untracked asmdef defines Editor boundary' 0 (Invoke-FixtureAudit $root)

    $root = New-AuditFixture
    Remove-Item -LiteralPath (Join-Path $root 'unity/Assets/Game/RuntimeMarker.cs')
    Write-FixtureFile $root 'unity/Assets/Game/RenamedRuntimeMarker.cs' @'
#nullable enable
public sealed class RenamedRuntimeMarker { }
'@
    Assert-AuditResult 'unstaged rename uses current worktree' 0 (Invoke-FixtureAudit $root)

    Write-Host "contract-audit fixtures: $passed passed"
}
finally {
    foreach ($root in $fixtures) {
        $full = [IO.Path]::GetFullPath($root)
        if (-not $full.StartsWith($fixtureBase, [StringComparison]::OrdinalIgnoreCase) -or
            -not ([IO.Path]::GetFileName($full)).StartsWith($fixturePrefix, [StringComparison]::Ordinal)) {
            throw "fixture cleanup の対象外パスを拒否した: $full"
        }
        if (Test-Path -LiteralPath $full) {
            Remove-Item -LiteralPath $full -Recurse -Force
        }
    }
}
