Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# 閉じた v1 の各 object は必須キーだけを許す。未知キーを policy 側で追認しない。
function Assert-ObservationKeys {
    param([AllowNull()][object]$Value, [string[]]$Keys, [string]$Name)
    if ($Value -isnot [Collections.IDictionary]) { throw "$Name が object ではありません" }
    foreach ($key in $Keys) { if (-not $Value.Contains($key)) { throw "$Name に $key がありません" } }
    # JSON の Count/Keys という名前が IDictionary の member を隠しても、実際の項目数で拒否する。
    if ($Value.PSBase.Count -ne $Keys.Count) { throw "$Name に未凍結の key があります" }
}

function Assert-ObservationScalar([object]$Value, [string]$Kind, [string]$Name) {
    # JSONの数値/真偽をPowerShellが暗黙変換すると、改変した原値を正常な観測として読んでしまう。
    $valid = switch ($Kind) {
        'string' { $Value -is [string] }
        'integer' { $Value -is [int] -or $Value -is [long] }
        'number' { $Value -is [int] -or $Value -is [long] -or $Value -is [double] -or $Value -is [decimal] }
        'boolean' { $Value -is [bool] }
        'array' { $Value -is [array] }
        default { throw "未知の観測型です: $Kind" }
    }
    if (-not $valid) { throw "$Name のJSON型が不正です（$Kind）" }
}

function Assert-ObservationDtoTypes([object]$State, [string]$Name) {
    foreach ($key in @('recordKind','invocationId','projectPath','platform','status')) { Assert-ObservationScalar $State[$key] 'string' "$Name.$key" }
    foreach ($key in @('schemaVersion','processId','sealSequence','sequence')) { Assert-ObservationScalar $State[$key] 'integer' "$Name.$key" }
    Assert-ObservationScalar $State.sealed 'boolean' "$Name.sealed"
    foreach ($key in @('failure','domains','selected','events','results','assemblies')) { Assert-ObservationScalar $State[$key] 'array' "$Name.$key" }
    foreach ($reason in $State.failure) { Assert-ObservationScalar $reason 'string' "$Name.failure[]" }
    foreach ($key in @('status','runStartedUtc','runFinishedUtc')) { Assert-ObservationScalar $State.clock[$key] 'string' "$Name.clock.$key" }
    foreach ($key in @('frequency','runStartedTicks','runFinishedTicks')) { Assert-ObservationScalar $State.clock[$key] 'integer' "$Name.clock.$key" }
    Assert-ObservationScalar $State.clock.durationMs 'number' "$Name.clock.durationMs"
    foreach ($node in $State.domains) {
        foreach ($key in @('ordinal','bootstrapTicks','reloadBeforeTicks')) { Assert-ObservationScalar $node[$key] 'integer' "$Name.domains[].$key" }
        foreach ($key in @('id','bootstrapUtc','reloadBeforeUtc')) { Assert-ObservationScalar $node[$key] 'string' "$Name.domains[].$key" }
    }
    foreach ($node in $State.selected) { foreach ($key in @('id','name','fullname','uniqueName','assemblyName','runState')) { Assert-ObservationScalar $node[$key] 'string' "$Name.selected[].$key" } }
    foreach ($node in $State.events) {
        foreach ($key in @('sequence','domainOrdinal','ticks')) { Assert-ObservationScalar $node[$key] 'integer' "$Name.events[].$key" }
        foreach ($key in @('kind','id','result','label','utc')) { Assert-ObservationScalar $node[$key] 'string' "$Name.events[].$key" }
    }
    foreach ($node in $State.results) { foreach ($key in @('id','name','fullname','result','label')) { Assert-ObservationScalar $node[$key] 'string' "$Name.results[].$key" } }
    foreach ($node in $State.assemblies) {
        foreach ($key in @('domainOrdinal','ticks')) { Assert-ObservationScalar $node[$key] 'integer' "$Name.assemblies[].$key" }
        foreach ($key in @('observedAtUtc','fullName','location','loadedModuleVersionId','diskSha256','status','failure')) { Assert-ObservationScalar $node[$key] 'string' "$Name.assemblies[].$key" }
    }
}

