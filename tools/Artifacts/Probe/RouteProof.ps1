param(
    [string] $Endpoint,
    [switch] $Child,
    [ValidateSet('put','authenticated-get','unsigned-get','delete')][string] $Operation,
    [string] $Key,
    [string] $ExpectedHash,
    [int] $ExpectedBytes,
    [string] $PayloadBase64,
    [string] $RunId,
    [string] $LockRuleJson,
    [switch] $Library,
    [switch] $RunLoop
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:ObservationProperties = @(
    'Operation','HttpStatus','StatusClass','S3Code','ByteCount','BodySha256','PrefixSha256',
    'EofConfirmed','LimitReached','TimedOut','Redirected','Generation',
    'RequestMethod','RequestUri','AuthorizationPresent','SignatureQueryPresent','TargetChangingQueryPresent'
)

# 同じproduction loopをofflineで検査できるよう、時間・子process・通信の境界を差し替えます。
$script:RouteProofClock = $null
$script:RouteProofChildRunner = $null
$script:RouteProofTransport = $null

function Test-Endpoint([string] $Value) { return $Value -cmatch '^https://[0-9a-f]{32}\.r2\.cloudflarestorage\.com$' }
function Test-Hash([string] $Value) { return $Value -cmatch '^[0-9a-f]{64}$' }
function Test-RunId([string] $Value) { return $Value -cmatch '^[0-9a-f]{32}$' }
function Test-Key([string] $Value, [string] $ExpectedRunId = '') {
    # childごとにrun-idとkeyの対応を検証し、別runへの誤書込みを拒否します。
    return $Value -cmatch '^probe/(unlocked|locked)/[0-9a-f]{32}/[A-Za-z0-9._-]{1,64}$' -and
        ([string]::IsNullOrEmpty($ExpectedRunId) -or $Value -match ('^probe/(unlocked|locked)/' + [regex]::Escape($ExpectedRunId) + '/')) -and
        $Value.IndexOfAny([char[]]([char]0..[char]31 + [char]127)) -lt 0
}

function Get-RouteElapsed {
    if ($null -ne $script:RouteProofClock) { return [TimeSpan](& $script:RouteProofClock) }
    return $script:RunClock.Elapsed
}

function Start-RouteClock {
    if ($null -ne $script:RouteProofClock) { return }
    $script:RunClock = [Diagnostics.Stopwatch]::StartNew()
}

function Invoke-RouteDelay([int] $Milliseconds) {
    if ($null -ne $script:RouteProofClock) { return }
    Start-Sleep -Milliseconds $Milliseconds
}

function Invoke-RouteProofLoop([string] $EndpointValue, [string] $RunIdValue, [string] $RuleJson) {
    $script:ActiveEndpoint = $EndpointValue
    $lockRule = Read-LockRule $RuleJson
    Start-RouteClock
    $unlockedKey = "probe/unlocked/$RunIdValue/object.txt"
    $lockedKey = "probe/locked/$RunIdValue/object.txt"
    $original = [Text.Encoding]::ASCII.GetBytes("OSM-ROUTE-PROOF:${RunIdValue}:original")
    $changed = [Text.Encoding]::ASCII.GetBytes("OSM-ROUTE-PROOF:${RunIdValue}:changed")
    $originalHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($original)).ToLowerInvariant()
    $changedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($changed)).ToLowerInvariant()
    $records = [Collections.Generic.List[object]]::new()
    $classification = 'pass'; $unlockedPut = $false; $unlockedRemoved = $false; $lockedPut = $false; $writerGeneration = $null
    $steps = @(
        @{ Op='put'; Key=$unlockedKey; Bytes=$original; Hash=$originalHash; Phase='unlocked-put' },
        @{ Op='authenticated-get'; Key=$unlockedKey; Bytes=$original; Hash=$originalHash; Phase='unlocked-authenticated-get' },
        @{ Op='unsigned-get'; Key=$unlockedKey; Bytes=$original; Hash=$originalHash; Phase='unsigned-get' },
        @{ Op='put'; Key=$unlockedKey; Bytes=$changed; Hash=$changedHash; Phase='unlocked-overwrite' },
        @{ Op='authenticated-get'; Key=$unlockedKey; Bytes=$changed; Hash=$changedHash; Phase='unlocked-overwrite-get' },
        @{ Op='delete'; Key=$unlockedKey; Bytes=$changed; Hash=$changedHash; Phase='unlocked-delete' },
        @{ Op='authenticated-get'; Key=$unlockedKey; Bytes=$changed; Hash=$changedHash; Phase='unlocked-delete-confirm' },
        @{ Op='put'; Key=$lockedKey; Bytes=$original; Hash=$originalHash; Phase='locked-put' },
        @{ Op='authenticated-get'; Key=$lockedKey; Bytes=$original; Hash=$originalHash; Phase='locked-authenticated-get' },
        @{ Op='put'; Key=$lockedKey; Bytes=$changed; Hash=$changedHash; Phase='locked-overwrite' },
        @{ Op='delete'; Key=$lockedKey; Bytes=$original; Hash=$originalHash; Phase='locked-delete' },
        @{ Op='authenticated-get'; Key=$lockedKey; Bytes=$original; Hash=$originalHash; Phase='locked-confirm' }
    )
    foreach ($step in $steps) {
        if ((Get-RouteElapsed) -ge [TimeSpan]::FromMinutes(5)) { $classification = 'inconclusive'; break }
        $payload = if ($step.Op -eq 'put') { [Convert]::ToBase64String($step.Bytes) } else { $null }
        $script:ActiveKey = $step.Key
        $observation = Invoke-ChildOperation $EndpointValue $step.Op $step.Key $step.Hash $step.Bytes.Length $payload $RunIdValue
        if ($null -ne $observation -and $step.Phase -eq 'unlocked-overwrite' -and $observation.StatusClass -eq 'too-many-requests') {
            Invoke-RouteDelay 1000
            $observation = Invoke-ChildOperation $EndpointValue $step.Op $step.Key $step.Hash $step.Bytes.Length $payload $RunIdValue
        } elseif ($null -ne $observation -and $step.Phase -in @('locked-overwrite','locked-delete') -and $observation.StatusClass -eq 'success') {
            Invoke-RouteDelay 1000
            $observation = Invoke-ChildOperation $EndpointValue $step.Op $step.Key $step.Hash $step.Bytes.Length $payload $RunIdValue
        }
        $passed = $false
        if ($null -ne $observation) {
            if ($step.Phase -eq 'unsigned-get') { $passed = Test-UnsignedPrivacy $observation $step.Hash $step.Bytes.Length }
            elseif ($step.Phase -in @('locked-overwrite','locked-delete')) { $passed = Test-LockRejection $observation }
            elseif ($step.Phase -eq 'unlocked-delete-confirm') { $passed = $observation.StatusClass -eq 'not-found' -and $observation.S3Code -ceq 'NoSuchKey' }
            elseif ($step.Op -eq 'put') { $passed = $observation.StatusClass -eq 'success' -and $observation.HttpStatus -eq 200 }
            elseif ($step.Op -eq 'delete') { $passed = $observation.StatusClass -eq 'success' -and $observation.HttpStatus -eq 204 }
            else { $passed = Test-AuthenticatedMatch $observation $step.Hash $step.Bytes.Length $writerGeneration }
        }
        if ($passed -and $null -ne $writerGeneration -and $observation.Generation -cne $writerGeneration) {
            $passed = $false; $classification = 'inconclusive'
        } elseif ($passed -and $null -eq $writerGeneration) { $writerGeneration = $observation.Generation }
        if ($step.Phase -eq 'unlocked-put' -and $passed) { $unlockedPut = $true }
        if ($step.Phase -eq 'unlocked-delete' -and $passed) { $unlockedRemoved = $true }
        if ($step.Phase -eq 'locked-put' -and $passed) { $lockedPut = $true }
        if (-not $passed) {
            if ($step.Phase -eq 'unsigned-get' -and $null -ne $observation -and $observation.PrefixSha256 -ceq $step.Hash) { $classification = 'provider-capability-failure' }
            elseif (($step.Phase -like 'locked-*') -and $lockedPut -and $null -ne $observation -and $observation.StatusClass -eq 'success') { $classification = 'provider-capability-failure' }
            else { $classification = 'inconclusive' }
            $records.Add((New-ObservationRecord $classification $step.Phase $step.Op $step.Key $step.Hash $step.Bytes.Length $observation $(if ($step.Phase -like 'locked-*' -and $lockedPut) { 'retained-by-lock' } else { 'unconfirmed' }) $lockRule $script:Base $script:Head))
            break
        }
        $cleanup = if ($step.Phase -eq 'unlocked-delete') { 'removed' } elseif ($step.Phase -like 'locked-*') { 'retained-by-lock' } else { 'not-needed' }
        $records.Add((New-ObservationRecord 'pass' $step.Phase $step.Op $step.Key $step.Hash $step.Bytes.Length $observation $cleanup $lockRule $script:Base $script:Head))
    }
    if ($classification -eq 'pass' -and $records.Count -eq 12) {
        $records.Add((New-ObservationRecord 'pass' 'complete' 'route-proof' $null $null 0 $null $(if ($unlockedRemoved) { 'removed; locked object retained' } else { 'unconfirmed' }) $lockRule $script:Base $script:Head))
    } else {
        if ($unlockedPut -and -not $unlockedRemoved) {
            # PUT応答が曖昧でもkeyは存在し得るため、結果失敗後に同じrun内で清掃を試します。
            $script:ActiveKey = $unlockedKey
            $cleanupObservation = Invoke-ChildOperation $EndpointValue 'delete' $unlockedKey $changedHash $changed.Length $null $RunIdValue
            if ($null -eq $cleanupObservation -or $cleanupObservation.StatusClass -ne 'success') { $classification = 'inconclusive' }
        }
        $records.Add((New-ObservationRecord $classification 'complete' 'route-proof' $null $null 0 $null $(if ($lockedPut) { 'retained-by-lock' } else { 'unconfirmed' }) $lockRule $script:Base $script:Head))
    }
    [Array]::Clear($original, 0, $original.Length); [Array]::Clear($changed, 0, $changed.Length)
    $exit = switch ($classification) { 'pass' { 0 } 'provider-capability-failure' { 2 } 'environment-blocked' { 3 } default { 4 } }
    return [pscustomobject]@{ Records = $records; ExitCode = $exit }
}

