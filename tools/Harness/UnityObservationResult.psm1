Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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
        $terminal = ConvertFrom-Json -InputObject ([string]$ObservationText) -AsHashtable -Depth 64
        $progress = ConvertFrom-Json -InputObject ([string]$ProgressText) -AsHashtable -Depth 64
        if ($terminal -isnot [Collections.IDictionary] -or $progress -isnot [Collections.IDictionary]) { throw 'JSON root が object ではありません' }
        foreach ($item in @(@('terminal',$terminal),@('progress',$progress))) {
            $name = $item[0]; $s = $item[1]
            foreach ($key in @('schemaVersion','recordKind','invocationId','projectPath','processId','platform','status','failure','sealed','sealSequence','sequence','clock','domains','selected','events','results','assemblies','runStarted','runFinished')) {
                if (-not $s.Contains($key)) { throw "$name に $key がありません" }
            }
            if ($s.schemaVersion -ne 1 -or $s.recordKind -cne 'unity-test-observation' -or
                $s.invocationId -cne $ExpectedInvocation -or $s.projectPath -cne $ExpectedProject -or
                $s.processId -ne $ExpectedPid -or $ExpectedPid -le 0 -or $s.platform -cne 'EditMode') {
                throw "$name の schema/identity が一致しません"
            }
        }
        # writer は同じ state を連続保存する。seal 後の callback は progress だけ進むので raw 同一性で拒否する。
        if ([string]$ObservationText -cne [string]$ProgressText) { throw 'terminal と progress が異なります' }
        if ($terminal.sealed -ne $true -or $terminal.sealSequence -ne $terminal.sequence -or
            $terminal.sequence -le 0 -or $terminal.status -cne 'complete' -or
            @($terminal.failure).Count -ne 0 -or $terminal.runStarted -ne $true -or $terminal.runFinished -ne $true) {
            throw 'seal または complete state が成立しません'
        }
        if ($ExpectedPath) {
            $fullPath = [IO.Path]::GetFullPath($ExpectedPath)
            if ($fullPath -cne [IO.Path]::Combine([IO.Path]::GetDirectoryName($fullPath),'observation.json') -or
                [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($fullPath)) -cne $ExpectedInvocation) {
                throw 'terminal path と GUID directory が一致しません'
            }
        }
        $clock = $terminal.clock
        foreach ($key in @('status','frequency','runStartedTicks','runFinishedTicks','runStartedUtc','runFinishedUtc','durationMs')) {
            if (-not $clock.Contains($key)) { throw "clock.$key がありません" }
        }
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
        if ($failure.Count -eq 0) { $status = 'complete' }
        return [pscustomobject]@{ status=$status; failure=@($failure.ToArray()); clock=$clock; assemblies=$assemblies }
    }
    catch {
        $failure.Add($_.Exception.Message)
        return [pscustomobject]@{ status='invalid'; failure=@($failure.ToArray()); clock=$null; assemblies=$null }
    }
}

Export-ModuleMember -Function Get-UnityObservationResult
