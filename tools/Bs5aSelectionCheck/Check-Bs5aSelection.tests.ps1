#Requires -Version 7.0
# offline の自己検査。合成した preflight.json と期待値で Check-Bs5aSelection.ps1 の合否が意図どおりかを確かめる。
# 実行: pwsh tools/Bs5aSelectionCheck/Check-Bs5aSelection.tests.ps1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$check = Join-Path $PSScriptRoot 'Check-Bs5aSelection.ps1'
$save = Join-Path $PSScriptRoot 'Save-Bs5aBaseline.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("bs5a-check-" + [Guid]::NewGuid().ToString('N'))
$expectedDir = Join-Path $testRoot 'expected'
New-Item -ItemType Directory -Force -Path $expectedDir | Out-Null

function Assert-Equal($Expected, $Actual, [string] $Message) {
    if ($Expected -ne $Actual) { throw "$Message Expected='$Expected' Actual='$Actual'" }
}
function Stable([string] $Logical, [string] $Rep, [string] $Guid) {
    "osm-build-candidate-v1|" + "$($Logical.Length):$Logical" + "$($Rep.Length):$Rep" + "$($Guid.Length):$Guid"
}
function Entry([string] $Logical, [string] $Rep, [string] $Guid) { (Stable $Logical $Rep $Guid) + "|$Logical|$Guid" }
function Write-Preflight([string] $Name, [hashtable] $Body) {
    $path = Join-Path $testRoot "$Name.json"
    [System.IO.File]::WriteAllText($path, (($Body | ConvertTo-Json -Depth 5) + "`n"), [System.Text.UTF8Encoding]::new($false))
    $path
}
function Invoke-Check([string[]] $Files, [string] $Mode) {
    $out = & pwsh -NoLogo -NoProfile -File $check -Preflight $Files -Mode $Mode -ExpectedDir $expectedDir 2>&1
    [pscustomobject]@{ Exit = $LASTEXITCODE; Output = ($out -join "`n") }
}

