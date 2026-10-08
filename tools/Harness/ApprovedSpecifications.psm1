# Git 外の登録情報は承認の根拠にしない。task ごとの固定値をここで所有する。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ApprovedSpecification([string]$Task) {
    switch -CaseSensitive ($Task) {
        'h1-transport-identity' {
            return [ordered]@{
                task = $Task; textSha256 = '8070e3e0a299f84500c00b574fd406e145ea50fb7ec5a7d92fc00f28e7a049a1'
                approved = $true; base = '292129b563087c4dbff43dcd2d6ef71dd5db5b51'; testPolicy = 'local-gates-v1'; recordPolicy = 'external-current-v1'
                title = 'H1-PILOT-TRANSPORT-IDENTITY'; question = '過去RESULTを読まずに現行仕様と採用runから着手・判定できるか'
                summary = 'Git外CURRENT、機械run、Artifacts/Harness限定のB exitとC entry'; outOfScope = 'Unity・R2 live・Route Proof本体・既存履歴の削除'
                minimum = 'A3 snapshotのM1〜M6を満たす'; trialDays = 7; adoptedDaysAfterClose = 30
                profile = $null; scope = $null
            }
        }
        'h2c-unity-gate' {
            return [ordered]@{
                task = $Task; textSha256 = '19d008f64ecae1e17ed5c3e071ca681480cde24ce49089f6a6b198892b743946'
                approved = $true; base = '2c29c99806788406551affba6cc795e67e614748'; testPolicy = 'unity-pilot-gates-v1'; recordPolicy = 'external-current-v1'
                title = 'H2C-UNITY-GATE'; question = '標準runnerのB限定Unity結果とC全EditModeを同じGit外recordへ接続し、欠測をreadyにせずblind監査へ渡せるか'
                summary = '承認済み一taskのUnity adapter、固定集合、raw再検査、B限定許可'; outOfScope = 'H2d live probe・H3配布/削除・R2/Cloud・新Build/PlayMode/Player'
                minimum = '凍結仕様h2c-a3-v1のM1〜M5を満たす'; trialDays = 7; adoptedDaysAfterClose = 30
                profile = [ordered]@{ platform = 'EditMode'; observeUnity = $true; withGraphics = $false; project = 'unity'; requestedVersion = '6000.6.0f1'; environment = [ordered]@{ SAMPLEGAME_CONTENT__RUNTIMEMODE = 'addressables' }; limitedFilter = 'OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests' }
                scope = @('tools/Harness/**','tools/harness.ps1','tools/run-tests.ps1','tools/contract-audit.ps1','tools/docs-audit.ps1','AGENTS.md','.agents/skills/osm-workflow/**','.agents/skills/osm-unity-editor/**','docs/README.md','unity/Assets/OneStarMaker/Tests/Editor/TestObservation/ObservationStateTests.cs','unity/Assets/OneStarMaker/Tests/Editor/TestObservation/ObservationStateTests.cs.meta')
            }
        }
        'h2c-unity-gate-r2' {
            return [ordered]@{
                task = $Task; textSha256 = '6fa8801ca35cd99a16679313e8737e64f03a2b7e9f35b784f90dc2a3f0f6914f'
                approved = $true; base = '9828feb56e91e8907f350392bc0cde95b7921779'; testPolicy = 'unity-pilot-gates-v1'; recordPolicy = 'external-current-v1'
                title = 'H2C-UNITY-GATE-R2'; question = '標準runnerのB限定Unity結果とC全EditModeを同じGit外recordへ接続し、欠測をreadyにせずblind監査へ渡せるか'
                summary = '承認済み一taskのUnity adapter、固定集合、raw再検査、B限定許可'; outOfScope = 'H2d live probe・H3配布/削除・R2/Cloud・新Build/PlayMode/Player'
                minimum = '凍結仕様h2c-a3-v2のM1〜M5を満たす'; trialDays = 7; adoptedDaysAfterClose = 30
                profile = [ordered]@{ platform = 'EditMode'; observeUnity = $true; withGraphics = $false; project = 'unity'; requestedVersion = '6000.6.0f1'; environment = [ordered]@{ SAMPLEGAME_CONTENT__RUNTIMEMODE = 'addressables' }; limitedFilter = 'OneStarMaker.Tests.Editor.TestObservation.ObservationStateTests' }
                scope = @('tools/Harness/**','tools/harness.ps1','tools/run-tests.ps1','tools/contract-audit.ps1','tools/docs-audit.ps1','AGENTS.md','.agents/skills/osm-workflow/**','.agents/skills/osm-unity-editor/**','docs/README.md','unity/Assets/OneStarMaker/Tests/Editor/TestObservation/ObservationStateTests.cs','unity/Assets/OneStarMaker/Tests/Editor/TestObservation/ObservationStateTests.cs.meta')
            }
        }
        'artifact-evidence-lifecycle' {
            return [ordered]@{
                task = $Task; textSha256 = '92fda7d2b05870b47245ba94b76281046607f6f85fb736d03c024d5be425cb01'
                approved = $true; base = 'd1a2606f0e84dde4bfb795e88c21a58cd4a7ecb9'; testPolicy = 'local-gates-v1'; recordPolicy = 'external-current-v1'
                title = 'ARTIFACT-EVIDENCE-LIFECYCLE'; question = '明示非秘密fileを保存・別session閲覧し、作業中保持とtask終了後30日清掃を成立させられるか'
                summary = 'Evidence v2、Workflow終了配送、期限清掃、一回切替、task限定H1'; outOfScope = 'Unity・Build系列・Cloud/別host・GUI・Harness全体改造'
                minimum = '凍結仕様evidence-lifecycle-a3-r5のM1〜M5を満たす'; trialDays = 7; adoptedDaysAfterClose = 30
                profile = $null
                scope = @('tools/Artifacts/**','tools/Workflow/**','tools/artifacts.ps1','tools/workflow-task.ps1','tools/Harness/ApprovedSpecifications.psm1','tools/Harness/RecordStore.psm1','tools/harness.ps1','tools/Harness/GatePolicy.psm1','tools/Harness/Adapters/LocalChecks.psm1','tools/Harness/tests/Harness.Tests.ps1','tools/Harness/README.md','tools/contract-audit.ps1','tools/docs-audit.ps1','.agents/skills/osm-workflow/SKILL.md','.agents/skills/osm-workflow/references/phases-and-handoff.md','docs/README.md','docs/handoff/BUILD_SYSTEM_ARTIFACT_STORAGE_PROGRAM.md','docs/handoff/ARTIFACT_EVIDENCE_LIFECYCLE.md')
                discoverySteps = @('artifacts-evidence-local','workflow-local','harness-local','contract-audit','docs-audit')
                judgmentSteps = @('artifacts-evidence-local','workflow-local','harness-local','contract-audit','docs-audit')
            }
        }
        default { throw "承認されていないtaskです: $Task" }
    }
}

