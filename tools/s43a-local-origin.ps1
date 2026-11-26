<#
.SYNOPSIS
    S4-3a（検証済み content の取得元・Local）の実 Editor の検証（E 系列）の case の列を実行する。
.DESCRIPTION
    Phase C の実行担当が呼ぶ driver。実機の検証の基盤（tools/Harness/Scenario.psm1）の上の scenario `s43a` で、
    checkout X（batch で publish する）と checkout Y（X を取得元にして取得し、Play する。別の Library）を使い、
    HANDOFF §5 の順で S0・P1・E1・P2・E2・E3・E4・E5・E6・Cleanup を走らせる。
    checkout の用意と事前確認、patch の適用と復元、process の起動と終了の確認、Editor の adapter、run の root と
    外部 root、証拠の置き場は基盤が持つ（契約は tools/Harness/README.md の「実機の検証の scenario」）。
    この file が持つのは S4-3a に固有の物だけ: case の列、設定の file、Local の取得元の fixture、eval の code、保存する証拠。

    この driver は合否を判定しない。照合は判定の担当が保存された原記録で HANDOFF §5 の表のとおりに行う。
    exit 0 は基盤の操作が全て通り、選んだ case が最後まで走ったことだけを示す。
.PARAMETER RunId
    run の id（英数字と間の . _ -、32 文字まで）。
.PARAMETER Only
    走らせる case（既定は全て）。続けてよい case は HANDOFF §5 の約束の 8。
.PARAMETER DryRun
    Unity を起動しない。checkout が既に用意されている時だけ使える。
.PARAMETER Commit
    対象の commit（implementation head）。省くと driver のある checkout の HEAD になるので、判定 C では省かない。
.PARAMETER XRoot
    checkout X（登録した検証用の checkout）。省けば基盤が用意する。
.PARAMETER YRoot
    checkout Y（X と別の検証用の checkout、別の Library）。省けば基盤が用意する。
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$RunId,
    [string[]]$Only = @(),
    [switch]$DryRun,
    [string]$Commit = '',
    [string]$XRoot = '',
    [string]$YRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Harness/Scenario.psm1')

$purpose = 'spring-full'
$target = 'StandaloneWindows64-Player'
$deliverySettings = 'unity/UserSettings/OSMContentDelivery.json'
$originSettings = 'unity/UserSettings/OSMContentOrigin.json'
$selectionRecord = 'unity/UserSettings/OSMContentBaseSelection.json'
$variantSettings = 'unity/UserSettings/OneStarMaker/DeveloperVariantSettings.asset'
$transportRelative = 'artifacts/bs2b/transport'
$headRelative = ".origin/$purpose/$target.json"
$launcher = 'SampleGame.DependOnAll.Editor.Build.ContentAcquireLauncher'
$recordKey = 'OSM.PlayEntry.Record.v1'
$saveAssetsResponseKey = 'OSM.PlayEntry.SaveAssetsResponse.v1'
$titleScene = 'Assets/SampleGame/OutGame/Title/Title.unity'

# ─── eval の code（製品の public static の入口、EditorApplication、SessionState、入口の設定の setter の reflection だけ） ───