try {
    $g1 = '0123456789abcdef0123456789abcdef'; $g2 = 'fedcba9876543210fedcba9876543210'; $g3 = 'a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1a1'
    $boot = 'samplegame:bootstrap:uicommon/Full|samplegame:bootstrap:uicommon|bd51734fab3deb24b9fbd444046213ac'
    $sel = @((Entry 'OutGameScene' 'Full' $g1), (Entry 'Spring_Cell_0_0' 'Full' $g2), $boot)

    # 1. Save-Bs5aBaseline が sorted stable key と provenance を書く。
    $base = Write-Preflight 'spring-full' @{ identity = 'x'; contentSet = 'spring-full'; selected = $sel; issues = @() }
    & pwsh -NoLogo -NoProfile -File $save -Preflight $base -SourceCommit 'deadbeef' -ExpectedDir $expectedDir | Out-Null
    Assert-Equal 0 $LASTEXITCODE 'save の終了コード。'
    $keys = Get-Content (Join-Path $expectedDir 'spring-full.baseline.keys')
    Assert-Equal 3 $keys.Count 'baseline の件数。'
    $meta = Get-Content (Join-Path $expectedDir 'spring-full.baseline.json') -Raw | ConvertFrom-Json
    Assert-Equal 'deadbeef' $meta.sourceCommit 'provenance の commit。'

    # 2. Baseline mode: 同じ preflight は PASS、1 件欠けると FAIL。
    $r = Invoke-Check @($base) 'Baseline'
    Assert-Equal 0 $r.Exit "Baseline PASS。`n$($r.Output)"
    $less = Write-Preflight 'spring-full-less' @{ identity = 'y'; contentSet = 'spring-full'; selected = $sel[0..1]; issues = @() }
    $r = Invoke-Check @($less) 'Baseline'
    Assert-Equal 1 $r.Exit "欠けは FAIL。`n$($r.Output)"
    if ($r.Output -notmatch '件数が違う') { throw "件数の失敗理由が出ていない。`n$($r.Output)" }

    # 3. Judgment mode: baseline + 付随 asset、requirements[] が要る。
    $att = Stable 'InGameSession:Lit' 'Full' $g3
    [System.IO.File]::WriteAllText((Join-Path $expectedDir 'attached-assets.keys'), "$att`n", [System.Text.UTF8Encoding]::new($false))
    $reqs = @(
        @{ logicalKey = 'InGameSession:Lit'; representation = 'Full'; path = @('InGameScene', 'InGameSession') },
        @{ logicalKey = 'OutGameScene'; representation = 'Full'; path = @('OutGameScene') },
        @{ logicalKey = 'Spring_Cell_0_0'; representation = 'Full'; path = @('InGameScene', 'InGameSession', 'Season_Spring', 'Spring_Cell_0_0') }
    )
    $judg = Write-Preflight 'spring-full-judgment' @{ identity = 'z'; contentSet = 'spring-full'; selected = ($sel + (Entry 'InGameSession:Lit' 'Full' $g3)); issues = @(); requirements = $reqs }
    $r = Invoke-Check @($judg) 'Judgment'
    Assert-Equal 0 $r.Exit "Judgment PASS。`n$($r.Output)"
    $noReq = Write-Preflight 'spring-full-noreq' @{ identity = 'z2'; contentSet = 'spring-full'; selected = ($sel + (Entry 'InGameSession:Lit' 'Full' $g3)); issues = @() }
    $r = Invoke-Check @($noReq) 'Judgment'
    Assert-Equal 1 $r.Exit "requirements 無しは FAIL。`n$($r.Output)"
    $badRoot = Write-Preflight 'spring-full-badroot' @{ identity = 'z3'; contentSet = 'spring-full'; selected = ($sel + (Entry 'InGameSession:Lit' 'Full' $g3)); issues = @(); requirements = @(@{ logicalKey = 'A'; representation = 'Full'; path = @('Season_Spring') }) }
    $r = Invoke-Check @($badRoot) 'Judgment'
    if ($r.Exit -ne 1 -or $r.Output -notmatch 'root から始まらない') { throw "root 違反が FAIL にならない。`n$($r.Output)" }

    # 4. planner: 禁止 pattern と (iii) の期待値。
    $plannerKeys = @((Stable 'OutGameScene' 'Full' $g1), (Stable 'Spring_Cell_0_0' 'Whitebox' $g2), 'samplegame:bootstrap:uicommon/Full')
    [System.IO.File]::WriteAllText((Join-Path $expectedDir 'spring-planner-whitebox.keys'), (($plannerKeys | Sort-Object -Culture '' -CaseSensitive) -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
    $preq = @(@{ logicalKey = 'OutGameScene'; representation = 'Full'; path = @('OutGameScene') }, @{ logicalKey = 'Spring_Cell_0_0'; representation = 'Whitebox'; path = @('InGameScene', 'Spring_Cell_0_0') })
    $pl = Write-Preflight 'planner' @{ identity = 'p'; contentSet = 'spring-planner-whitebox'; selected = @((Entry 'OutGameScene' 'Full' $g1), (Entry 'Spring_Cell_0_0' 'Whitebox' $g2), $boot); issues = @(); requirements = $preq }
    $r = Invoke-Check @($pl) 'Judgment'
    Assert-Equal 0 $r.Exit "planner PASS。`n$($r.Output)"
    $plEnv = Write-Preflight 'planner-env' @{ identity = 'p2'; contentSet = 'spring-planner-whitebox'; selected = @((Entry 'OutGameScene' 'Full' $g1), (Entry 'Spring_Cell_0_0' 'Whitebox' $g2), $boot, (Entry 'Spring_Environment_0_0' 'Full' $g3)); issues = @(); requirements = $preq }
    $r = Invoke-Check @($plEnv) 'Judgment'
    if ($r.Exit -ne 1 -or $r.Output -notmatch '禁止 pattern') { throw "Environment の混入が FAIL にならない。`n$($r.Output)" }

    # 5. bs4: probe 1 件を除いて spring-full と比べる。
    $bs4 = Write-Preflight 'bs4' @{ identity = 'b'; contentSet = 'bs4-spring-full'; selected = ($sel + (Entry 'InGameSession:Lit' 'Full' $g3) + 'bs4:fixture:prefab/Full|bs4:fixture:prefab|11111111111111111111111111111111'); issues = @(); requirements = $reqs }
    $r = Invoke-Check @($bs4) 'Judgment'
    Assert-Equal 0 $r.Exit "bs4 PASS。`n$($r.Output)"
    $bs4NoProbe = Write-Preflight 'bs4-noprobe' @{ identity = 'b2'; contentSet = 'bs4-spring-full'; selected = ($sel + (Entry 'InGameSession:Lit' 'Full' $g3)); issues = @(); requirements = $reqs }
    $r = Invoke-Check @($bs4NoProbe) 'Judgment'
    if ($r.Exit -ne 1 -or $r.Output -notmatch 'probe') { throw "probe 欠落が FAIL にならない。`n$($r.Output)" }

    Write-Output 'Check-Bs5aSelection.tests: all passed'
}
finally {
    Remove-Item -LiteralPath $testRoot -Recurse -Force -ErrorAction SilentlyContinue
}
