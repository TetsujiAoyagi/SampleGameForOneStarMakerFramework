Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# XML と log の内容だけから判定する。保存や process の寿命は呼出元が所有する。
function Get-UnityTestResult {
    param(
        [AllowNull()][object]$XmlText,
        [AllowNull()][object]$LogText,
        [AllowNull()][object]$ProcessExitCode,
        [string[]]$OrchestrationFailure = @()
    )

    $failure = [Collections.Generic.List[string]]::new()
    foreach ($item in $OrchestrationFailure) { if ($item) { $failure.Add($item) } }
    $counts = [ordered]@{ total=$null; passed=$null; failed=$null; skipped=$null; inconclusive=$null; executed=$null }
    $cases = [Collections.Generic.List[object]]::new()
    $xmlDuration = $null
    $xmlStarted = $null
    $xmlEnded = $null
    $compileErrors = [Collections.Generic.List[string]]::new()

    if ($null -eq $LogText) {
        $failure.Add('unity.log を読み取れません')
    } else {
        foreach ($line in ([string]$LogText -split "`r?`n")) {
            if ($line -match 'error CS\d+') {
                $value = $line.Trim()
                if (-not $compileErrors.Contains($value)) { $compileErrors.Add($value) }
            }
        }
        if ($compileErrors.Count -gt 0) { $failure.Add('C# コンパイルエラーを検出しました') }
    }

    if ($null -eq $XmlText) {
        $failure.Add('results.xml を読み取れません')
    } else {
        try {
            $settings = [Xml.XmlReaderSettings]::new()
            $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
            $settings.XmlResolver = $null
            $reader = [Xml.XmlReader]::Create([IO.StringReader]::new([string]$XmlText), $settings)
            try {
                $document = [Xml.XmlDocument]::new()
                $document.XmlResolver = $null
                $document.Load($reader)
            } finally { $reader.Dispose() }
            $root = $document.DocumentElement
            if ($null -eq $root -or $root.Name -cne 'test-run') { throw 'document root が test-run ではありません' }

            $xmlStarted = if ($root.HasAttribute('start-time')) { $root.GetAttribute('start-time') } else { $null }
            $xmlEnded = if ($root.HasAttribute('end-time')) { $root.GetAttribute('end-time') } else { $null }
            $durationRaw = $root.GetAttribute('duration')
            $durationValue = [double]0
            if ($durationRaw -and [double]::TryParse($durationRaw, [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$durationValue) -and [double]::IsFinite($durationValue) -and $durationValue -ge 0) {
                $xmlDuration = $durationValue
            }

            foreach ($key in @('total','passed','failed','skipped','inconclusive')) {
                $raw = $root.GetAttribute($key)
                $value = [int]0
                if ($raw -cnotmatch '^(0|[1-9][0-9]*)$' -or -not [int]::TryParse($raw, [ref]$value)) { throw "不正または欠落した XML count: $key" }
                $counts[$key] = $value
            }
            $observed = [ordered]@{ Passed=0; Failed=0; Skipped=0; Inconclusive=0 }
            foreach ($node in $root.SelectNodes('.//test-case')) {
                $result = $node.GetAttribute('result')
                if (-not $observed.Contains($result)) { throw "不明または欠落した test-case result: $result" }
                $observed[$result]++
                $reasonNode = $node.SelectSingleNode('./reason/message')
                $cases.Add([ordered]@{
                    id=$node.GetAttribute('id'); name=$node.GetAttribute('name'); fullname=$node.GetAttribute('fullname')
                    result=$result; label=$node.GetAttribute('label'); reason=$(if ($reasonNode) { $reasonNode.InnerText } else { '' })
                })
            }
            $counts.executed = $observed.Passed + $observed.Failed + $observed.Inconclusive
            if ($counts.total -ne $cases.Count -or $counts.total -ne ($counts.passed + $counts.failed + $counts.skipped + $counts.inconclusive) -or
                $counts.passed -ne $observed.Passed -or $counts.failed -ne $observed.Failed -or
                $counts.skipped -ne $observed.Skipped -or $counts.inconclusive -ne $observed.Inconclusive) {
                $failure.Add('XML の root count と test-case 件数が矛盾しています')
            }
            if ($counts.passed -lt 1) { $failure.Add('成功したテストがありません') }
            if ($counts.failed -gt 0) { $failure.Add('失敗したテストがあります') }
            if ($counts.inconclusive -gt 0) { $failure.Add('Inconclusive のテストがあります') }
            if ($root.GetAttribute('result') -cne 'Passed') { $failure.Add('XML root result が Passed ではありません') }
        } catch {
            $failure.Add("XML を評価できません: $($_.Exception.Message)")
            $counts = [ordered]@{ total=$null; passed=$null; failed=$null; skipped=$null; inconclusive=$null; executed=$null }
            $cases.Clear()
        }
    }

    if ($null -eq $ProcessExitCode) {
        $failure.Add('Unity process の終了コードを取得できません')
    } else {
        $exitValue = [long]$ProcessExitCode
        if ($exitValue -ne 0 -and $exitValue -ne -1073741819 -and $exitValue -ne 3221225477) {
            $failure.Add("Unity process が正常終了しませんでした: $exitValue")
        }
    }
    return [pscustomobject]@{
        status=$(if ($failure.Count -eq 0) { 'passed' } else { 'failed' }); exitCode=$(if ($failure.Count -eq 0) { 0 } else { 1 })
        failure=@($failure.ToArray()); counts=$counts; cases=@($cases.ToArray()); compileErrors=@($compileErrors.ToArray())
        xmlDurationSeconds=$xmlDuration; xmlStartedAt=$xmlStarted; xmlEndedAt=$xmlEnded
    }
}

Export-ModuleMember -Function Get-UnityTestResult