function Get-StatusClass([int] $Status) {
    if ($Status -ge 200 -and $Status -le 299) { return 'success' }
    if ($Status -in @(301,302,303,307,308)) { return 'redirect' }
    if ($Status -eq 401) { return 'unauthorized' }
    if ($Status -eq 403) { return 'forbidden' }
    if ($Status -eq 404) { return 'not-found' }
    if ($Status -eq 429) { return 'too-many-requests' }
    if ($Status -ge 500 -and $Status -le 599) { return 'server-error' }
    return 'other'
}

function Assert-Observation($Observation) {
    $names = @($Observation.PSObject.Properties.Name)
    $actualNames = (($names | Sort-Object) -join ',')
    $expectedNames = (($script:ObservationProperties | Sort-Object) -join ',')
    if ($actualNames -cne $expectedNames) { throw 'Invalid transport observation.' }
    if ($Observation.Operation -notin @('put','authenticated-get','unsigned-get','delete') -or
        $Observation.StatusClass -notin @('success','unauthorized','forbidden','not-found','too-many-requests','server-error','redirect','other','timeout','transport-error','invalid-input') -or
        $Observation.Generation -notmatch '^[0-9a-f]{32}$') { throw 'Invalid transport observation.' }
    foreach ($name in @('EofConfirmed','LimitReached','TimedOut','Redirected')) {
        if ($Observation.$name -isnot [bool]) { throw 'Invalid transport observation.' }
    }
    foreach ($name in @('AuthorizationPresent','SignatureQueryPresent','TargetChangingQueryPresent')) {
        if ($Observation.$name -isnot [bool]) { throw 'Invalid transport observation.' }
    }
    if ($null -ne $Observation.RequestMethod -and $Observation.RequestMethod -isnot [string]) { throw 'Invalid transport observation.' }
    if ($null -ne $Observation.RequestUri -and $Observation.RequestUri -isnot [string]) { throw 'Invalid transport observation.' }
    if ($null -ne $Observation.HttpStatus -and (($Observation.HttpStatus -isnot [int] -and $Observation.HttpStatus -isnot [long]) -or $Observation.HttpStatus -lt 100 -or $Observation.HttpStatus -gt 599)) { throw 'Invalid transport observation.' }
    if ($null -ne $Observation.ByteCount -and (($Observation.ByteCount -isnot [int] -and $Observation.ByteCount -isnot [long]) -or $Observation.ByteCount -lt 0 -or $Observation.ByteCount -gt 8193)) { throw 'Invalid transport observation.' }
    foreach ($name in @('BodySha256','PrefixSha256')) {
        if ($null -ne $Observation.$name -and $Observation.$name -cnotmatch '^[0-9a-f]{64}$') { throw 'Invalid transport observation.' }
    }
    if ($null -ne $Observation.S3Code -and $Observation.S3Code -cnotmatch '^[A-Za-z][A-Za-z0-9]{0,63}$') { throw 'Invalid transport observation.' }
}