function Get-EntrySettingsCode([string]$Route) {
    return @"
var t = System.Type.GetType("SampleGame.DependOnAll.Editor.Build.PlayEntrySettings, SampleGame.DependOnAll.Editor");
var r = System.Type.GetType("SampleGame.DependOnAll.Editor.Build.PlayEntryRoute, SampleGame.DependOnAll.Editor");
var f = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
t.GetProperty("RunPurpose", f).SetValue(null, "$purpose");
t.GetProperty("Route", f).SetValue(null, System.Enum.Parse(r, "$Route"));
return "set";
"@
}
$readEntrySettings = @'
var t = System.Type.GetType("SampleGame.DependOnAll.Editor.Build.PlayEntrySettings, SampleGame.DependOnAll.Editor");
var f = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
return t.GetProperty("RunPurpose", f).GetValue(null) + "|" + t.GetProperty("Route", f).GetValue(null);
'@
$readLastOperation = "return $launcher.IsRunning + `"|`" + $launcher.LastOperation;"
$readRecord = "return UnityEditor.SessionState.GetString(`"$recordKey`", `"`") + `"\n@@`" + UnityEditor.EditorApplication.timeSinceStartup.ToString(System.Globalization.CultureInfo.InvariantCulture);"
# Play の中の SceneDirector へ reflection で届き（18 章「宣言からの Play 経路」の観測の注意）、HomeScene を 1 回だけ要求する。
$addHomeScene = @'
var init = System.Type.GetType("SampleGame.DependOnAll.AppInitializer, SampleGame.DependOnAll").GetField("s_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
System.Reflection.FieldInfo field = null;
for (var type = init.GetType(); type != null && field == null; type = type.BaseType) field = type.GetField("_sceneDirector", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var director = field.GetValue(init);
var add = director.GetType().GetMethod("AddScene");
var parameters = add.GetParameters();
var args = new object[parameters.Length];
for (var i = 0; i < parameters.Length; i++) args[i] = i == 0 ? "HomeScene" : parameters[i].ParameterType == typeof(System.Threading.CancellationToken) ? (object)System.Threading.CancellationToken.None : parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
add.Invoke(director, args);
return "requested";
'@
$readHomeSceneLoad = @'
var init = System.Type.GetType("SampleGame.DependOnAll.AppInitializer, SampleGame.DependOnAll").GetField("s_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
if (init == null) return "";
System.Reflection.FieldInfo field = null;
for (var type = init.GetType(); type != null && field == null; type = type.BaseType) field = type.GetField("_sceneDirector", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
var director = field.GetValue(init);
if (director == null) return "";
var record = director.GetType().GetMethod("GetContentLoadRecord").Invoke(director, new object[] { "HomeScene" });
if (record == null) return "";
var candidate = record.GetType().GetProperty("Candidate").GetValue(record);
var identity = candidate.GetType().GetProperty("Identity").GetValue(candidate);
return identity.GetType().GetProperty("Revision").GetValue(identity) + "|" + identity.GetType().GetProperty("Digest").GetValue(identity);
'@
$readTitleToken = @'
foreach (var document in UnityEngine.Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>(UnityEngine.FindObjectsSortMode.None))
{
    var button = document.rootVisualElement == null ? null : UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(document.rootVisualElement, "pulse-button");
    if (button != null) return button.text;
}
return "";
'@

function Send-Marker([psobject]$Context, [psobject]$Session, [string]$Point) {
    [void](Send-ScenarioEditor $Context $Session -Code "UnityEngine.Debug.Log(`"[s43a] $($Context.Case) $Point`"); return `"marked`";")
}

# ─── 設定と証拠 ───

function Save-Evidence([psobject]$Context, [string]$Name, [AllowNull()] [object]$Content = $null, [string]$Path = '') {
    # -DryRun では基盤の関数が値を返さないので、証拠を残さない（空の値を証拠と取り違えない）。
    if ($Context.DryRun) { return $null }
    if ($Path) { return Save-ScenarioArtifact $Context -Name $Name -Path $Path }
    return Save-ScenarioArtifact $Context -Name $Name -Content $Content
}

function Set-DeliverySettings([psobject]$Context) {
    # Y の cache は run ごとの外部 root の下。budget は既定の 10 GiB（今ある他の欄は残す）。
    $path = Join-Path $Context.Roots['Y'] $deliverySettings
    $settings = if (Test-Path -LiteralPath $path) { Get-Content -Raw -LiteralPath $path | ConvertFrom-Json -AsHashtable } else { @{} }
    $settings.cacheRoot = Get-ScenarioDirectory $Context -Purpose 'cache' -PathOnly
    $settings.budget = 10737418240
    [void](Set-ScenarioFile $Context -Checkout 'Y' -Path $deliverySettings -Content ($settings | ConvertTo-Json -Depth 4))
}

function Set-OriginRoot([psobject]$Context, [string]$Root) {
    [void](Set-ScenarioFile $Context -Checkout 'Y' -Path $originSettings -Content (ConvertTo-Json -InputObject @{ originRoot=$Root }))
    Write-ScenarioRecord $Context @{ step='origin'; originRoot=$Root }
}

