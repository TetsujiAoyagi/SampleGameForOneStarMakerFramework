# 標準Unity runnerの起動・生結果の固定と、固定済みpayloadの読取再検査。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot '../UnityTestResult.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../UnityObservationResult.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../UnityGatePolicy.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'UnityTestOutput.psm1') -Force

function Get-RawSha([string]$Path) { return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant() }
function Get-RelativePayload([string]$TaskDirectory, [string]$Path) { return [IO.Path]::GetRelativePath($TaskDirectory, $Path) }
function Assert-ClosedKeys([object]$Value, [string[]]$Keys, [string]$Label) {
    if ($Value -isnot [Collections.IDictionary]) { throw "$Label がobjectではありません。" }
    foreach ($key in $Keys) { if (-not $Value.Contains($key)) { throw "$Label に$key がありません。" } }
    if ($Value.PSBase.Count -ne $Keys.Count) { throw "$Label に未知keyがあります。" }
}
function Assert-Integer([object]$Value, [string]$Label, [long]$Minimum = 0) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or [long]$Value -lt $Minimum) { throw "$Label の整数型または範囲が不正です。" }
}
function Assert-UnityType([object]$Value, [string]$Kind, [string]$Label) {
    # ConvertFrom-Json の後に比較すると true/1 や単要素array/stringが暗黙変換される。原JSON型を先に閉じる。
    $valid = switch ($Kind) {
        'string' { $Value -is [string] }
        'integer' { $Value -is [int] -or $Value -is [long] }
        'number' { $Value -is [int] -or $Value -is [long] -or $Value -is [double] -or $Value -is [decimal] }
        'boolean' { $Value -is [bool] }
        'array' { $Value -is [array] }
        'object' { $Value -is [Collections.IDictionary] }
        'nullable-string' { $null -eq $Value -or $Value -is [string] }
        'nullable-number' { $null -eq $Value -or $Value -is [int] -or $Value -is [long] -or $Value -is [double] -or $Value -is [decimal] }
        default { throw "未知のUnity DTO型です: $Kind" }
    }
    if (-not $valid) { throw "$Label のJSON型が不正です（$Kind）。" }
}
function Assert-UnityStringArray([object]$Value, [string]$Label) {
    Assert-UnityType $Value 'array' $Label
    foreach ($item in $Value) { Assert-UnityType $item 'string' "$Label[]" }
}
function Assert-OuterUnityTypes([object]$Step) {
    Assert-ClosedKeys $Step @('name','kind','adapterKind','status','exitCode','failure','argv','cwd','durationMs','timing','logs','logHash','profile','runner','result','payloads','observedAssemblies') 'Unity outer step'
    foreach ($key in @('name','kind','adapterKind','status','cwd','logHash','profile')) { Assert-UnityType $Step[$key] 'string' "outer.$key" }
    foreach ($key in @('exitCode','durationMs')) { Assert-UnityType $Step[$key] 'integer' "outer.$key" }
    foreach ($key in @('failure','logs')) { Assert-UnityStringArray $Step[$key] "outer.$key" }
    Assert-UnityType $Step.argv 'array' 'outer.argv'
    if ($Step.argv.Count -ne 1) { throw 'outer.argv は一件だけです。' }
    Assert-ClosedKeys $Step.argv[0] @('executable','arguments') 'outer.argv[0]'
    Assert-UnityType $Step.argv[0].executable 'string' 'outer.argv[0].executable'
    Assert-UnityStringArray $Step.argv[0].arguments 'outer.argv[0].arguments'
    Assert-ClosedKeys $Step.timing @('runnerWallMs','collectionMs') 'outer.timing'
    foreach ($key in @('runnerWallMs','collectionMs')) { Assert-UnityType $Step.timing[$key] 'integer' "outer.timing.$key" }
    Assert-ClosedKeys $Step.runner @('exitCode','stdoutPath','stdoutSha256','stderrPath','stderrSha256','environment') 'outer.runner'
    Assert-UnityType $Step.runner.exitCode 'integer' 'outer.runner.exitCode'
    foreach ($key in @('stdoutPath','stdoutSha256','stderrPath','stderrSha256')) { Assert-UnityType $Step.runner[$key] 'string' "outer.runner.$key" }
    Assert-ClosedKeys $Step.runner.environment @('SAMPLEGAME_CONTENT__RUNTIMEMODE') 'outer.runner.environment'
    Assert-UnityType $Step.runner.environment.SAMPLEGAME_CONTENT__RUNTIMEMODE 'string' 'outer.runner.environment.SAMPLEGAME_CONTENT__RUNTIMEMODE'
    Assert-ClosedKeys $Step.result @('stepPath','stepSha256','invocationId') 'outer.result'
    foreach ($key in @('stepPath','stepSha256','invocationId')) { Assert-UnityType $Step.result[$key] 'string' "outer.result.$key" }
    Assert-ClosedKeys $Step.observedAssemblies @('path','sha256') 'outer.observedAssemblies'
    foreach ($key in @('path','sha256')) { Assert-UnityType $Step.observedAssemblies[$key] 'string' "outer.observedAssemblies.$key" }
    Assert-UnityType $Step.payloads 'array' 'outer.payloads'
    foreach ($item in $Step.payloads) {
        Assert-ClosedKeys $item @('role','path','sha256') 'outer.payloads[]'
        foreach ($key in @('role','path','sha256')) { Assert-UnityType $item[$key] 'string' "outer.payloads[].$key" }
    }
}
function Assert-RawUnityTypes([object]$Raw) {
    Assert-ClosedKeys $Raw @('schemaVersion','recordKind','invocationId','name','kind','status','exitCode','failure','argv','cwd','projectPath','platform','filter','withGraphics','unity','process','durationMs','timing','counts','cases','compileErrors','logs','logHash','observation') 'raw step'
    foreach ($key in @('recordKind','invocationId','name','kind','status','cwd','projectPath','platform','filter','logHash')) { Assert-UnityType $Raw[$key] 'string' "raw.$key" }
    foreach ($key in @('schemaVersion','exitCode','durationMs')) { Assert-UnityType $Raw[$key] 'integer' "raw.$key" }
    Assert-UnityType $Raw.withGraphics 'boolean' 'raw.withGraphics'
    foreach ($key in @('failure','argv','compileErrors','logs')) { Assert-UnityStringArray $Raw[$key] "raw.$key" }
    Assert-ClosedKeys $Raw.unity @('executablePath','requestedVersion','executableVersion') 'raw.unity'
    foreach ($key in @('executablePath','requestedVersion','executableVersion')) { Assert-UnityType $Raw.unity[$key] 'string' "raw.unity.$key" }
    Assert-ClosedKeys $Raw.process @('startedAt','endedAt','exitCode','id') 'raw.process'
    foreach ($key in @('startedAt','endedAt')) { Assert-UnityType $Raw.process[$key] 'string' "raw.process.$key" }
    foreach ($key in @('exitCode','id')) { Assert-UnityType $Raw.process[$key] 'integer' "raw.process.$key" }
    Assert-ClosedKeys $Raw.timing @('processDurationMs','xmlDurationSeconds','xmlStartedAt','xmlEndedAt','unityMarkers','loadedAssemblies') 'raw.timing'
    Assert-UnityType $Raw.timing.processDurationMs 'integer' 'raw.timing.processDurationMs'
    Assert-UnityType $Raw.timing.xmlDurationSeconds 'nullable-number' 'raw.timing.xmlDurationSeconds'
    foreach ($key in @('xmlStartedAt','xmlEndedAt')) { Assert-UnityType $Raw.timing[$key] 'nullable-string' "raw.timing.$key" }
    Assert-ClosedKeys $Raw.timing.unityMarkers @('status','clock','path') 'raw.timing.unityMarkers'
    foreach ($key in @('status','path')) { Assert-UnityType $Raw.timing.unityMarkers[$key] 'string' "raw.timing.unityMarkers.$key" }
    $clock = $Raw.timing.unityMarkers.clock
    Assert-ClosedKeys $clock @('status','frequency','runStartedTicks','runFinishedTicks','runStartedUtc','runFinishedUtc','durationMs') 'raw.timing.unityMarkers.clock'
    foreach ($key in @('status','runStartedUtc','runFinishedUtc')) { Assert-UnityType $clock[$key] 'string' "raw.timing.unityMarkers.clock.$key" }
    foreach ($key in @('frequency','runStartedTicks','runFinishedTicks')) { Assert-UnityType $clock[$key] 'integer' "raw.timing.unityMarkers.clock.$key" }
    Assert-UnityType $clock.durationMs 'number' 'raw.timing.unityMarkers.clock.durationMs'
    Assert-ClosedKeys $Raw.timing.loadedAssemblies @('status','assemblies','path') 'raw.timing.loadedAssemblies'
    foreach ($key in @('status','path')) { Assert-UnityType $Raw.timing.loadedAssemblies[$key] 'string' "raw.timing.loadedAssemblies.$key" }
    Assert-UnityType $Raw.timing.loadedAssemblies.assemblies 'array' 'raw.timing.loadedAssemblies.assemblies'
    foreach ($item in $Raw.timing.loadedAssemblies.assemblies) {
        Assert-ClosedKeys $item @('domainOrdinal','observedAtUtc','ticks','fullName','location','loadedModuleVersionId','diskSha256','status','failure') 'raw.timing.loadedAssemblies.assemblies[]'
        foreach ($key in @('domainOrdinal','ticks')) { Assert-UnityType $item[$key] 'integer' "raw.timing.loadedAssemblies.assemblies[].$key" }
        foreach ($key in @('observedAtUtc','fullName','location','loadedModuleVersionId','diskSha256','status','failure')) { Assert-UnityType $item[$key] 'string' "raw.timing.loadedAssemblies.assemblies[].$key" }
    }
    Assert-ClosedKeys $Raw.counts @('total','passed','failed','skipped','inconclusive','executed') 'raw.counts'
    foreach ($key in @('total','passed','failed','skipped','inconclusive','executed')) { Assert-UnityType $Raw.counts[$key] 'integer' "raw.counts.$key" }
    Assert-UnityType $Raw.cases 'array' 'raw.cases'
    foreach ($item in $Raw.cases) {
        Assert-ClosedKeys $item @('id','name','fullname','result','label','reason') 'raw.cases[]'
        foreach ($key in @('id','name','fullname','result','label','reason')) { Assert-UnityType $item[$key] 'string' "raw.cases[].$key" }
    }
    Assert-ClosedKeys $Raw.observation @('required','path','sha256','progressPath','progressSha256','status','failure') 'raw.observation'
    Assert-UnityType $Raw.observation.required 'boolean' 'raw.observation.required'
    foreach ($key in @('path','sha256','progressPath','progressSha256','status')) { Assert-UnityType $Raw.observation[$key] 'string' "raw.observation.$key" }
    Assert-UnityStringArray $Raw.observation.failure 'raw.observation.failure'
}
function Assert-PayloadPath([string]$TaskDirectory, [string]$RunId, [string]$Relative) {
    if ([IO.Path]::IsPathRooted($Relative) -or $Relative -match '(^|[\\/])\.\.([\\/]|$)') { throw 'payload pathがtask外です。' }
    $root = [IO.Path]::GetFullPath([IO.Path]::Combine($TaskDirectory,'payload',$RunId))
    $path = [IO.Path]::GetFullPath([IO.Path]::Combine($TaskDirectory,$Relative))
    if (-not $path.StartsWith(($root + [IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase) -or -not [IO.File]::Exists($path)) { throw "payload pathが欠落またはrun外です: $Relative" }
    # Junction/symlinkで別payloadへ抜ける場合、文字列上のprefixだけでは不十分。
    $taskRoot = [IO.Path]::GetFullPath($TaskDirectory)
    $node = [IO.Path]::GetDirectoryName($path)
    while ($node.StartsWith($taskRoot,[StringComparison]::OrdinalIgnoreCase)) {
        if (([IO.File]::GetAttributes($node) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'payload祖先がreparse pointです。' }
        if ($node -ceq $taskRoot) { break }
        $node = [IO.Path]::GetDirectoryName($node)
    }
    if (([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'payload fileがreparse pointです。' }
    return $path
}

function Invoke-UnityChild([string]$Repo, [string[]]$Arguments, [string]$EnvironmentValue) {
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = 'pwsh'; $info.WorkingDirectory = $Repo; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $info.Environment['SAMPLEGAME_CONTENT__RUNTIMEMODE'] = $EnvironmentValue
    foreach ($argument in $Arguments) { [void]$info.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    $clock = [Diagnostics.Stopwatch]::StartNew()
    try {
        if (-not $process.Start()) { throw 'Unity runner子processを起動できません。' }
        # 両pipeを終了待ちより先に排水する。Unity正常進行には固定timeoutを置かない。
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        [Threading.Tasks.Task]::WaitAll(@($stdout,$stderr))
        $clock.Stop()
        return [pscustomobject]@{ ExitCode=$process.ExitCode; Stdout=$stdout.Result; Stderr=$stderr.Result; WallMs=$clock.ElapsedMilliseconds; OutputComplete=$true }
    } finally { $process.Dispose() }
}

function Get-UnityMarker([string]$Stdout, [string]$Stderr, [string]$UnityRoot) {
    $prefix = 'UNITY_TEST_RESULT '
    if (@($Stderr -split "`r?`n" | Where-Object { $_.StartsWith($prefix,[StringComparison]::Ordinal) }).Count -gt 0) { throw 'stderrに結果markerがあります。' }
    $markers = @($Stdout -split "`n" | ForEach-Object { $_.TrimEnd("`r") } | Where-Object { $_.StartsWith($prefix,[StringComparison]::Ordinal) })
    if ($markers.Count -ne 1) { throw "結果markerは一件だけ必要です: $($markers.Count)" }
    $path = $markers[0].Substring($prefix.Length)
    if ([string]::IsNullOrEmpty($path) -or -not [IO.Path]::IsPathFullyQualified($path)) { throw '結果markerのpathが空か絶対pathではありません。' }
    $root = [IO.Path]::GetFullPath($UnityRoot)
    $full = [IO.Path]::GetFullPath($path)
    if (-not $full.StartsWith(($root + [IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($full) -cne 'step.json') { throw '結果markerがこのrunのstep.jsonを指していません。' }
    $guid = [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($full))
    if ($guid -cnotmatch '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$') { throw '結果markerのGUID directoryが不正です。' }
    if ([IO.Path]::GetDirectoryName($full) -cne [IO.Path]::Combine($root,$guid)) { throw '結果markerが直接GUID子directoryを指していません。' }
    if (-not [IO.File]::Exists($full)) { throw '結果markerのstep.jsonがありません。' }
    $node=[IO.Path]::GetDirectoryName($full)
    while($node.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){
        if(([IO.File]::GetAttributes($node) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '結果markerの祖先がreparse pointです。'}
        if($node -ceq $root){break}
        $node=[IO.Path]::GetDirectoryName($node)
    }
    if(([IO.File]::GetAttributes($full) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw '結果markerのstep.jsonがreparse pointです。'}
    return $full
}

function Assert-UnityPayload([string]$Repo, [string]$TaskDirectory, [string]$RunId, [object]$Step, [object]$Spec) {
    # 保存済みの生ファイルだけを読む唯一の再検査入口。processや後日のLibrary DLLには触れない。
    $stepMap = ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $Step -Depth 64) -AsHashtable -DateKind String -Depth 64
    Assert-OuterUnityTypes $stepMap
    foreach ($forbidden in @('binaryPath','loadedPath','loadedHash','registered','selected','executed')) { if ($stepMap.Contains($forbidden)) { throw "Unity stepにoffline専用$forbidden があります。" } }
    if ($Step.adapterKind -cne 'unity-test-v2' -or $Step.name -cnotin @('unity-editmode-limited','unity-editmode-full') -or $Step.kind -cne 'test' -or $Step.status -cne 'passed' -or $Step.exitCode -ne 0 -or @($Step.failure).Count -ne 0) { throw 'Unity outer stepが成功形ではありません。' }
    $profile = if ($Step.name -ceq 'unity-editmode-limited') { 'limited-v1' } else { 'full-v1' }
    if ($Step.profile -cne $profile -or $Step.cwd -cne $Repo -or $Step.durationMs -lt 0) { throw 'Unity profile/cwd/durationが一致しません。' }
    Assert-Integer $Step.exitCode 'outer exitCode'
    Assert-Integer $Step.durationMs 'outer durationMs'
    Assert-Integer $Step.runner.exitCode 'runner exitCode'
    Assert-Integer $Step.timing.runnerWallMs 'runnerWallMs'
    Assert-Integer $Step.timing.collectionMs 'collectionMs'
    if ($Step.runner.exitCode -ne 0 -or $Step.timing.runnerWallMs -lt 0 -or $Step.timing.collectionMs -lt 0 -or $Step.runner.environment.SAMPLEGAME_CONTENT__RUNTIMEMODE -cne 'addressables') { throw 'runner終了/時間/envが不正です。' }
    $expectedArgs = @('-NoProfile','-File',(Join-Path $Repo 'tools/run-tests.ps1'),'-ObserveUnity','-OutputRoot',[IO.Path]::Combine($TaskDirectory,'payload',$RunId,'unity'))
    if ($profile -ceq 'limited-v1') { $expectedArgs += @('-Filter',$Spec.profile.limitedFilter) }
    if (@($Step.argv).Count -ne 1 -or $Step.argv[0].executable -cne 'pwsh' -or (ConvertTo-Json -InputObject @($Step.argv[0].arguments) -Compress) -cne (ConvertTo-Json -InputObject $expectedArgs -Compress)) { throw 'runner argvが承認profileと一致しません。' }
    $roles = @('runner-stdout','runner-stderr','runner-step','xml','unity-log','progress','observation')
    $base = [IO.Path]::Combine('payload',$RunId)
    $files = @{}
    foreach ($item in @($Step.payloads)) {
        $map = ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $item -Compress) -AsHashtable
        Assert-ClosedKeys $map @('role','path','sha256') 'payloads[]'
        if ($roles -cnotcontains $item.role -and $item.role -cne 'reload-settings') { throw '未知payload roleです。' }
        if ($files.ContainsKey([string]$item.role)) { throw 'payload role重複です。' }
        $path = Assert-PayloadPath $TaskDirectory $RunId $item.path
        if ((Get-RawSha $path) -cne $item.sha256) { throw "payload hash不一致: $($item.role)" }
        if (@($files.Values | Where-Object { $_ -ceq $path }).Count -gt 0) { throw 'payload path重複です。' }
        $files[[string]$item.role] = $path
    }
    foreach ($role in $roles) { if (-not $files.ContainsKey($role)) { throw "必須payload欠落: $role" } }
    $guid = [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($files['runner-step']))
    if ($guid -cne $Step.result.invocationId -or $files['runner-step'] -cne (Join-Path (Split-Path $files['runner-step']) 'step.json')) { throw 'raw invocationId/path不一致です。' }
    $expectedNames = @('runner.stdout.log','runner.stderr.log',"unity/$guid/step.json","unity/$guid/results.xml","unity/$guid/unity.log","unity/$guid/observation-progress.json","unity/$guid/observation.json")
    $expectedRoles = $roles
    if ($files.ContainsKey('reload-settings')) { $expectedNames += "unity/$guid/reload-settings.json"; $expectedRoles += 'reload-settings' }
    for ($i=0; $i -lt $expectedNames.Count; $i++) {
        $relative = [IO.Path]::Combine($base,($expectedNames[$i].Replace('/',[IO.Path]::DirectorySeparatorChar)))
        if ($Step.logs[$i] -cne $relative -or $Step.payloads[$i].role -cne $expectedRoles[$i] -or $Step.payloads[$i].path -cne $relative) { throw 'Unity payloadのpath/順序が不正です。' }
    }
    if (@($Step.logs).Count -ne $expectedNames.Count -or @($Step.payloads).Count -ne $expectedNames.Count) { throw 'Unity payload件数が不正です。' }
    $hashes = @($Step.logs | ForEach-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath (Assert-PayloadPath $TaskDirectory $RunId $_)).Hash })
    $logHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashes -join '|')))).ToLowerInvariant()
    if ($logHash -cne $Step.logHash) { throw 'Unity outer logHash不一致です。' }
    if ($Step.runner.stdoutPath -cne $Step.payloads[0].path -or $Step.runner.stdoutSha256 -cne $Step.payloads[0].sha256 -or $Step.runner.stderrPath -cne $Step.payloads[1].path -or $Step.runner.stderrSha256 -cne $Step.payloads[1].sha256 -or $Step.result.stepPath -cne $Step.payloads[2].path -or $Step.result.stepSha256 -cne $Step.payloads[2].sha256 -or $Step.observedAssemblies.path -cne $Step.payloads[6].path -or $Step.observedAssemblies.sha256 -cne $Step.payloads[6].sha256) { throw 'transport参照がpayloadと一致しません。' }
    $stdoutText = [IO.File]::ReadAllText($files['runner-stdout']); $stderrText = [IO.File]::ReadAllText($files['runner-stderr'])
    if ((Get-UnityMarker $stdoutText $stderrText ([IO.Path]::Combine($TaskDirectory,$base,'unity'))) -cne $files['runner-step']) { throw '保存済みmarkerがraw stepと違います。' }
    $raw = ConvertFrom-Json -InputObject ([IO.File]::ReadAllText($files['runner-step'])) -AsHashtable -DateKind String -Depth 64
    Assert-RawUnityTypes $raw
    foreach ($key in @('schemaVersion','exitCode','durationMs')) { Assert-Integer $raw[$key] "raw.$key" }
    Assert-Integer $raw.process.exitCode 'raw.process.exitCode'
    Assert-Integer $raw.process.id 'raw.process.id' 1
    Assert-Integer $raw.timing.processDurationMs 'raw.processDurationMs'
    foreach ($key in @('total','passed','failed','skipped','inconclusive','executed')) { Assert-Integer $raw.counts[$key] "raw.counts.$key" }
    if ($raw.schemaVersion -isnot [long] -and $raw.schemaVersion -isnot [int]) { throw 'raw schemaVersion型が不正です。' }
    if ($raw.schemaVersion -ne 2 -or $raw.recordKind -cne 'unity-test-step' -or $raw.invocationId -cne $guid -or $raw.name -cne 'unity-tests' -or $raw.kind -cne 'test' -or $raw.status -cne 'passed' -or $raw.exitCode -ne 0 -or @($raw.failure).Count -ne 0 -or $raw.process.exitCode -ne 0 -or $raw.process.id -le 0 -or $raw.platform -cne 'EditMode' -or $raw.withGraphics -isnot [bool] -or $raw.withGraphics -or $raw.unity.requestedVersion -cne '6000.6.0f1' -or $raw.cwd -cne $Repo -or $raw.projectPath -cne (Join-Path $Repo 'unity')) { throw 'raw schema/profile/identityが不正です。' }
    if (-not $raw.process.startedAt -or -not $raw.process.endedAt -or $raw.durationMs -lt 0 -or $raw.timing.processDurationMs -lt 0 -or -not $raw.unity.executableVersion) { throw 'raw process/timing/version欠測です。' }
    $versionExe = [IO.Path]::GetFullPath([IO.Path]::Combine('D:\UnityEditor','6000.6.0f1','Editor','Unity.exe'))
    if ($raw.unity.executablePath -cne $versionExe) { throw '標準runner解決のUnity executableではありません。' }
    $expectedFilter = if ($profile -ceq 'limited-v1') { $Spec.profile.limitedFilter } else { '' }
    if ($raw.filter -cne $expectedFilter -or $raw.observation.required -isnot [bool] -or -not $raw.observation.required) { throw 'raw filter/ObserveUnity不一致です。' }
    if ($profile -ceq 'full-v1' -and $raw.argv -ccontains '-testFilter') { throw 'fullのargvに-testFilterがあります。' }
    if ($profile -ceq 'limited-v1' -and ($raw.argv -cnotcontains '-testFilter' -or $raw.argv[[array]::IndexOf([array]$raw.argv,'-testFilter')+1] -cne $expectedFilter)) { throw 'limitedのfilter argv不一致です。' }
    $expectedRawArgs=@('-nographics','-batchmode','-projectPath',(Join-Path $Repo 'unity'),'-runTests','-testPlatform','EditMode','-testResults',$files['xml'],'-logFile',$files['unity-log'])
    if ($profile -ceq 'limited-v1') { $expectedRawArgs+=@('-testFilter',$expectedFilter) }
    # expected path は検証済みrun内payloadと標準runnerの生成規則から作る。raw argv 自己申告をanchorにしない。
    $expectedRawArgs+=@('-osmTestInvocation',$guid,'-osmTestObservation',(Get-UnityObservationArgumentPath $files['observation']))
    if ((ConvertTo-Json -InputObject @($raw.argv) -Compress) -cne (ConvertTo-Json -InputObject $expectedRawArgs -Compress)) { throw 'raw Unity argvが承認profileと不一致です。' }
    foreach ($entry in @(@('results.xml','xml'),@('unity.log','unity-log'),@('observation-progress.json','progress'),@('observation.json','observation'))) {
        $filename=$entry[0]; $role=$entry[1]
        if ($files[$role] -cne (Join-Path (Split-Path $files['runner-step']) $filename)) { throw "raw $role path不一致です。" }
    }
    if ($raw.observation.path -cne $files['observation'] -or $raw.observation.progressPath -cne $files['progress'] -or $raw.observation.sha256 -cne $Step.payloads[6].sha256 -or $raw.observation.progressSha256 -cne $Step.payloads[5].sha256) { throw 'raw observation path/hash不一致です。' }
    $rawLogNames=@('results.xml','unity.log','observation-progress.json','observation.json')
    if ((ConvertTo-Json -InputObject @($raw.logs) -Compress) -cne (ConvertTo-Json -InputObject $rawLogNames -Compress)) { throw 'raw logs順序不一致です。' }
    $rawHashes=@($rawLogNames | ForEach-Object { (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path (Split-Path $files['runner-step']) $_)).Hash })
    $rawLogHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($rawHashes -join '|')))).ToLowerInvariant()
    if ($rawLogHash -cne $raw.logHash) { throw 'raw logHash不一致です。' }
    $xml=[IO.File]::ReadAllText($files['xml']); $log=[IO.File]::ReadAllText($files['unity-log']); $progress=[IO.File]::ReadAllText($files['progress']); $observationText=[IO.File]::ReadAllText($files['observation'])
    $test=Get-UnityTestResult -XmlText $xml -LogText $log -ProcessExitCode $raw.process.exitCode
    $observed=Get-UnityObservationResult -XmlText $xml -ObservationText $observationText -ProgressText $progress -LogText $log -ExpectedInvocation $guid -ExpectedProject (Join-Path $Repo 'unity') -ExpectedPid ([int]$raw.process.id) -ExpectedPath $files['observation']
    if ($test.status -cne 'passed' -or $observed.status -cne 'complete' -or $raw.observation.status -cne $observed.status -or
        $raw.observation.failure -isnot [array] -or
        (ConvertTo-Json -InputObject @($raw.observation.failure) -Compress) -cne (ConvertTo-Json -InputObject @($observed.failure) -Compress)) {
        throw 'raw XML/観測とraw observation summaryが一致しません。'
    }
    foreach ($key in @('counts','cases','compileErrors')) {
        if ((ConvertTo-Json -InputObject $raw[$key] -Depth 32 -Compress) -cne (ConvertTo-Json -InputObject $test.$key -Depth 32 -Compress)) { throw "raw $key とXML再計算が不一致です。" }
    }
    foreach ($pair in @(@('xmlDurationSeconds','xmlDurationSeconds'),@('xmlStartedAt','xmlStartedAt'),@('xmlEndedAt','xmlEndedAt'))) { if ($raw.timing[$pair[0]] -cne $test.($pair[1])) { throw 'raw XML timing不一致です。' } }
    if ($raw.timing.unityMarkers.status -cne 'observed' -or $raw.timing.loadedAssemblies.status -cne 'observed' -or $raw.timing.unityMarkers.path -cne $files['observation'] -or $raw.timing.loadedAssemblies.path -cne $files['observation']) { throw 'raw観測要約が不正です。' }
    if ((ConvertTo-Json -InputObject $raw.timing.unityMarkers.clock -Depth 32 -Compress) -cne (ConvertTo-Json -InputObject $observed.clock -Depth 32 -Compress) -or (ConvertTo-Json -InputObject $raw.timing.loadedAssemblies.assemblies -Depth 32 -Compress) -cne (ConvertTo-Json -InputObject $observed.assemblies -Depth 32 -Compress)) { throw 'raw clock/assemblyが観測原本と不一致です。' }
    $terminal=ConvertFrom-Json -InputObject $observationText -AsHashtable -Depth 64
    Assert-UnityCaseSet $profile @($raw.cases) $raw.counts $terminal
    return $true
}