function Test-AuthenticatedMatch($Observation, [string] $Hash, [int] $Bytes, [string] $ExpectedGeneration = $null) {
    try { Assert-Observation $Observation } catch { return $false }
    return $Observation.StatusClass -eq 'success' -and $Observation.HttpStatus -eq 200 -and
        $Observation.EofConfirmed -and -not $Observation.LimitReached -and
        $Observation.ByteCount -eq $Bytes -and $Observation.BodySha256 -ceq $Hash -and
        $Observation.Generation -match '^[0-9a-f]{32}$' -and
        ($null -eq $ExpectedGeneration -or $Observation.Generation -ceq $ExpectedGeneration)
}

function Test-UnsignedPrivacy($Observation, [string] $Hash, [int] $Bytes) {
    try { Assert-Observation $Observation } catch { return $false }
    # 拒否statusだけを信用せず、非秘密の観測が意図したGET生成経路と一致するか確認します。
    $segments = @($script:ActiveKey -split '/' | ForEach-Object { [uri]::EscapeDataString($_) })
    $expectedUri = ([uri]($script:ActiveEndpoint + '/osm-artifacts/' + ($segments -join '/'))).AbsoluteUri
    if ($Observation.RequestMethod -cne 'GET' -or $Observation.RequestUri -cne $expectedUri -or
        $Observation.AuthorizationPresent -or $Observation.SignatureQueryPresent -or $Observation.TargetChangingQueryPresent) { return $false }
    # このrevisionでは401/Unauthorizedと403/AccessDeniedだけを許し、400 InvalidArgumentは常にinconclusiveです。
    return $Observation.HttpStatus -in @(401,403) -and $Observation.StatusClass -in @('unauthorized','forbidden') -and
        $Observation.S3Code -in @('Unauthorized','AccessDenied') -and $Observation.EofConfirmed -and
        -not $Observation.LimitReached -and -not $Observation.Redirected -and
        $null -ne $Observation.BodySha256 -and $Observation.BodySha256 -cne $Hash -and
        ($null -eq $Observation.PrefixSha256 -or $Observation.PrefixSha256 -cne $Hash)
}

