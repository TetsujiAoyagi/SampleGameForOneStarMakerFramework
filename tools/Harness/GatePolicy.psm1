Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-ApprovedSpec([string]$Task, [object]$Spec) {
    # H1はこの1件のA3承認に固定する。新taskの採用は別A3とこのアンカー更新を要する。
    $approvedHash = '8070e3e0a299f84500c00b574fd406e145ea50fb7ec5a7d92fc00f28e7a049a1'
    $approvedBase = '292129b563087c4dbff43dcd2d6ef71dd5db5b51'
    if ($Task -cne 'h1-transport-identity' -or $Spec.base -cne $approvedBase -or $Spec.testPolicy -cne 'local-gates-v1' -or $Spec.recordPolicy -cne 'external-current-v1' -or -not $Spec.approved) { throw 'このtaskに承認されたH1 A3仕様がありません。' }
    $actualHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Spec.text))).ToLowerInvariant()
    if ($actualHash -cne $approvedHash) { throw 'A3 snapshotのhashが承認値と一致しません。' }
}

function Get-RequiredSteps([object]$Spec, [string[]]$ChangedPaths, [string]$Stage) {
    if ($Spec.testPolicy -cne 'local-gates-v1' -or $Spec.recordPolicy -cne 'external-current-v1') { throw 'H1適用済みの仕様ではありません。' }
    $steps = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($path in $ChangedPaths) {
        $path = $path.Replace('\', '/')
        if ($path -like 'tools/Artifacts/*') {
            [void]$steps.Add('artifacts-local')
        } elseif ($path -like 'tools/Harness/*' -or $path -ceq 'tools/harness.ps1') {
            [void]$steps.Add('harness-local')
        } elseif ($path -ceq 'tools/contract-audit.ps1' -or $path -ceq 'tools/docs-audit.ps1' -or $path -ceq '.gitignore' -or $path -ceq 'AGENTS.md' -or $path -like '.agents/skills/osm-workflow/*' -or $path -like '.agents/skills/osm-unity-editor/*' -or $path -ceq 'docs/README.md') {
            [void]$steps.Add('harness-local')
        } else { throw "H1で未対応の変更pathです: $path。Unityを含む場合はH2接続後に判定してください。" }
    }
    if ($ChangedPaths.Count -eq 0) { throw 'base/headに変更がありません。候補commitを確認してください。' }
    [void]$steps.Add('contract-audit')
    if (@($ChangedPaths | Where-Object { $_ -like 'docs/*' -or $_ -like '*.md' -or $_ -like '.agents/skills/*' }).Count -gt 0) { [void]$steps.Add('docs-audit') }
    if ($Stage -ceq 'judgment') {
        foreach ($name in @('artifacts-local', 'harness-local', 'contract-audit', 'docs-audit')) { [void]$steps.Add($name) }
    } elseif ($Stage -cne 'discovery') { throw 'stage は discovery または judgment です。' }
    return @($steps | Sort-Object)
}

function Assert-RunForGate([object]$Run, [object]$Spec, [string]$Head, [string[]]$RequiredSteps) {
    if ($Run.specHash -cne $Spec.specHash -or $Run.base -cne $Spec.base -or $Run.head -cne $Head) { throw 'runの仕様またはbase/headが候補と一致しません。' }
    if ($null -eq $Run.dirtyBefore -or $null -eq $Run.dirtyAfter -or $Run.dirtyBefore -isnot [array] -or $Run.dirtyAfter -isnot [array]) { throw 'run前後のdirty状態が確定していません。' }
    if ($Run.dirtyBefore -or $Run.dirtyAfter) { throw 'dirtyなrunは完了引渡しに使えません。commitするかtrialとして実行してください。' }
    if ($Run.status -cne 'passed') { throw '失敗または未完了のrunは採用できてもgateは通過できません。' }
    foreach ($name in $RequiredSteps) {
        $found = @($Run.steps | Where-Object { $_.name -ceq $name -and $_.status -ceq 'passed' })
        if ($found.Count -ne 1) { throw "必須stepが成功していません: $name" }
        if ($name -like '*local') {
            Assert-CaseSets @($found[0].registered) @($found[0].selected) @($found[0].executed) $name
        }
    }
}

function Assert-CaseSets([string[]]$Registered, [string[]]$Selected, [string[]]$Executed, [string]$Name) {
    if ($Registered.Count -eq 0 -or $Selected.Count -eq 0 -or $Executed.Count -eq 0) { throw "case集合が空です: $Name" }
    foreach ($cases in @($Registered, $Selected, $Executed)) {
        if (@($cases | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0 -or
            @($cases | Sort-Object -Unique -CaseSensitive).Count -ne $cases.Count) { throw "case集合に空値または重複があります: $Name" }
    }
    $expected = @($Registered | Sort-Object -CaseSensitive)
    foreach ($actual in @($Selected, $Executed)) {
        $ordered = @($actual | Sort-Object -CaseSensitive)
        if ($ordered.Count -ne $expected.Count) { throw "case集合が不一致です: $Name" }
        for ($i = 0; $i -lt $expected.Count; $i++) {
            if ($expected[$i] -cne $ordered[$i]) { throw "case集合が不一致です: $Name" }
        }
    }
}

function Resolve-CloseReferences([object[]]$References, [string]$Task, [bool]$ReviewClosed, [DateTimeOffset]$ClosedAt) {
    $resolved = [Collections.Generic.List[object]]::new()
    foreach ($item in $References) {
        $ref = $item | ConvertTo-Json -Depth 10 | ConvertFrom-Json -DateKind String
        if (-not $ref.releasedAt -and $ref.purpose -ceq 'review' -and -not $ReviewClosed) { throw '進行中reviewがあります。終了済みなら-ReviewClosedで明示してください。' }
        if (-not $ref.releasedAt -and (($ref.purpose -ceq 'adopted-run' -and $ref.consumerId -ceq $Task) -or ($ref.purpose -ceq 'review' -and $ReviewClosed))) { $ref.releasedAt = $ClosedAt.ToString('o') }
        if (-not $ref.releasedAt -and (!$ref.expiresAt -or [DateTimeOffset]::Parse($ref.expiresAt) -le $ClosedAt)) { throw "期限切れ参照があります: $($ref.referenceId)。所有者が解放または有限延長してください。" }
        $resolved.Add($ref)
    }
    return @($resolved)
}

function Select-BlindInput([object]$Spec, [object]$Run, [string]$Kind, [string[]]$ChangedPaths, [string]$Diff = '') {
    if ($Kind -cnotin @('discovery', 'judgment')) { throw '入力種別が不正です。' }
    # 可変CURRENTとC所見を列挙せず、固定項目だけを新しいrecordに写す。
    return [ordered]@{
        inputKind = $Kind
        specId = $Spec.id
        specHash = $Spec.specHash
        specification = $Spec.text
        base = $Spec.base
        head = $Run.head
        changedPaths = @($ChangedPaths)
        diff = $Diff
        runId = $Run.id
        runHash = $Run.recordHash
        steps = @($Run.steps)
        implementationResult = $Run.implementationResult
    }
}

Export-ModuleMember -Function Assert-ApprovedSpec,Get-RequiredSteps,Assert-RunForGate,Assert-CaseSets,Resolve-CloseReferences,Select-BlindInput