function Get-XOrigin([psobject]$Context) { return Join-Path $Context.Roots['X'] $transportRelative }

function Get-CacheListing([string]$CacheRoot) {
    $listing = [ordered]@{ installed=@(); staging=@() }
    $installed = Join-Path $CacheRoot 'installed'
    if (Test-Path -LiteralPath $installed) {
        $listing.installed = @(Get-ChildItem -LiteralPath $installed -Directory | ForEach-Object {
            $set = $_.Name
            Get-ChildItem -LiteralPath $_.FullName -Directory | ForEach-Object {
                $receipt = Join-Path $_.FullName 'receipt.json'
                [ordered]@{ name="$set/$($_.Name)"; receiptSha256=$(if (Test-Path -LiteralPath $receipt) { (Get-FileHash -Algorithm SHA256 -LiteralPath $receipt).Hash.ToLowerInvariant() } else { '' }) }
            }
        })
    }
    $staging = Join-Path $CacheRoot 'staging'
    if (Test-Path -LiteralPath $staging) { $listing.staging = @(Get-ChildItem -LiteralPath $staging -Directory | ForEach-Object { $_.Name }) }
    return $listing
}

function Save-YState([psobject]$Context, [string]$Point) {
    # 次の case が上書きする前に、cache の一覧と receipt、選んだ base の記録、設定の file を残す。
    $y = $Context.Roots['Y']
    $cacheRoot = Get-ScenarioDirectory $Context -Purpose 'cache' -PathOnly
    [void](Save-Evidence $Context -Name "$Point-cache.json" -Content (ConvertTo-Json -InputObject (Get-CacheListing $cacheRoot) -Depth 5))
    $selection = Join-Path $y $selectionRecord
    if (Test-Path -LiteralPath $selection) { [void](Save-Evidence $Context -Name "$Point-selection.json" -Path $selection) }
    $installed = Join-Path $cacheRoot "installed/$purpose"
    if (Test-Path -LiteralPath $installed) {
        # revision の名前（build の identity）は基盤の証拠の名前の上限に入るとは限らないので、写しは番号で呼ぶ。
        # 番号は時点ごとに振り直すので、どの revision の写しかは同じ時点の対応の file で読む。
        $copies = [Collections.Generic.List[object]]::new()
        foreach ($revision in Get-ChildItem -LiteralPath $installed -Directory | Sort-Object -Property Name) {
            $receipt = Join-Path $revision.FullName 'receipt.json'
            if (-not (Test-Path -LiteralPath $receipt)) { continue }
            $name = "$Point-receipt-$($copies.Count + 1).json"
            [void](Save-Evidence $Context -Name $name -Path $receipt)
            $copies.Add([ordered]@{ evidence=$name; installed="$purpose/$($revision.Name)" })
        }
        if ($copies.Count -gt 0) { [void](Save-Evidence $Context -Name "$Point-receipts.json" -Content (ConvertTo-Json -InputObject $copies.ToArray())) }
    }
}

function Save-CheckoutFacts([psobject]$Context, [string]$Checkout) {
    $root = $Context.Roots[$Checkout]
    [void](Save-Evidence $Context -Name "$Checkout-ProjectVersion.txt" -Path (Join-Path $root 'unity/ProjectSettings/ProjectVersion.txt'))
    foreach ($relative in $deliverySettings, $originSettings) {
        $path = Join-Path $root $relative
        if ($Checkout -ceq 'Y' -and (Test-Path -LiteralPath $path)) { [void](Save-Evidence $Context -Name ("Y-" + [IO.Path]::GetFileName($relative)) -Path $path) }
    }
    Write-ScenarioRecord $Context @{ step='checkout'; checkout=$Checkout; library=(Join-Path $root 'unity/Library') }
}

# ─── Editor の段 ───