function Test-LockRejection($Observation) {
    try { Assert-Observation $Observation } catch { return $false }
    return $Observation.HttpStatus -eq 403 -and $Observation.StatusClass -eq 'forbidden' -and
        $Observation.S3Code -ceq 'ObjectLockedByBucketPolicy'
}

function New-ObservationRecord(
    [string] $Result,
    [string] $Phase,
    [string] $OperationName,
    [string] $KeyValue,
    [string] $ExpectedHashValue,
    [int] $ExpectedBytesValue,
    $Observation,
    [string] $Cleanup,
    $LockRule,
    [string] $Base,
    [string] $Head) {
    [pscustomobject][ordered]@{
        result = $Result; phase = $Phase; operation = $OperationName
        key = $KeyValue; expectedHash = $ExpectedHashValue
        observedHash = if ($null -eq $Observation) { $null } else { $Observation.BodySha256 }
        expectedBytes = $ExpectedBytesValue
        observedBytes = if ($null -eq $Observation) { $null } else { $Observation.ByteCount }
        prefixHash = if ($null -eq $Observation) { $null } else { $Observation.PrefixSha256 }
        eofConfirmed = if ($null -eq $Observation) { $false } else { [bool]$Observation.EofConfirmed }
        limitReached = if ($null -eq $Observation) { $false } else { [bool]$Observation.LimitReached }
        httpStatus = if ($null -eq $Observation) { $null } else { $Observation.HttpStatus }
        statusClass = if ($null -eq $Observation) { $null } else { $Observation.StatusClass }
        s3Code = if ($null -eq $Observation) { $null } else { $Observation.S3Code }
        generation = if ($null -eq $Observation) { $null } else { $Observation.Generation }
        lockRule = $LockRule; executedUtc = [DateTimeOffset]::UtcNow.ToString('o')
        cleanup = $Cleanup; base = $Base; head = $Head
    }
}

