# 完了引渡しに必要な実行と、run がその実行を満たすかの判定だけを置く。
# 子プロセスや Git の採取は LocalChecks、保存は RecordStore。ここは仕様と run の突合。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'ApprovedSpecifications.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'UnityGatePolicy.psm1') -Force

function Assert-ApprovedSpec([string]$Task, [object]$Spec) {
    Assert-ApprovedSpecification $Task $Spec
}

function Get-RequiredSteps([object]$Spec, [string[]]$ChangedPaths, [string]$Stage, [object]$Changes = $null) {
    if ((($Spec -is [Collections.IDictionary] -and $Spec.Contains('title')) -or $Spec.PSObject.Properties['title']) -and $Spec.title -ceq 'ARTIFACT-EVIDENCE-LIFECYCLE') {
        Assert-ApprovedSpecification 'artifact-evidence-lifecycle' $Spec
        if ($Stage -cnotin @('discovery','judgment') -or $ChangedPaths.Count -eq 0) { throw 'stageまたは変更集合が不正です。' }
        foreach ($path in $ChangedPaths) {
            $path = $path.Replace('\', '/')
            if ($path -ceq 'docs/handoff/ARTIFACT_EVIDENCE_LIFECYCLE.md' -and ($null -eq $Changes -or $Changes[$path] -cne 'D')) { throw 'Git外specへ投影するHANDOFFの削除だけが承認されています。' }
            $allowed = @($Spec.scope | Where-Object { $path -clike $_ }).Count -gt 0
            if (-not $allowed -or $path -match '^tools/(Artifacts|Workflow)/(.*/)?(artifacts|bin|obj)/') { throw "Evidence taskで未承認の変更pathです: $path" }
        }
        return @($(if ($Stage -ceq 'discovery') { $Spec.discoverySteps } else { $Spec.judgmentSteps }))
    }
    if ($Spec.testPolicy -ceq 'unity-pilot-gates-v1') { return @(Get-UnityRequiredSteps $Spec $ChangedPaths $Stage) }
    if ($Spec.testPolicy -cne 'local-gates-v1' -or $Spec.recordPolicy -cne 'external-current-v1') { throw 'H1適用済みの仕様ではありません。' }
    # 変更パスから必須 step を決める。未知のパス（Unity を含む）は、触った面だけの成功に見せず拒否する。
    # discovery は触った面と契約検査。judgment は差分が書類だけでも offline suite 4種すべてを要求する。
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

function Assert-RunForGate([object]$Run, [object]$Spec, [string]$Head, [string[]]$RequiredSteps, [string]$RequiredStage = '') {
    # 採用は「この run を見る」であり、ここを通過したことではない。失敗 run も採用できるが引渡しには使えない。
    # dirty の null は「汚れていない」ではなく未採取。空配列だけを清浄とみなす。
    if ($Run.specHash -cne $Spec.specHash -or $Run.base -cne $Spec.base -or $Run.head -cne $Head) { throw 'runの仕様またはbase/headが候補と一致しません。' }
    if ($null -eq $Run.dirtyBefore -or $null -eq $Run.dirtyAfter -or $Run.dirtyBefore -isnot [array] -or $Run.dirtyAfter -isnot [array]) { throw 'run前後のdirty状態が確定していません。' }
    if ($Run.dirtyBefore -or $Run.dirtyAfter) { throw 'dirtyなrunは完了引渡しに使えません。commitするかtrialとして実行してください。' }
    if ($Run.status -cne 'passed') { throw '失敗または未完了のrunは採用できてもgateは通過できません。' }
    if ((($Spec -is [Collections.IDictionary] -and $Spec.Contains('title')) -or $Spec.PSObject.Properties['title']) -and $Spec.title -ceq 'ARTIFACT-EVIDENCE-LIFECYCLE') {
        if (-not $Run.PSObject.Properties['taskId'] -or $Run.taskId -cne 'artifact-evidence-lifecycle') { throw 'Evidence runのtask identityが不一致です。' }
        Assert-CaseSets @($Spec.discoverySteps) @($RequiredSteps) @($RequiredSteps) 'Evidence gate steps'
        Assert-ApprovedSpecification $Run.taskId $Spec
        if ($Run.suiteVersion -cne $Spec.testPolicy -or $Run.specId -cne $Spec.id -or $Run.stage -cnotin @('discovery','judgment') -or ($RequiredStage -and $Run.stage -cne $RequiredStage)) { throw 'Evidence runのtask/policy/仕様ID/stageが不一致です。' }
        if (@($Run.steps).Count -ne $RequiredSteps.Count -or @($Run.steps | Where-Object { $RequiredSteps -cnotcontains $_.name }).Count -gt 0) { throw 'Evidence step集合が固定5件と一致しません。' }
        foreach ($step in @($Run.steps | Where-Object { $_.name -like '*local' })) {
            $expected = switch ($step.name) {
                'artifacts-evidence-local' { @('Credentials','RouteProof','R2RouteTransport','ArtifactPackage','ArtifactTransfer','ArtifactRotation','ArtifactEvidence','EvidenceRetention','EvidenceCleanup','EvidenceSchedule','EvidenceReset') }
                'workflow-local' { @('TaskLifecycle') }
                'harness-local' { @('Harness') }
                default { throw '未知のEvidence suiteです。' }
            }
            $actual = @($step.registered | ForEach-Object { ($_ -split '/',2)[0] } | Sort-Object -Unique -CaseSensitive)
            Assert-CaseSets @($expected) @($actual) @($actual) $step.name
        }
    }
    $unityPilot = $Spec.PSObject.Properties['testPolicy'] -and $Spec.testPolicy -ceq 'unity-pilot-gates-v1'
    if ($unityPilot) {
        # run の task と承認本文を結び、別revisionの承認値を取り違えた引渡しを拒否する。
        Assert-ApprovedSpecification $Run.taskId $Spec
        if ($Run.suiteVersion -cne $Spec.testPolicy -or $Run.specId -cne $Spec.id) { throw 'Unity pilot runのtask/policy/仕様IDが不一致です。' }
        $requiredStage = if ($RequiredSteps -ccontains 'unity-editmode-full') { 'judgment' } else { 'discovery' }
        if ($Run.stage -cne $requiredStage) { throw 'Unity pilotのB限定runとC全件runはprofile別の引渡しです。' }
        if (@($Run.steps).Count -ne $RequiredSteps.Count) { throw 'step数が必須集合と一致しません。' }
    }
    foreach ($name in $RequiredSteps) {
        $found = @($Run.steps | Where-Object { $_.name -ceq $name -and $_.status -ceq 'passed' })
        if ($found.Count -ne 1) { throw "必須stepが成功していません: $name" }
        if ($name -like '*local') {
            Assert-CaseSets @($found[0].registered) @($found[0].selected) @($found[0].executed) $name
        }
    }
    if ($unityPilot -and @($Run.steps | Where-Object { $RequiredSteps -cnotcontains $_.name }).Count -gt 0) { throw '未知または重複stepがあります。' }
}

function Assert-CaseSets([string[]]$Registered, [string[]]$Selected, [string[]]$Executed, [string]$Name) {
    # 登録・選択・実行は同じ集合。順序は問わず、空名と重複は件数を揃えても通せない。
    # フィルタで実行を減らすと選択が登録より狭くなり、完了引渡しは失敗する。
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

function Resolve-CloseReferences([object[]]$References, [string]$Task, [bool]$ReviewClosed, [DateTimeOffset]$ClosedAt, [string]$TaskOwner) {
    # この task 自身の採用 run と、終了を明示したレビュー参照だけを閉じる。
    # 欠陥や他 consumer の参照は残す。期限切れの有効参照が1件でもあれば close しない。
    $resolved = [Collections.Generic.List[object]]::new()
    foreach ($item in $References) {
        $ref = $item | ConvertTo-Json -Depth 10 | ConvertFrom-Json -DateKind String
        $ownReview = $ref.purpose -ceq 'review' -and $ref.owner -ceq $TaskOwner -and $ref.consumerId -cin @('CDiscovery','CJudgment','CBlind')
        if (-not $ref.releasedAt -and $ownReview -and -not $ReviewClosed) { throw '進行中reviewがあります。終了済みなら-ReviewClosedで明示してください。' }
        if (-not $ref.releasedAt -and (($ref.purpose -ceq 'adopted-run' -and $ref.consumerId -ceq $Task) -or ($ownReview -and $ReviewClosed))) { $ref.releasedAt = $ClosedAt.ToString('o') }
        if (-not $ref.releasedAt -and (!$ref.expiresAt -or [DateTimeOffset]::Parse($ref.expiresAt) -le $ClosedAt)) { throw "期限切れ参照があります: $($ref.referenceId)。所有者が解放または有限延長してください。" }
        $resolved.Add($ref)
    }
    return @($resolved)
}

function New-RunResult([object]$Spec, [object]$Current, [string]$Task, [string]$Stage, [object]$Scope, [object]$After, [DateTimeOffset]$Started, [long]$ElapsedMs, [string]$Id, [string]$PreviousRun, [string]$Difference, [string]$Question, [string]$StopWhen, [object[]]$Results, [string]$Failure, [string]$ImplementationResult = '') {
    # 事後のHEAD/dirtyを採れない実行は成功にしない。記録の寿命は凍結仕様の値を使う。
    $failed = @($Results | Where-Object { $_.status -cne 'passed' }).Count -gt 0
    $status = if ($Failure -or $failed -or -not $After -or $Scope.Head -cne $After.Head) { 'failed' } else { 'passed' }
    $dirtyAfter = if ($After) { @($After.Dirty | Where-Object { $_ }) } else { @('post-run scope unavailable') }
    $ended = [DateTimeOffset]::UtcNow
    # suiteはA3の固定policy版、adapterは同じrepository commitの実装版で特定する。
    return [ordered]@{ schemaVersion = 1; id = $Id; repoId = $Current.repoId; taskId = $Task; base = $Spec.base; head = $Scope.Head; specId = $Current.specId; specHash = $Spec.specHash; suiteVersion = $Spec.testPolicy; adapterVersion = "git:$($Scope.Head)"; stage = $Stage; changedPaths = @($Scope.Paths); dirtyBefore = @($Scope.Dirty); dirtyAfter = @($dirtyAfter); startedAt = $Started.ToString('o'); endedAt = $ended.ToString('o'); durationMs = $ElapsedMs; predecessor = $PreviousRun; difference = $Difference; question = $Question; stopWhen = $StopWhen; steps = @($Results); status = $status; failure = $Failure; implementationResult = $(if ($ImplementationResult) { $ImplementationResult } else { '固定base/headの変更pathと実行結果を参照' }); retainUntil = $Started.AddDays([double]$Spec.trialDays).ToString('o') }
}

function Assert-HandoffCandidate([object]$Current, [string]$To) {
    if ($To -ceq 'CJudgment' -and @($Current.blockers).Count -gt 0) { throw '未解決blockerがあります。' }
}

function New-CloseRecord([object]$Current, [string]$Task, [string]$Owner, [string]$Outcome, [string]$Reason, [string]$CReview, [string]$CBlindReview, [bool]$ReviewClosed, [DateTimeOffset]$ClosedAt) {
    # 判定条件と参照期限から終端recordの値だけを作る。確定保存と競合検査はRecordStoreが行う。
    if (-not $Reason) { throw 'closeには-Reasonが必要です。' }
    if ($Owner -cne $Current.owner) { throw 'task ownerを-Ownerで明示してください。' }
    if ($Outcome -ceq 'Completed' -and (-not $Current.judgmentInputId -or -not $CReview -or -not $CBlindReview)) { throw 'Completedには判定入力とC/C′結果の明示が必要です。' }
    $closedRefs = @(Resolve-CloseReferences @($Current.references) $Task $ReviewClosed $ClosedAt $Owner)
    $retainUntil = $ClosedAt.AddDays(30)
    foreach ($ref in @($closedRefs | Where-Object { $_.expiresAt -and -not $_.releasedAt })) {
        $expiry = [DateTimeOffset]::Parse($ref.expiresAt)
        if ($expiry -gt $retainUntil) { $retainUntil = $expiry }
    }
    return [ordered]@{ kind = 'closed'; taskId = $Task; owner = $Owner; closedAt = $ClosedAt.ToString('o'); outcome = $Outcome; reason = $Reason; cReview = $CReview; cBlindReview = $CBlindReview; retainUntil = $retainUntil.ToString('o'); references = @($closedRefs) }
}

function Select-BlindInput([object]$Spec, [object]$Run, [string]$Kind, [string[]]$ChangedPaths, [string]$Diff = '') {
    if ($Kind -cnotin @('discovery', 'judgment')) { throw '入力種別が不正です。' }
    # 可変の CURRENT とレビュー所見は写さない。仕様本文、固定 base/head、差分、run の hash と step だけを残す。
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
        predecessor = $Run.predecessor
        steps = @($Run.steps)
        implementationResult = $Run.implementationResult
        observation = $(if ($Run.PSObject.Properties['observation']) { $Run.observation } else { $null })
    }
}

Export-ModuleMember -Function Assert-ApprovedSpec,Get-RequiredSteps,Assert-RunForGate,New-RunResult,Assert-HandoffCandidate,Assert-CaseSets,Resolve-CloseReferences,New-CloseRecord,Select-BlindInput