function Set-EntrySettings([psobject]$Context, [psobject]$Session, [string]$Route) {
    [void](Send-ScenarioEditor $Context $Session -Code (Get-EntrySettingsCode $Route))
    $value = Read-ScenarioEditor $Context $Session -Code $readEntrySettings
    [void](Save-Evidence $Context -Name 'entry-settings.txt' -Content "$value")
    return $value
}

function Invoke-Acquire([psobject]$Context, [psobject]$Session, [string]$Name) {
    # 読む eval で前の番号を読み、送る eval で Start を 1 回だけ送る。応答が失われた時は送り直さず、前の番号の次を今回とする。
    $before = Read-ScenarioEditor $Context $Session -Code $readLastOperation
    Send-Marker $Context $Session 'acquire-start'
    $sent = Send-ScenarioEditor $Context $Session -Code "return $launcher.Start().ToString();"
    if ($Context.DryRun) { return @{ name=$Name; dryRun=$true } }
    $previous = [int]("$before".Split('|')[1])
    $operation = if ($sent.Answered -and "$($sent.Value)" -match '^\d+$') { [int]$sent.Value } else { $previous + 1 }
    $finished = $true
    try {
        [void](Wait-ScenarioEditor $Context $Session -Code $readLastOperation -TimeoutMs 1800000 -Until {
            param($value) $parts = "$value".Split('|'); $parts[0] -ceq 'False' -and [int]$parts[1] -eq $operation }.GetNewClosure())
    }
    catch { $finished = $false }
    $result = Read-ScenarioEditor $Context $Session -Code "return $launcher.LastResultJson;"
    [void](Save-Evidence $Context -Name "$Name-result.json" -Content "$result")
    $summary = @{ name=$Name; before=$previous; startAnswered=[bool]$sent.Answered; operation=$operation; finished=$finished }
    [void](Save-Evidence $Context -Name "$Name-operation.json" -Content (ConvertTo-Json -InputObject $summary))
    if (-not $finished) { Write-ScenarioRecord $Context @{ step='acquire'; name=$Name; outcome='not-finished-or-unlinked'; operation=$operation } }
    return $summary
}

function Wait-PlayRecord([psobject]$Context, [psobject]$Session, [int]$TimeoutMs) {
    # 移行の間は eval を送らない（QuietMs）。record の state が Blocked になるか、入った時刻から起動の観測の窓（15 秒）を過ぎるまで読む。
    $until = {
        param($value)
        $parts = "$value" -split "\n@@"
        if ($parts.Count -lt 2 -or $parts[0].Length -eq 0) { return $false }
        $document = $parts[0] | ConvertFrom-Json
        $entered = [double]$document.record.enteredPlayTime
        return $document.record.state -ceq 'Blocked' -or ($entered -gt 0 -and [double]::Parse($parts[1], [Globalization.CultureInfo]::InvariantCulture) - $entered -gt 15)
    }
    try { return @{ value=(Wait-ScenarioEditor $Context $Session -Code $readRecord -Until $until -QuietMs 45000 -TimeoutMs $TimeoutMs); expired=$false } }
    catch { return @{ value=(Read-ScenarioEditor $Context $Session -Code $readRecord); expired=$true; reason="$_" } }
}