function Invoke-UnityStep([string]$Repo, [string]$TaskDirectory, [string]$RunId, [string]$Name, [object]$Spec) {
    $clock=[Diagnostics.Stopwatch]::StartNew(); $collection=[Diagnostics.Stopwatch]::new()
    $payload=[IO.Path]::Combine($TaskDirectory,'payload',$RunId); $unity=[IO.Path]::Combine($payload,'unity')
    [IO.Directory]::CreateDirectory($unity) | Out-Null
    if (([IO.File]::GetAttributes($unity) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Unity raw保存先がreparse pointです。' }
    if (@([IO.Directory]::EnumerateFileSystemEntries($unity)).Count -ne 0) { throw 'Unity raw保存先は新規runの空directoryが必要です。' }
    $args=@('-NoProfile','-File',(Join-Path $Repo 'tools/run-tests.ps1'),'-ObserveUnity','-OutputRoot',$unity)
    $profile=if($Name -ceq 'unity-editmode-limited'){'limited-v1'}elseif($Name -ceq 'unity-editmode-full'){'full-v1'}else{throw '未知Unity stepです。'}
    if($profile -ceq 'limited-v1'){$args+=@('-Filter',$Spec.profile.limitedFilter)}
    $paths=[Collections.Generic.List[string]]::new(); $roles=[Collections.Generic.List[string]]::new(); $failure=[Collections.Generic.List[string]]::new()
    $runner=$null; $result=$null; $observed=$null; $exit=$null
    try {
        $runner=Invoke-UnityChild $Repo $args 'addressables'; $exit=$runner.ExitCode
        $collection.Start()
        foreach($entry in @(@('runner-stdout','runner.stdout.log',$runner.Stdout),@('runner-stderr','runner.stderr.log',$runner.Stderr))){
            $file=Join-Path $payload $entry[1]; [IO.File]::WriteAllText($file,[string]$entry[2]); $roles.Add($entry[0]); $paths.Add($file)
        }
        $collection.Stop()
        if(-not $runner.OutputComplete){throw 'runner両streamの排水が完了しませんでした。'}
        $stepPath=Get-UnityMarker $runner.Stdout $runner.Stderr $unity
        $guid=[IO.Path]::GetFileName([IO.Path]::GetDirectoryName($stepPath))
        foreach($entry in @(@('runner-step','step.json'),@('xml','results.xml'),@('unity-log','unity.log'),@('progress','observation-progress.json'),@('observation','observation.json'),@('reload-settings','reload-settings.json'))){
            $file=Join-Path (Split-Path $stepPath) $entry[1]
            if([IO.File]::Exists($file)){
                if(([IO.File]::GetAttributes($file) -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw "raw fileがreparse pointです: $($entry[1])"}
                $roles.Add($entry[0]); $paths.Add($file)
            }
        }
        $result=[ordered]@{stepPath=(Get-RelativePayload $TaskDirectory $stepPath);stepSha256=(Get-RawSha $stepPath);invocationId=$guid}
        if($exit -ne 0){throw "runner exit=$exit"}
    } catch { $failure.Add($_.Exception.Message) }
    $clock.Stop(); $collection.Stop()
    $items=[Collections.Generic.List[object]]::new()
    for($i=0;$i -lt $paths.Count;$i++){ $items.Add([ordered]@{role=$roles[$i];path=(Get-RelativePayload $TaskDirectory $paths[$i]);sha256=(Get-RawSha $paths[$i])}) }
    $logs=@($items | ForEach-Object {$_.path}); $hashes=@($paths | ForEach-Object {(Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash})
    $logHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes(($hashes -join '|')))).ToLowerInvariant()
    $stdoutItem=@($items | Where-Object role -eq 'runner-stdout' | Select-Object -First 1); $stderrItem=@($items | Where-Object role -eq 'runner-stderr' | Select-Object -First 1)
    $obsItem=@($items | Where-Object role -eq 'observation' | Select-Object -First 1)
    $step=[ordered]@{name=$Name;kind='test';adapterKind='unity-test-v2';status='failed';exitCode=$exit;failure=@($failure);argv=@([ordered]@{executable='pwsh';arguments=@($args)});cwd=$Repo;durationMs=$clock.ElapsedMilliseconds;timing=[ordered]@{runnerWallMs=$(if($runner){$runner.WallMs}else{$null});collectionMs=$collection.ElapsedMilliseconds};logs=$logs;logHash=$logHash;profile=$profile;runner=[ordered]@{exitCode=$exit;stdoutPath=$(if($stdoutItem){$stdoutItem[0].path}else{$null});stdoutSha256=$(if($stdoutItem){$stdoutItem[0].sha256}else{$null});stderrPath=$(if($stderrItem){$stderrItem[0].path}else{$null});stderrSha256=$(if($stderrItem){$stderrItem[0].sha256}else{$null});environment=[ordered]@{SAMPLEGAME_CONTENT__RUNTIMEMODE='addressables'}};result=$result;payloads=@($items);observedAssemblies=$(if($obsItem){[ordered]@{path=$obsItem[0].path;sha256=$obsItem[0].sha256}}else{$null})}
    if($failure.Count -eq 0){
        $step.status='passed'; $step.exitCode=0
        try { [void](Assert-UnityPayload $Repo $TaskDirectory $RunId ([pscustomobject]$step) $Spec) }
        catch { $step.status='failed'; $step.exitCode=1; $step.failure=@($_.Exception.Message) }
    }
    return [pscustomobject]$step
}

Export-ModuleMember -Function Invoke-UnityStep,Assert-UnityPayload