function New-ApprovedSpecification([string]$Task, [string]$Text) {
    $entry = Get-ApprovedSpecification $Task
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Text))).ToLowerInvariant()
    if ($hash -cne $entry.textSha256) { throw 'A3 snapshotのhashが承認値と一致しません。' }
    $body = [ordered]@{}
    $keys = @('approved','title','question','summary','outOfScope','minimum','base','testPolicy','recordPolicy','trialDays','adoptedDaysAfterClose')
    if ($entry.testPolicy -ceq 'unity-pilot-gates-v1') { $keys += @('profile','scope') }
    if ($Task -ceq 'artifact-evidence-lifecycle') { $keys += @('scope','discoverySteps','judgmentSteps') }
    foreach ($key in $keys) { $body[$key] = $entry[$key] }
    $body.text = $Text
    return $body
}

function Assert-ApprovedSpecification([string]$Task, [object]$Spec) {
    $entry = Get-ApprovedSpecification $Task
    if ($null -eq $Spec -or $null -eq $Spec.text -or $Spec.text -isnot [string]) { throw '承認済みA3 snapshot本文がありません。' }
    $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($Spec.text))).ToLowerInvariant()
    if ($actual -cne $entry.textSha256) { throw 'A3 snapshotのhashが承認値と一致しません。' }
    # 承認本文の hash と、登録される構造化値の双方を照合する。mutable JSON の profile/範囲を採用しない。
    $keys = @('approved','title','question','summary','outOfScope','minimum','base','testPolicy','recordPolicy','trialDays','adoptedDaysAfterClose')
    if ($entry.testPolicy -ceq 'unity-pilot-gates-v1') { $keys += @('profile','scope') }
    if ($Task -ceq 'artifact-evidence-lifecycle') { $keys += @('scope','discoverySteps','judgmentSteps') }
    foreach ($key in $keys) {
        if ($Spec -is [Collections.IDictionary]) { if (-not $Spec.Contains($key)) { throw "仕様の$key がありません。" } }
        elseif ($null -eq $Spec.PSObject.Properties[$key]) { throw "仕様の$key がありません。" }
        $value = if ($Spec -is [Collections.IDictionary]) { $Spec[$key] } else { $Spec.$key }
        $expected = $entry[$key]
        if ((ConvertTo-Json -InputObject $value -Depth 30 -Compress) -cne (ConvertTo-Json -InputObject $expected -Depth 30 -Compress)) { throw "承認仕様の$key が一致しません。" }
    }
}

Export-ModuleMember -Function Get-ApprovedSpecification,New-ApprovedSpecification,Assert-ApprovedSpecification
