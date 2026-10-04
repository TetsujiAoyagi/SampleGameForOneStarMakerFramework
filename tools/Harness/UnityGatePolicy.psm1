# Unity pilot の固定集合と変更範囲。process と保存先には依存しない。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:LimitedPrefix = 'OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests.'
$script:LimitedNames = @(
    'PassedLeaf_RequiresBothCallbacksAndSeal',
    'MissingStarted_DoesNotBecomeComplete',
    'Reload_PreservesFirstSelection',
    'DuplicateAttempt_IsInvalid',
    'CallbackAfterSeal_ChangesSequenceAndRejectsCandidate',
    'ReloadAfterRunFinished_CannotSealComplete',
    'Seal_RequiresRunStartAndRunFinishCapture',
    'Seal_RequiresEveryInRunReloadResumeCapture',
    'ExecutedFailedLeaf_WithCompleteCallbacks_IsCompleteObservation',
    'NonexecutedFailedLeaf_IsIncompleteButContradictionIsInvalid',
    'UnknownOrUnrepresentedRootResult_IsRejectedByCompleteness'
)

function Get-UnityRequiredSteps([object]$Spec, [string[]]$ChangedPaths, [string]$Stage) {
    if ($Spec.testPolicy -cne 'unity-pilot-gates-v1' -or $Spec.recordPolicy -cne 'external-current-v1') { throw 'Unity pilot policyではありません。' }
    if (@($ChangedPaths).Count -eq 0) { throw 'base/headに変更がありません。' }
    foreach ($path in $ChangedPaths) {
        $path = $path.Replace('\','/')
        if ($path -ceq 'tools/harness.ps1' -or $path -ceq 'tools/run-tests.ps1' -or $path -ceq 'tools/contract-audit.ps1' -or $path -ceq 'tools/docs-audit.ps1' -or $path -ceq 'AGENTS.md' -or $path -ceq 'docs/README.md' -or
            $path -like 'tools/Harness/*' -or $path -like '.agents/skills/osm-workflow/*' -or $path -like '.agents/skills/osm-unity-editor/*' -or
            $path -ceq 'unity/Assets/OneStarMaker/Tests/Editor/TestObservation/ObservationStateTests.cs' -or $path -ceq 'unity/Assets/OneStarMaker/Tests/Editor/TestObservation/ObservationStateTests.cs.meta') { continue }
        throw "Unity pilotで未承認の変更pathです: $path"
    }
    $common = @('harness-local','unity-runner-local','unity-observation-local','unity-adapter-local','contract-audit','docs-audit')
    switch -CaseSensitive ($Stage) {
        'discovery' { return @($common + 'unity-editmode-limited' | Sort-Object) }
        'judgment' { return @($common + @('artifacts-local','unity-editmode-full') | Sort-Object) }
        default { throw 'stage は discovery または judgment です。' }
    }
}

function Get-UnityLimitedCases { return @($script:LimitedNames | ForEach-Object { $script:LimitedPrefix + $_ }) }

function Assert-UnityCaseSet([string]$Profile, [object[]]$Cases, [object]$Counts, [object]$Observation) {
    # XMLのleafを多重集合のまま扱う。fullにはparameterized fullnameの重複を許す。
    if ($null -eq $Counts -or $Counts.skipped -isnot [long] -and $Counts.skipped -isnot [int] -or $Counts.skipped -ne 0) { throw 'Skipped countを拒否します。' }
    if (@($Cases).Count -eq 0 -or $Counts.total -ne @($Cases).Count -or $Counts.executed -ne @($Cases).Count) { throw 'Unity leaf集合が空か件数不一致です。' }
    foreach ($case in $Cases) {
        if ($case.result -cne 'Passed' -or [string]::IsNullOrEmpty($case.fullname)) { throw '全leaf Passedが必要です。' }
    }
    if ($null -eq $Observation -or $null -eq $Observation.selected -or @($Observation.selected).Count -ne @($Cases).Count) { throw '初回選択leaf集合が一致しません。' }
    $selected = @($Observation.selected)
    foreach ($item in $selected) {
        if ($item.assemblyName -cne 'OneStarMaker.Tests.TestObservation.Editor' -and $Profile -ceq 'limited-v1') { throw '限定集合のassemblyが違います。' }
    }
    $expected = @(Get-UnityLimitedCases)
    $actual = @($selected | Where-Object { $_.assemblyName -ceq 'OneStarMaker.Tests.TestObservation.Editor' } | ForEach-Object { $_.fullname })
    if ($Profile -ceq 'limited-v1' -and $actual.Count -ne $expected.Count) { throw '限定集合の件数が11件ではありません。' }
    foreach ($name in $expected) {
        if (@($actual | Where-Object { $_ -ceq $name }).Count -ne 1) { throw "固定11件が不足または重複しています: $name" }
    }
    if ($Profile -ceq 'limited-v1' -and @($actual | Where-Object { $expected -cnotcontains $_ }).Count -gt 0) { throw '限定集合に余分なleafがあります。' }
}

Export-ModuleMember -Function Get-UnityRequiredSteps,Get-UnityLimitedCases,Assert-UnityCaseSet