function Invoke-ChildOperation(
    [string] $EndpointValue,
    [string] $OperationName,
    [string] $KeyValue,
    [string] $ExpectedHashValue,
    [int] $Bytes,
    [string] $Payload,
    [string] $ExpectedRunId) {
    $elapsed = Get-RouteElapsed
    if ($elapsed -ge [TimeSpan]::FromMinutes(5)) { return $null }
    # 45秒の子上限と残り5分予算の短い方を、起動からpipe回収まで共有します。
    $remaining = [Math]::Min(45000, [int]([TimeSpan]::FromMinutes(5) - $elapsed).TotalMilliseconds)
    if ($null -ne $script:RouteProofChildRunner) {
        return & $script:RouteProofChildRunner $EndpointValue $OperationName $KeyValue $ExpectedHashValue $Bytes $Payload $ExpectedRunId $remaining
    }
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = 'pwsh'; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile','-File',$PSCommandPath,'-Endpoint',$EndpointValue,'-Child','-Operation',$OperationName,'-Key',$KeyValue,'-ExpectedHash',$ExpectedHashValue,'-ExpectedBytes',[string]$Bytes,'-RunId',$ExpectedRunId)) { [void]$info.ArgumentList.Add($argument) }
    if ($Payload) { [void]$info.ArgumentList.Add('-PayloadBase64'); [void]$info.ArgumentList.Add($Payload) }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    try {
        if (-not $process.Start()) { return $null }
        # pipeを先に並行drainし、子終了後のReadToEndによる無期限待ちを避けます。
        # 大きなpipe出力でも子を止めないよう並行drainし、終了後の同期ReadToEndは避けます。
        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($remaining)) {
            try { $process.Kill($true) } catch { try { $process.Kill() } catch { } }
            try { $process.WaitForExit(5000) } catch { }
            return $null
        }
        $remainingForPipes = [Math]::Max(0, [Math]::Min(5000, $remaining - [int](Get-RouteElapsed).TotalMilliseconds))
        if (-not $stdoutTask.Wait($remainingForPipes) -or -not $stderrTask.Wait($remainingForPipes)) {
            try { $process.Kill($true) } catch { try { $process.Kill() } catch { } }
            return $null
        }
        $remainingForPipes = [Math]::Max(0, [Math]::Min(5000, $remaining - [int](Get-RouteElapsed).TotalMilliseconds))
        if (-not $stdoutTask.Wait($remainingForPipes) -or -not $stderrTask.Wait($remainingForPipes)) {
            try { $process.Kill($true) } catch { try { $process.Kill() } catch { } }
            return $null
        }
        $remainingForPipes = [Math]::Max(0, [Math]::Min(5000, $remaining - [int](Get-RouteElapsed).TotalMilliseconds))
        if (-not $stdoutTask.Wait($remainingForPipes) -or -not $stderrTask.Wait($remainingForPipes)) {
            try { $process.Kill($true) } catch { try { $process.Kill() } catch { } }
            return $null
        }
        $stdout = $stdoutTask.GetAwaiter().GetResult().Trim()
        $stderr = $stderrTask.GetAwaiter().GetResult().Trim()
        if ($stderr -or [string]::IsNullOrWhiteSpace($stdout)) { return $null }
        $lines = @($stdout -split "`r?`n" | Where-Object { $_ -ne '' })
        if ($lines.Count -ne 1) { return $null }
        $observation = $lines[0] | ConvertFrom-Json
        Assert-Observation $observation
        return $observation
    } catch { return $null } finally { $process.Dispose() }
}