# Unity process の外で、同じ invocation の生文字列だけを検査する。path の存在や process 起動は runner が所有する。
function Get-UnityObservationResult {
    param(
        [AllowNull()][object]$XmlText, [AllowNull()][object]$ObservationText,
        [AllowNull()][object]$ProgressText, [AllowNull()][object]$LogText,
        [string]$ExpectedInvocation, [string]$ExpectedProject, [int]$ExpectedPid,
        [string]$ExpectedPath, [bool]$RequireReload = $false
    )
    $failure = [Collections.Generic.List[string]]::new()
    $status = 'incomplete'
    if ($null -ne $LogText -and [string]$LogText -cmatch 'OSM_OBSERVATION_ERROR') {
        $failure.Add('観測器の error log を検出しました')
        $status = 'invalid'
    }
    if ($null -eq $ObservationText -or $null -eq $ProgressText) {
        $failure.Add('seal または progress がありません')
        return [pscustomobject]@{ status=$status; failure=@($failure.ToArray()); clock=$null; assemblies=$null }
    }
    try {
        $terminal = ConvertFrom-Json -InputObject ([string]$ObservationText) -AsHashtable -DateKind String -Depth 64
        $progress = ConvertFrom-Json -InputObject ([string]$ProgressText) -AsHashtable -DateKind String -Depth 64
        if ($terminal -isnot [Collections.IDictionary] -or $progress -isnot [Collections.IDictionary]) { throw 'JSON root が object ではありません' }
        foreach ($item in @(@('terminal',$terminal),@('progress',$progress))) {
            $name = $item[0]; $s = $item[1]
            $closedKeys = @('schemaVersion','recordKind','invocationId','projectPath','processId','platform','status','failure','sealed','sealSequence','sequence','clock','domains','selected','events','results','assemblies')
            Assert-ObservationKeys $s $closedKeys $name
            Assert-ObservationKeys $s.clock @('status','frequency','runStartedTicks','runFinishedTicks','runStartedUtc','runFinishedUtc','durationMs') "$name.clock"
            foreach ($node in @($s.domains)) { Assert-ObservationKeys $node @('ordinal','id','bootstrapUtc','bootstrapTicks','reloadBeforeUtc','reloadBeforeTicks') "$name.domains[]" }
            foreach ($node in @($s.selected)) { Assert-ObservationKeys $node @('id','name','fullname','uniqueName','assemblyName','runState') "$name.selected[]" }
            foreach ($node in @($s.events)) { Assert-ObservationKeys $node @('sequence','domainOrdinal','kind','id','result','label','utc','ticks') "$name.events[]" }
            foreach ($node in @($s.results)) { Assert-ObservationKeys $node @('id','name','fullname','result','label') "$name.results[]" }
            foreach ($node in @($s.assemblies)) { Assert-ObservationKeys $node @('domainOrdinal','observedAtUtc','ticks','fullName','location','loadedModuleVersionId','diskSha256','status','failure') "$name.assemblies[]" }
            Assert-ObservationDtoTypes $s $name
            if ($s.schemaVersion -ne 1 -or $s.recordKind -cne 'unity-test-observation' -or
                $s.invocationId -cne $ExpectedInvocation -or $s.projectPath -cne $ExpectedProject -or
                $s.processId -ne $ExpectedPid -or $ExpectedPid -le 0 -or $s.platform -cne 'EditMode') {
                throw "$name の schema/identity が一致しません"
            }
        }
        # writer は同じ state を連続保存する。seal 後の callback は progress だけ進むので raw 同一性で拒否する。
        if ([string]$ObservationText -cne [string]$ProgressText) { throw 'terminal と progress が異なります' }
        if ($terminal.sealed -ne $true -or $terminal.sealSequence -ne $terminal.sequence -or
            $terminal.sequence -le 0 -or $terminal.status -cnotin @('complete','incomplete','invalid')) {
            throw 'seal または observation status が成立しません'
        }
        if ($ExpectedPath) {
            $fullPath = [IO.Path]::GetFullPath($ExpectedPath)
            if ($fullPath -cne [IO.Path]::Combine([IO.Path]::GetDirectoryName($fullPath),'observation.json') -or
                [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($fullPath)) -cne $ExpectedInvocation) {
                throw 'terminal path と GUID directory が一致しません'
            }
        }
        if ($terminal.status -cne 'complete') {
            # nonexecuted failed leaf の incomplete は状態分類として保存する。XML の Failed は別の policy が判定する。
            foreach ($reason in @($terminal.failure)) { $failure.Add([string]$reason) }
            $rawStatus = if ($status -ceq 'invalid' -or $terminal.status -ceq 'invalid') { 'invalid' } else { 'incomplete' }
            return [pscustomobject]@{ status=$rawStatus; failure=@($failure.ToArray()); clock=$null; assemblies=$null }
        }
        if (@($terminal.failure).Count -ne 0) { throw 'complete state に failure が含まれます' }
        $clock = $terminal.clock
        if ($clock.status -cne 'observed' -or [long]$clock.frequency -le 0 -or
            [long]$clock.runStartedTicks -le 0 -or [long]$clock.runFinishedTicks -le [long]$clock.runStartedTicks -or
            [string]::IsNullOrEmpty($clock.runStartedUtc) -or [string]::IsNullOrEmpty($clock.runFinishedUtc)) {
            throw '時計の観測値が不完全です'
        }
        [void][DateTimeOffset]::Parse([string]$clock.runStartedUtc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind)
        [void][DateTimeOffset]::Parse([string]$clock.runFinishedUtc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind)
        $expectedDuration = 1000.0 * ([double]([long]$clock.runFinishedTicks - [long]$clock.runStartedTicks)) / [double]$clock.frequency
        if ([Math]::Abs([double]$clock.durationMs - $expectedDuration) -gt 0.001) { throw '時計の duration が ticks/frequency と矛盾します' }
        $domains = @($terminal.domains)
        if ($domains.Count -lt 1) { throw 'domain がありません' }
        if ($RequireReload -and $domains.Count -lt 2) { throw '正常 reload の復帰 domain がありません' }
        for ($i = 0; $i -lt $domains.Count; $i++) {
            $domain = $domains[$i]
            if ($domain.ordinal -ne $i -or [string]::IsNullOrEmpty($domain.id) -or
                [string]::IsNullOrEmpty($domain.bootstrapUtc) -or [long]$domain.bootstrapTicks -le 0) { throw 'domain sequence が不正です' }
            # domain は初回 RunStarted 前にも作られるが、終端結果より後の復帰/境界は成功寿命に含めない。
            if ([long]$domain.bootstrapTicks -gt [long]$clock.runFinishedTicks -or
                [long]$domain.reloadBeforeTicks -gt [long]$clock.runFinishedTicks) { throw 'RunFinished 後の domain/reload が含まれます' }
            if ($i -gt 0 -and [long]$domain.bootstrapTicks -lt [long]$domains[$i-1].reloadBeforeTicks) { throw 'reload 復帰の単調時計が逆行しました' }
            if ($i -lt $domains.Count - 1 -and ([string]::IsNullOrEmpty($domain.reloadBeforeUtc) -or [long]$domain.reloadBeforeTicks -le 0)) {
                throw 'reload boundary がありません'
            }
            if ($i -eq $domains.Count - 1 -and -not [string]::IsNullOrEmpty($domain.reloadBeforeUtc)) { throw '最後の domain が復帰していません' }
        }
        $selected = @($terminal.selected); $results = @($terminal.results); $events = @($terminal.events)
        if ($selected.Count -lt 1 -or $selected.Count -ne $results.Count) { throw '選択と結果の数が一致しません' }
        $ids = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        for ($i = 0; $i -lt $selected.Count; $i++) {
            $s = $selected[$i]; $r = $results[$i]
            if ([string]::IsNullOrEmpty($s.id) -or -not $ids.Add([string]$s.id) -or
                $s.id -cne $r.id -or $s.name -cne $r.name -or $s.fullname -cne $r.fullname -or
                [string]::IsNullOrEmpty($s.uniqueName) -or [string]::IsNullOrEmpty($s.assemblyName) -or
                [string]::IsNullOrEmpty($s.runState) -or
                $r.result -cnotin @('Passed','Failed','Inconclusive','Skipped')) { throw '選択/終端 leaf が不正です' }
        }
        $xmlCases = [Collections.Generic.List[object]]::new()
        if ($null -eq $XmlText) { throw 'XML がありません' }
        $settings = [Xml.XmlReaderSettings]::new(); $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit; $settings.XmlResolver = $null
        $reader = [Xml.XmlReader]::Create([IO.StringReader]::new([string]$XmlText),$settings)
        try { $xml = [Xml.XmlDocument]::new(); $xml.XmlResolver = $null; $xml.Load($reader) } finally { $reader.Dispose() }
        foreach ($node in $xml.DocumentElement.SelectNodes('.//test-case')) { $xmlCases.Add($node) }
        if ($xmlCases.Count -ne $results.Count) { throw 'XML leaf 件数が一致しません' }
        for ($i = 0; $i -lt $results.Count; $i++) {
            $r = $results[$i]; $node = $xmlCases[$i]
            foreach ($key in @('id','name','fullname','result','label')) {
                if ($node.GetAttribute($key) -cne [string]$r[$key]) { throw "XML と終端 leaf の $key が不一致です" }
            }
        }
        $lastSequence = 0L; $lastTicks = 0L
        foreach ($event in $events) {
            if ($event.sequence -le $lastSequence -or $event.ticks -lt $lastTicks -or
                [long]$event.ticks -lt [long]$clock.runStartedTicks -or [long]$event.ticks -gt [long]$clock.runFinishedTicks -or
                $event.domainOrdinal -lt 0 -or $event.domainOrdinal -ge $domains.Count -or
                $event.kind -cnotin @('started','finished') -or -not $ids.Contains([string]$event.id) -or
                [string]::IsNullOrEmpty($event.utc)) { throw 'callback event の順序または identity が不正です' }
            if ($event.kind -ceq 'started' -and ($event.result -cne '' -or $event.label -cne '')) { throw 'started event に結果があります' }
            $lastSequence = [long]$event.sequence; $lastTicks = [long]$event.ticks
        }
        foreach ($r in $results) {
            $started = @($events | Where-Object { $_.id -ceq $r.id -and $_.kind -ceq 'started' })
            $finished = @($events | Where-Object { $_.id -ceq $r.id -and $_.kind -ceq 'finished' })
            if ($started.Count -gt 1 -or $finished.Count -gt 1 -or
                ($r.result -cne 'Skipped' -and ($started.Count -ne 1 -or $finished.Count -ne 1)) -or
                ($r.result -ceq 'Skipped' -and $started.Count -ne $finished.Count)) { throw "callback 欠測または複数 attempt: $($r.id)" }
            if ($started.Count -eq 1 -and $finished.Count -eq 1 -and $started[0].sequence -ge $finished[0].sequence) {
                throw "callback の開始/終了順序が逆です: $($r.id)"
            }
            if ($finished.Count -eq 1 -and ($finished[0].result -cne $r.result -or $finished[0].label -cne $r.label)) {
                throw "callback result/label が不一致です: $($r.id)"
            }
        }
        $assemblies = @($terminal.assemblies)
        if ($assemblies.Count -lt 1) { throw 'loaded assembly 観測がありません' }
        $assemblyNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $identity = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
        $captureNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($assembly in $assemblies) {
            if ($assembly.status -cne 'observed' -or [string]::IsNullOrEmpty($assembly.fullName) -or
                [string]::IsNullOrEmpty($assembly.location) -or [string]::IsNullOrEmpty($assembly.loadedModuleVersionId) -or
                [string]::IsNullOrEmpty($assembly.observedAtUtc) -or [long]$assembly.ticks -le 0 -or
                -not [string]::IsNullOrEmpty($assembly.failure) -or
                $assembly.diskSha256 -cnotmatch '^[A-F0-9]{64}$' -or $assembly.domainOrdinal -lt 0 -or
                $assembly.domainOrdinal -ge $domains.Count) { throw 'loaded assembly identity が欠測しました' }
            $full = [string]$assembly.fullName
            if (-not $captureNames.Add("$($assembly.domainOrdinal)|$($assembly.ticks)|$full")) { throw "同名 assembly の複数実体です: $full" }
            [void]$assemblyNames.Add($full.Split(',')[0])
            $fingerprint = "$($assembly.location)|$($assembly.loadedModuleVersionId)|$($assembly.diskSha256)"
            if ($identity.ContainsKey($full) -and $identity[$full] -cne $fingerprint) { throw "同名 assembly の identity が変化しました: $full" }
            $identity[$full] = $fingerprint
        }
        foreach ($name in @('OneStarMaker.Editor.TestObservation','UnityEditor.TestRunner','UnityEngine.TestRunner')) {
            if (-not $assemblyNames.Contains($name)) { throw "必須 assembly がありません: $name" }
        }
        foreach ($s in $selected) { if (-not $assemblyNames.Contains([string]$s.assemblyName)) { throw "test assembly がありません: $($s.assemblyName)" } }
        $runStartDomain = $null; $runFinishDomain = $null
        foreach ($domain in $domains) {
            if ([long]$domain.bootstrapTicks -le [long]$clock.runStartedTicks) { $runStartDomain = $domain }
            if ([long]$domain.bootstrapTicks -le [long]$clock.runFinishedTicks) { $runFinishDomain = $domain }
        }
        if ($null -eq $runStartDomain -or $null -eq $runFinishDomain) { throw '実行時計に対応する domain がありません' }
        $capturePoints = [Collections.Generic.List[object]]::new()
        $capturePoints.Add([pscustomobject]@{ phase='RunStarted'; ordinal=$runStartDomain.ordinal; utc=$clock.runStartedUtc; ticks=[long]$clock.runStartedTicks })
        foreach ($domain in $domains) {
            if ($domain.ordinal -gt $runStartDomain.ordinal -and [long]$domain.bootstrapTicks -le [long]$clock.runFinishedTicks) {
                $capturePoints.Add([pscustomobject]@{ phase='reload 復帰'; ordinal=$domain.ordinal; utc=$domain.bootstrapUtc; ticks=[long]$domain.bootstrapTicks })
            }
        }
        $capturePoints.Add([pscustomobject]@{ phase='RunFinished'; ordinal=$runFinishDomain.ordinal; utc=$clock.runFinishedUtc; ticks=[long]$clock.runFinishedTicks })
        $requiredNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($name in @('OneStarMaker.Editor.TestObservation','UnityEditor.TestRunner','UnityEngine.TestRunner')) { [void]$requiredNames.Add($name) }
        foreach ($s in $selected) { [void]$requiredNames.Add([string]$s.assemblyName) }
        foreach ($point in $capturePoints) {
            # 既存 clock/domain の stamp と同じ assembly 群を要求し、別地点の集合を流用させない。
            $group = @($assemblies | Where-Object { $_.domainOrdinal -eq $point.ordinal -and [long]$_.ticks -eq $point.ticks -and $_.observedAtUtc -ceq $point.utc })
            if ($group.Count -eq 0) { throw "$($point.phase) の loaded assembly capture がありません" }
            $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($item in $group) { [void]$names.Add(([string]$item.fullName).Split(',')[0]) }
            foreach ($name in $requiredNames) { if (-not $names.Contains($name)) { throw "$($point.phase) の必須 assembly がありません: $name" } }
        }
        if ($failure.Count -eq 0) { $status = 'complete' }
        return [pscustomobject]@{ status=$status; failure=@($failure.ToArray()); clock=$clock; assemblies=$assemblies }
    }
    catch {
        $failure.Add($_.Exception.Message)
        return [pscustomobject]@{ status='invalid'; failure=@($failure.ToArray()); clock=$null; assemblies=$null }
    }
}

Export-ModuleMember -Function Get-UnityObservationResult
