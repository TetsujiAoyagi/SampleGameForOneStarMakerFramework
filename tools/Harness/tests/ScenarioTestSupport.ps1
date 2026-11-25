# 実機の検証の基盤の offline test が共有する fixture と Run / Assert。各 test file が dot-source する。
# 一時の git repo と linked worktree を fixture にし、実の Unity も Unity CLI も起動しない。
# 呼び出し側は $Filter を定義してから dot-source する。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoUnderTest = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
Import-Module (Join-Path $repoUnderTest 'tools/Harness/UnityProcess.psm1') -Force
Import-Module (Join-Path $repoUnderTest 'tools/Harness/ScenarioRun.psm1') -Force
Import-Module (Join-Path $repoUnderTest 'tools/Harness/ScenarioCheckout.psm1') -Force
Import-Module (Join-Path $repoUnderTest 'tools/Harness/Scenario.psm1') -Force
. (Join-Path $repoUnderTest 'tools/Harness/scenario-checkout.ps1')

$failed = [Collections.Generic.List[string]]::new()
$executed = 0
$registered = [Collections.Generic.List[string]]::new()
$selected = [Collections.Generic.List[string]]::new()
$executedNames = [Collections.Generic.List[string]]::new()
function Assert($condition, [string]$message) { if (-not $condition) { throw $message } }
function Run([string]$name, [scriptblock]$body) {
    $script:registered.Add($name)
    if ($Filter -and $name -notlike "*$Filter*") { return }
    $script:selected.Add($name)
    $script:executed++
    $script:executedNames.Add($name)
    try { & $body; [Console]::WriteLine("PASS $name") }
    catch { $script:failed.Add("$name : $($_.Exception.Message)"); [Console]::Error.WriteLine("FAIL $name : $($_.Exception.Message) $($_.ScriptStackTrace)") }
}