function Read-LockRule([string] $Json) {
    if ([string]::IsNullOrWhiteSpace($Json)) { throw 'Lock rule record unavailable.' }
    $rule = $Json | ConvertFrom-Json
    $names = @($rule.PSObject.Properties.Name)
    $expected = @('Prefix','Enabled','Kind','RetentionSeconds','RuleCount','DateRules','IndefiniteRules','WriterCanConfigure','LifecycleCompatible','BeforeHash','AfterHash')
    if ((($names | Sort-Object) -join ',') -cne (($expected | Sort-Object) -join ',') -or
        $rule.Prefix -isnot [string] -or $rule.Prefix -cne 'probe/locked/' -or $rule.Enabled -isnot [bool] -or -not $rule.Enabled -or
        $rule.Kind -isnot [string] -or $rule.Kind -cne 'Age' -or
        (($rule.RetentionSeconds -isnot [int]) -and ($rule.RetentionSeconds -isnot [long])) -or $rule.RetentionSeconds -lt 900 -or $rule.RetentionSeconds -gt 86400 -or
        (($rule.RuleCount -isnot [int]) -and ($rule.RuleCount -isnot [long])) -or $rule.RuleCount -ne 1 -or
        (($rule.DateRules -isnot [int]) -and ($rule.DateRules -isnot [long])) -or $rule.DateRules -ne 0 -or
        (($rule.IndefiniteRules -isnot [int]) -and ($rule.IndefiniteRules -isnot [long])) -or $rule.IndefiniteRules -ne 0 -or
        $rule.WriterCanConfigure -isnot [bool] -or $rule.WriterCanConfigure -or
        $rule.LifecycleCompatible -isnot [bool] -or -not $rule.LifecycleCompatible -or
        $rule.BeforeHash -isnot [string] -or $rule.AfterHash -isnot [string] -or
        $rule.BeforeHash -cnotmatch '^[0-9a-f]{64}$' -or $rule.AfterHash -cne $rule.BeforeHash) {
        throw 'Lock rule record unavailable.'
    }
    return [pscustomobject][ordered]@{
        prefix = $rule.Prefix; enabled = [bool]$rule.Enabled; kind = $rule.Kind
        retentionSeconds = [int]$rule.RetentionSeconds; ruleCount = [int]$rule.RuleCount
        dateRules = [int]$rule.DateRules; indefiniteRules = [int]$rule.IndefiniteRules
        writerCanConfigure = [bool]$rule.WriterCanConfigure; lifecycleCompatible = [bool]$rule.LifecycleCompatible
        beforeHash = $rule.BeforeHash; afterHash = $rule.AfterHash
    }
}

if ($Library) { return }