function Invoke-Play([psobject]$Context, [psobject]$Session, [int]$TimeoutMs, [switch]$ReadTitle) {
    [void](Send-ScenarioEditor $Context $Session -Code "UnityEditor.SessionState.EraseString(`"$recordKey`"); return `"erased`";")
    Send-Marker $Context $Session 'play-start'
    [void](Send-ScenarioEditor $Context $Session -Code 'UnityEditor.EditorApplication.EnterPlaymode(); return "entering";')
    if ($Context.DryRun) { return @{ dryRun=$true } }
    $play = Wait-PlayRecord $Context $Session $TimeoutMs
    [void](Save-Evidence $Context -Name 'play-record.txt' -Content "$($play.value)")
    $summary = [ordered]@{ playExpired=$play.expired; title=''; load=''; loadObserved=$false }
    $playing = Read-ScenarioEditor $Context $Session -Code 'return UnityEditor.EditorApplication.isPlaying.ToString();'
    if ($playing -ceq 'True') {
        if ($ReadTitle) { $summary.title = Read-ScenarioEditor $Context $Session -Code $readTitleToken }
        [void](Send-ScenarioEditor $Context $Session -Code $addHomeScene)
        try {
            $summary.load = Wait-ScenarioEditor $Context $Session -Code $readHomeSceneLoad -Until { param($value) "$value".Length -gt 0 } -TimeoutMs 120000
            $summary.loadObserved = $true
        }
        catch { $summary.loadReason = "HomeScene load record was not observed within 2 minutes: $_" }
    }
    [void](Save-Evidence $Context -Name 'play-observations.json' -Content (ConvertTo-Json -InputObject $summary))
    Send-Marker $Context $Session 'play-stop'
    [void](Send-ScenarioEditor $Context $Session -Code 'UnityEditor.EditorApplication.ExitPlaymode(); return "exiting";')
    try { [void](Wait-ScenarioEditor $Context $Session -Code 'return UnityEditor.EditorApplication.isPlaying.ToString();' -Until { param($value) $value -ceq 'False' } -QuietMs 45000 -TimeoutMs 600000) }
    catch { Write-ScenarioRecord $Context @{ step='play-exit'; outcome='not-confirmed'; reason="$_" } }
    return $summary
}

function Open-EmptyScene([psobject]$Context, [psobject]$Session) {
    # 開いている Scene が入口の BuiltWithReusableScenes で拒まれないよう、各 Play の前に空の Scene を開く。
    [void](Send-ScenarioEditor $Context $Session -Code 'UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single); return "empty";')
}

function Invoke-EditorCase([psobject]$Context, [scriptblock]$Steps, [string]$Route = 'Built') {
    $session = Start-ScenarioEditor $Context -KillOnTimeout
    $summary = [ordered]@{ case=$Context.Case; route=$Route }
    try {
        $summary.entrySettings = Set-EntrySettings $Context $session $Route
        & $Steps $session $summary
    }
    finally {
        Save-CheckoutFacts $Context 'Y'
        $exit = Stop-ScenarioEditor $Context $session
        $summary.editorExitCode = $exit.ExitCode
        $summary.editorForced = $exit.Forced
    }
    Write-ScenarioRecord $Context @{ step='case'; summary=$summary }
    return $summary
}

# ─── publish（X） ───

function Invoke-Publish([psobject]$Context) {
    $x = $Context.Roots['X']
    $batch = Invoke-ScenarioBatch $Context -Method 'SampleGame.DependOnAll.Editor.Build.SampleGameContentBatch.BuildAndPublish' -Arguments '-osmPurpose', $purpose -TimeoutMs 5400000 -KillOnTimeout
    $summary = [ordered]@{ case=$Context.Case; status=$batch.Status; exitCode=$batch.ExitCode; forced=$batch.Forced; logPath=$batch.LogPath }
    if ($Context.DryRun) { return $summary }
    $resultPath = Join-Path $x 'artifacts/bs2b/batch/last-result.json'
    $headPath = Join-Path (Get-XOrigin $Context) $headRelative
    foreach ($item in @(@{ name='last-result.json'; path=$resultPath }, @{ name='head.json'; path=$headPath })) {
        if (Test-Path -LiteralPath $item.path) {
            [void](Save-Evidence $Context -Name $item.name -Path $item.path)
            $summary["$($item.name)-utc"] = (Get-Item -LiteralPath $item.path).LastWriteTimeUtc.ToString('o')
        }
    }
    if (Test-Path -LiteralPath $resultPath) {
        $result = Get-Content -Raw -LiteralPath $resultPath | ConvertFrom-Json
        $manifest = Join-Path "$($result.publishPath)" 'transport.json'
        if ("$($result.publishPath)".Length -gt 0 -and (Test-Path -LiteralPath $manifest)) {
            $summary.transportSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $manifest).Hash.ToLowerInvariant()
        }
    }
    [void](Save-Evidence $Context -Name 'publish.json' -Content (ConvertTo-Json -InputObject $summary))
    return $summary
}