$script:tempRoot = Join-Path ([IO.Path]::GetTempPath()) ('osm-s4v-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
[void][IO.Directory]::CreateDirectory($script:tempRoot)
$script:fixtureCount = 0

function Invoke-Git([string]$Root, [string[]]$Arguments) {
    $output = & git -C $Root @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed: $output" }
    return $output
}

function Write-Text([string]$Path, [string]$Text) {
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function New-FixtureRepo([scriptblock]$Extra = $null) {
    # 毎回新しい repo と置き場を作る。主の worktree は <fixture>/repo、検証用の checkout の置き場は <fixture>/osm-v。
    # Extra は ($repo) を受け、最初の commit の前に file を足す。
    $script:fixtureCount++
    $fixture = Join-Path $script:tempRoot ("f$script:fixtureCount")
    $repo = Join-Path $fixture 'repo'
    [void][IO.Directory]::CreateDirectory($repo)
    Invoke-Git $repo @('init', '-q', '-b', 'main') | Out-Null
    Invoke-Git $repo @('config', 'core.autocrlf', 'false') | Out-Null
    Invoke-Git $repo @('config', 'user.name', 'fixture') | Out-Null
    Invoke-Git $repo @('config', 'user.email', 'fixture@example.invalid') | Out-Null
    Write-Text (Join-Path $repo '.gitignore') "TestResults/`nunity/UserSettings/`nunity/Library/`nunity/Temp/`nartifacts/`n"
    Write-Text (Join-Path $repo 'tools/path-budget.json') ([IO.File]::ReadAllText((Join-Path $repoUnderTest 'tools/path-budget.json')))
    Write-Text (Join-Path $repo 'unity/ProjectSettings/ProjectVersion.txt') "m_EditorVersion: 6000.6.9f1`n"
    Write-Text (Join-Path $repo 'data/value.txt') "one`n"
    Write-Text (Join-Path $repo 'data/other.txt') "a`n"
    if ($null -ne $Extra) { & $Extra $repo }
    Invoke-Git $repo @('add', '-A') | Out-Null
    Invoke-Git $repo @('commit', '-q', '-m', 'base') | Out-Null
    # patch は実の git diff から作り、repo に入れる（checkout の中の相対 path で指す）。
    Write-Text (Join-Path $repo 'data/value.txt') "two`n"
    $p1 = (Invoke-Git $repo @('diff')) -join "`n"
    Invoke-Git $repo @('checkout', '--', '.') | Out-Null
    Write-Text (Join-Path $repo 'data/other.txt') "b`n"
    $p2 = (Invoke-Git $repo @('diff')) -join "`n"
    Invoke-Git $repo @('checkout', '--', '.') | Out-Null
    Write-Text (Join-Path $repo 'patches/p1.patch') ($p1 + "`n")
    Write-Text (Join-Path $repo 'patches/p2.patch') ($p2 + "`n")
    Write-Text (Join-Path $repo 'patches/bad.patch') ($p1.Replace('-one', '-zzz') + "`n")
    Invoke-Git $repo @('add', '-A') | Out-Null
    Invoke-Git $repo @('commit', '-q', '-m', 'patches') | Out-Null
    $head = (Invoke-Git $repo @('rev-parse', 'HEAD')).Trim()
    Set-ScenarioSeam @{ RepoRoot=$repo; ExternalBase=(Join-Path $fixture 'drive') }
    return [pscustomobject]@{ Fixture=$fixture; Repo=$repo; Place=(Join-Path $fixture 'osm-v'); Head=$head; CommonDir=(Join-Path $repo '.git'); Drive=(Join-Path $fixture 'drive') }
}

function New-PatchFromEdit([string]$Repo, [string]$RelativePath, [string]$NewText) {
    # tracked の file を書き換えた差分を patch の文字列にして、書き換えを戻す。
    Write-Text (Join-Path $Repo $RelativePath) $NewText
    $patch = (Invoke-Git $Repo @('diff')) -join "`n"
    Invoke-Git $Repo @('checkout', '--', '.') | Out-Null
    return $patch + "`n"
}

function New-ManualWorktree([psobject]$Fx, [string]$Name, [switch]$Register, [string]$Branch = '') {
    $path = Join-Path $Fx.Fixture $Name
    if ($Branch) { Invoke-Git $Fx.Repo @('worktree', 'add', '-q', '-b', $Branch, $path) | Out-Null }
    else { Invoke-Git $Fx.Repo @('worktree', 'add', '-q', '--detach', $path) | Out-Null }
    if ($Register) {
        $r = Invoke-ScenarioCheckoutOperation -Operation register -Path $path -RepoRoot $Fx.Repo
        Assert $r.Ok "register failed: $($r.Problems -join '; ')"
    }
    return $path
}

function Read-Records([string]$RunRoot) {
    $path = Join-Path $RunRoot 'driver.jsonl'
    if (-not [IO.File]::Exists($path)) { return @() }
    return @([IO.File]::ReadAllLines($path) | Where-Object { $_ } | ForEach-Object { ConvertFrom-Json -InputObject $_ -AsHashtable })
}

function Invoke-Captured([scriptblock]$Action) {
    $original = [Console]::Error
    $capture = [IO.StringWriter]::new()
    try { [Console]::SetError($capture); $result = & $Action } finally { [Console]::SetError($original) }
    return [pscustomobject]@{ Result=$result; Err=$capture.ToString() }
}

function Get-RunRoot([psobject]$Fx, [string]$Name, [string]$RunId) { return Join-Path $Fx.Repo "TestResults/scenario/$Name/$RunId" }
function Read-Bytes([string]$Path) { return [IO.File]::ReadAllText($Path) }
function Get-WorktreeGitDir([string]$Root) { return ((Invoke-Git $Root @('rev-parse', '--path-format=absolute', '--absolute-git-dir')) | Select-Object -First 1).Trim() }

function Complete-ScenarioTestRun([string]$Suite) {
    Reset-ScenarioSeam
    # fixture の worktree を git に知らせずに消す（一時の repo ごと消えるので、共通の git dir も残らない）。
    $resolved = [IO.Path]::GetFullPath($script:tempRoot)
    $temp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if (-not $resolved.StartsWith($temp, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($resolved) -notlike 'osm-s4v-test-*') { throw 'unsafe cleanup path' }
    if ([IO.Directory]::Exists($resolved)) {
        # junction は中へ辿らずに外す。
        Get-ChildItem -LiteralPath $resolved -Recurse -Force -Attributes ReparsePoint -ErrorAction SilentlyContinue | ForEach-Object { [IO.Directory]::Delete($_.FullName) }
        Get-ChildItem -LiteralPath $resolved -Recurse -Force -File | ForEach-Object { $_.Attributes = 'Normal' }
        [IO.Directory]::Delete($resolved, $true)
    }
    [Console]::WriteLine("$Suite offline: $script:executed executed, $($script:failed.Count) failed")
    if ($ResultPath) { [IO.File]::WriteAllText([IO.Path]::GetFullPath($ResultPath), (ConvertTo-Json -InputObject ([ordered]@{registered=@($script:registered);selected=@($script:selected);executed=@($script:executedNames);failed=@($script:failed)}) -Depth 6)) }
}