$base = try { (git merge-base develop HEAD 2>$null).Trim() } catch { $null }
$head = try { (git rev-parse HEAD 2>$null).Trim() } catch { $null }
$script:Base = $base; $script:Head = $head

if ($RunLoop) {
    # テストも実運用と同じ12操作ループを通し、同じ分類とcleanupを確認します。
    try {
        if (-not (Test-Endpoint $Endpoint) -or -not (Test-RunId $RunId)) { throw 'Invalid input.' }
        $result = Invoke-RouteProofLoop $Endpoint $RunId $LockRuleJson
        foreach ($record in $result.Records) { [Console]::WriteLine(($record | ConvertTo-Json -Compress -Depth 6)) }
        exit $result.ExitCode
    } catch {
        $record = New-ObservationRecord 'environment-blocked' 'input' 'input' $null $null 0 $null 'not-needed' $null $base $head
        [Console]::WriteLine(($record | ConvertTo-Json -Compress -Depth 6)); exit 3
    }
}

if ($Child) {
    try {
        if (-not (Test-Endpoint $Endpoint) -or $Operation -notin @('put','authenticated-get','unsigned-get','delete') -or
            -not (Test-RunId $RunId) -or -not (Test-Key $Key $RunId) -or -not (Test-Hash $ExpectedHash) -or $ExpectedBytes -lt 1 -or $ExpectedBytes -gt 1024) { throw 'Invalid input.' }
        $transportModule = Import-Module (Join-Path $PSScriptRoot 'R2RouteTransport.psm1') -Force -PassThru
        $payload = if ($PayloadBase64) { [Convert]::FromBase64String($PayloadBase64) } else { $null }
        $request = @{
            Endpoint = $Endpoint; Operation = $Operation; Key = $Key; Payload = $payload
            ExpectedBytes = $ExpectedBytes; DeadlineMilliseconds = 30000
        }
        $callback = { param($id, $secret, $generation, $requestValue) Invoke-R2RouteTransport $id $secret $generation $requestValue }
        $storeModule = Import-Module (Join-Path $PSScriptRoot '../Credentials/CredentialStore.psm1') -Force -PassThru
        if ($null -ne $script:RouteProofTransport) { $observation = & $script:RouteProofTransport $Operation $request }
        else { $observation = Invoke-CredentialTransport 'osm' $callback $request }
        Assert-Observation $observation
        [Console]::WriteLine(($observation | ConvertTo-Json -Compress -Depth 4))
        exit 0
    } catch {
        $fallback = [pscustomobject][ordered]@{
            Operation = if ($Operation) { $Operation } else { 'input' }; HttpStatus = $null; StatusClass = 'inconclusive'
            S3Code = $null; ByteCount = $null; BodySha256 = $null; PrefixSha256 = $null
            EofConfirmed = $false; LimitReached = $false; TimedOut = $false; Redirected = $false; Generation = '00000000000000000000000000000000'
            RequestMethod = $null; RequestUri = $null; AuthorizationPresent = $false
            SignatureQueryPresent = $false; TargetChangingQueryPresent = $false
        }
        [Console]::WriteLine(($fallback | ConvertTo-Json -Compress -Depth 4)); exit 4
    }
}

try {
    if (-not (Test-Endpoint $Endpoint)) { throw 'Invalid endpoint.' }
    if ($RunId) { if (-not (Test-RunId $RunId)) { throw 'Invalid run id.' } } else { $RunId = [Guid]::NewGuid().ToString('N') }
    $script:Base = $base; $script:Head = $head
    $result = Invoke-RouteProofLoop $Endpoint $RunId $LockRuleJson
    foreach ($record in $result.Records) { [Console]::WriteLine(($record | ConvertTo-Json -Compress -Depth 6)) }
    exit $result.ExitCode
} catch {
    $record = New-ObservationRecord 'environment-blocked' 'input' 'input' $null $null 0 $null 'not-needed' $null $base $head
    [Console]::WriteLine(($record | ConvertTo-Json -Compress -Depth 6)); exit 3
}