function New-OriginFixtures([psobject]$Context) {
    # E3: P1 の head の bytes だけを置いた root（revision の directory は無い）。E5 (b): 同じ head の digest だけを別の値にした root。
    $head = Join-Path (Get-XOrigin $Context) $headRelative
    if (-not (Test-Path -LiteralPath $head)) { throw "P1 の head がありません: $head" }
    $bytes = [IO.File]::ReadAllBytes($head)
    $e3 = Join-Path (Get-ScenarioDirectory $Context -Purpose 'origin-e3') $headRelative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($e3))
    [IO.File]::WriteAllBytes($e3, $bytes)
    $json = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json
    $other = if ($json.manifestSha256.StartsWith('f')) { 'e' * 64 } else { 'f' * 64 }
    $json.manifestSha256 = $other
    $e5b = Join-Path (Get-ScenarioDirectory $Context -Purpose 'origin-e5b') $headRelative
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($e5b))
    [IO.File]::WriteAllText($e5b, ($json | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
    $hashes = [ordered]@{ p1Head=(Get-FileHash -Algorithm SHA256 -LiteralPath $head).Hash.ToLowerInvariant()
        e3Head=(Get-FileHash -Algorithm SHA256 -LiteralPath $e3).Hash.ToLowerInvariant(); e5bHead=(Get-FileHash -Algorithm SHA256 -LiteralPath $e5b).Hash.ToLowerInvariant(); e5bDigest=$other }
    [void](Save-Evidence $Context -Name 'fixtures.json' -Content (ConvertTo-Json -InputObject $hashes))
    return $hashes
}

# ─── case の列 ───

$scenario = New-Scenario -Name 's43a' -Checkout 'X', 'Y'

Add-ScenarioCase $scenario -Name 'S0' -Checkout 'Y' -Body {
    param($context)
    $y = $context.Roots['Y']
    # Variant profile の選択があると入口が Play に入らない。何も書かずに止まる（実行の担当が退避して新しい RunId で走らせ直す）。
    if (Test-Path -LiteralPath (Join-Path $y $variantSettings)) { throw "Y に $variantSettings があります。checkout の外へ退避してから新しい RunId で走らせます" }
    $selection = Join-Path $y $selectionRecord
    if (Test-Path -LiteralPath $selection) { [void](Save-Evidence $context -Name 'S0-selection-before.json' -Path $selection) }
    Set-DeliverySettings $context
    Set-OriginRoot $context (Get-XOrigin $context)
    return Invoke-EditorCase $context {
        param($session, $summary)
        $summary.identity = Read-ScenarioEditor $context $session -Code 'return UnityEngine.Application.unityVersion + "|" + System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);'
    }
}

Add-ScenarioCase $scenario -Name 'P1' -Checkout 'X' -Body {
    param($context)
    $summary = Invoke-Publish $context
    if (-not $context.DryRun) { $summary.fixtures = New-OriginFixtures $context }
    return $summary
}

Add-ScenarioCase $scenario -Name 'E1' -Checkout 'Y' -Body {
    param($context)
    return Invoke-EditorCase $context {
        param($session, $summary)
        Save-YState $context 'before'
        $summary.acquire = Invoke-Acquire $context $session 'acquire'
        Save-YState $context 'after-acquire'
        Open-EmptyScene $context $session
        $summary.play = Invoke-Play $context $session 600000
    }
}

Add-ScenarioCase $scenario -Name 'P2' -Checkout 'X' -Body { param($context) return Invoke-Publish $context }

Add-ScenarioCase $scenario -Name 'E2' -Checkout 'Y' -Body {
    param($context)
    return Invoke-EditorCase $context {
        param($session, $summary)
        Save-YState $context 'before'
        $summary.acquire = Invoke-Acquire $context $session 'acquire'
        Save-YState $context 'after-acquire'
        Open-EmptyScene $context $session
        $summary.play = Invoke-Play $context $session 600000
    }
}

Add-ScenarioCase $scenario -Name 'E3' -Checkout 'Y' -Body {
    param($context)
    # P1 の head の bytes だけを置いた root を取得元にする（bytes の取得口を開けば失敗する）。Play しない。
    Set-OriginRoot $context (Get-ScenarioDirectory $context -Purpose 'origin-e3' -PathOnly)
    return Invoke-EditorCase $context {
        param($session, $summary)
        Save-YState $context 'before'
        $summary.acquire = Invoke-Acquire $context $session 'acquire'
        Save-YState $context 'after-acquire'
    }
}

Add-ScenarioCase $scenario -Name 'E4' -Checkout 'Y' -Body {
    param($context)
    # 取得元の場所を X に戻し、新しい Editor の process で、取得せずに Play する。
    Set-OriginRoot $context (Get-XOrigin $context)
    return Invoke-EditorCase $context {
        param($session, $summary)
        Save-YState $context 'before-play'
        Open-EmptyScene $context $session
        $summary.play = Invoke-Play $context $session 600000
    }
}

Add-ScenarioCase $scenario -Name 'E5' -Checkout 'Y' -Body {
    param($context)
    return Invoke-EditorCase $context {
        param($session, $summary)
        Save-YState $context 'before'
        Set-OriginRoot $context (Get-ScenarioDirectory $context -Purpose 'origin-e5a' -PathOnly)
        $summary.acquireA = Invoke-Acquire $context $session 'acquire-a'
        Save-YState $context 'after-a'
        Set-OriginRoot $context (Get-ScenarioDirectory $context -Purpose 'origin-e5b' -PathOnly)
        $summary.acquireB = Invoke-Acquire $context $session 'acquire-b'
        Save-YState $context 'after-b'
        Set-OriginRoot $context (Get-XOrigin $context)
        Open-EmptyScene $context $session
        $summary.play = Invoke-Play $context $session 600000
    }
}

Add-ScenarioCase $scenario -Name 'E6' -Checkout 'Y' -Patches 'unity/Assets/SampleGame/Tests/Editor/Build/PlayerOverlayEdits/ac01-title.patch' -Body {
    param($context)
    # 経路 Built + local edits。patch を当てた Title.unity を開き（入口が複製して載せる）、未保存の asset の確認に cancel を先置きする。
    # body は Editor を止めてから戻る（基盤が patch を戻す時に、開いた Scene の modal を出さない）。
    return Invoke-EditorCase $context -Route 'BuiltWithLocalEdits' -Steps {
        param($session, $summary)
        $summary.patch = 'ac01-title.patch'
        [void](Send-ScenarioEditor $context $session -Code "UnityEditor.SceneManagement.EditorSceneManager.OpenScene(`"$titleScene`", UnityEditor.SceneManagement.OpenSceneMode.Single); return `"opened`";")
        [void](Send-ScenarioEditor $context $session -Code "UnityEditor.SessionState.SetString(`"$saveAssetsResponseKey`", `"cancel`"); return `"set`";")
        $summary.play = Invoke-Play $context $session 1800000 -ReadTitle
    }
}

Add-ScenarioCase $scenario -Name 'Cleanup' -Body {
    param($context)
    # Y の管理する file を書く前へ戻し、run の外部 root（cache と fixture）を消す。X の publish と head、Y の入口の設定と
    # 選んだ base の記録の entry は検証用の checkout の git 管理外の file として残る（HANDOFF §5 の約束の 9）。
    $cacheRoot = Get-ScenarioDirectory $context -Purpose 'cache' -PathOnly
    [void](Reset-ScenarioFiles $context -Checkout 'Y')
    [void](Remove-ScenarioDirectories $context)
    Write-ScenarioRecord $context @{ step='cleanup'; cacheRoot=$cacheRoot; dryRun=[bool]$context.DryRun }
    return @{ cacheRoot=$cacheRoot }
}

$checkoutRoots = @{}
if ($XRoot) { $checkoutRoots.X = $XRoot }
if ($YRoot) { $checkoutRoots.Y = $YRoot }
$result = Invoke-Scenario $scenario -RunId $RunId -Only $Only -DryRun:$DryRun -CheckoutRoots $checkoutRoots -Commit $Commit
if ($result.RunRoot) { Write-Host "[s43a] done: $(Join-Path $result.RunRoot 'driver.jsonl')" }
exit $result.ExitCode
