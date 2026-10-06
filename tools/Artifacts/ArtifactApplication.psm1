Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactProtectionPolicy.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactProtection.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Transport/R2ArtifactTransport.psm1') -Force

$script:PackageAssembly = Join-Path $PSScriptRoot 'Packaging/artifacts/package/ArtifactPackaging.dll'
$script:ReadbackChildPath = Join-Path $PSScriptRoot 'Transport/ArtifactReadback.ps1'
# Private test hooks are not exported and are unavailable through the public CLI.
$script:TransportHook = $null
$script:ReadbackHook = $null

function Import-ArtifactPackage {
    if (-not [IO.File]::Exists($script:PackageAssembly)) { throw 'Artifact package unavailable.' }
    $assembly = [Reflection.Assembly]::LoadFrom($script:PackageAssembly)
    if ($null -eq $assembly.GetType('OneStarMaker.Artifacts.Packaging.PackageIO', $true)) {
        throw 'Artifact package unavailable.'
    }
}

function Get-Remaining([Diagnostics.Stopwatch] $Timer, [int] $Maximum = 120000) {
    $remaining = [Math]::Min($Maximum, 600000 - [int]$Timer.ElapsedMilliseconds)
    if ($remaining -lt 1) { throw 'Artifact operation expired.' }
    return $remaining
}

function Invoke-ArtifactNetwork([string] $Generation, [hashtable] $Request) {
    $requestCopy = $Request.Clone()
    if ($script:TransportHook) { return & $script:TransportHook $Generation $requestCopy }
    $transportInvoker = (Get-Command Invoke-R2ArtifactOperation -ErrorAction Stop).ScriptBlock
    $callback = {
        param($id, $secret, $actualGeneration, $details)
        if ($actualGeneration -cne $Generation) { throw 'Credential generation changed.' }
        & $transportInvoker $id $secret $actualGeneration $details
    }.GetNewClosure()
    return Invoke-CredentialTransport 'osm' $callback $requestCopy
}

function Test-ArtifactSuccess($Result, [string] $Operation, [string] $Generation) {
    return $null -ne $Result -and $Result.Operation -ceq $Operation -and
        $Result.Generation -ceq $Generation -and $Result.StatusClass -ceq 'success' -and
        $Result.HttpStatus -ge 200 -and $Result.HttpStatus -le 299 -and
        $Result.StatusObserved -eq $true -and $Result.TimedOut -eq $false -and $Result.Redirected -eq $false
}

function Test-ArtifactLock($Result, [string] $Operation, [string] $Generation) {
    return $null -ne $Result -and $Result.Operation -ceq $Operation -and
        $Result.Generation -ceq $Generation -and $Result.StatusObserved -eq $true -and
        $Result.TimedOut -eq $false -and $Result.Redirected -eq $false -and
        $Result.S3Code -ceq 'ObjectLockedByBucketPolicy' -and
        (($Result.HttpStatus -eq 403 -and $Result.StatusClass -ceq 'forbidden') -or
         ($Result.HttpStatus -eq 409 -and $Result.StatusClass -ceq 'other'))
}

function Test-ArtifactMissing($Result, [string] $Generation) {
    return $null -ne $Result -and $Result.Operation -ceq 'get' -and
        $Result.Generation -ceq $Generation -and $Result.HttpStatus -eq 404 -and
        $Result.StatusClass -ceq 'not-found' -and $Result.S3Code -ceq 'NoSuchKey' -and
        $Result.StatusObserved -eq $true -and -not $Result.TimedOut -and -not $Result.Redirected
}

function Test-ArtifactReadback($Result, [string] $Generation, [string] $Key, [long] $Bytes, [string] $Hash) {
    return $null -ne $Result -and $Result.operation -ceq 'readback' -and
        $Result.generation -ceq $Generation -and $Result.key -ceq $Key -and
        $Result.bytes -eq $Bytes -and $Result.sha256 -ceq $Hash -and
        $Result.childExit -eq 0 -and $Result.childStatus -ceq 'passed'
}

function New-ArtifactRequest($Config, [string] $Operation, [string] $Key,
    [string] $Source, [string] $Destination, [Diagnostics.Stopwatch] $Timer) {
    return @{
        Endpoint = $Config.Data.endpoint; Operation = $Operation; Key = $Key
        SourcePath = $Source; DestinationPath = $Destination
        DeadlineMilliseconds = (Get-Remaining $Timer)
    }
}

function Write-ArtifactJson([string] $Path, $Value) {
    $json = ConvertTo-Json -InputObject $Value -Compress -Depth 20
    [IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($json))).ToLowerInvariant()
}

function New-ArtifactResult([string] $Id, [string] $Operation) {
    return [ordered]@{
        schemaVersion = 1; operationId = $Id; operation = $Operation; status = 'failed'
        reasonCode = 'unconfirmed'; verification = [ordered]@{}
        ledger = $null; outputPath = $null; residue = [ordered]@{ localPath = $null; remoteKeys = @(); retainUntil = $null; cleanup = 'unknown'; remote = 'unknown' }
    }
}

function Invoke-ArtifactObservedNetwork([Collections.Generic.List[object]] $Records, [string] $Phase,
    [string] $Generation, [hashtable] $Request, [scriptblock] $Action,
    [Nullable[long]] $ExpectedBytes = $null, [string] $ExpectedSha256 = '') {
    $record = [ordered]@{
        phase = $Phase; operation = $Request.Operation; generation = $Generation; key = $Request.Key
        expectedBytes = $ExpectedBytes; expectedSha256 = if ($ExpectedSha256) { $ExpectedSha256 } else { $null }
        startedAt = [DateTimeOffset]::UtcNow.ToString('o'); completedAt = $null; result = 'unconfirmed'
        httpStatus = $null; statusClass = $null; s3Code = $null; bytes = $null; sha256 = $null
        statusObserved = $false; timedOut = $false; redirected = $false
    }
    $Records.Add($record)
    try {
        $observed = & $Action
        # A fixed whitelist keeps SDK values, exception text and secrets out of the raw record.
        if ($null -ne $observed -and $observed.Operation -ceq $Request.Operation -and
            $observed.Generation -ceq $Generation -and
            $observed.StatusClass -cin @('success','unauthorized','forbidden','not-found','too-many-requests','server-error','redirect','other','timeout','transport-error','invalid-input','size-limit') -and
            ($null -eq $observed.HttpStatus -or ($observed.HttpStatus -is [int] -and $observed.HttpStatus -ge 100 -and $observed.HttpStatus -le 599)) -and
            ($null -eq $observed.S3Code -or ($observed.S3Code -is [string] -and $observed.S3Code -cmatch '\A[A-Za-z][A-Za-z0-9]{0,63}\z')) -and
            ($null -eq $observed.Bytes -or ($observed.Bytes -is [long] -and $observed.Bytes -ge 0 -and $observed.Bytes -le 256MB)) -and
            ($null -eq $observed.Sha256 -or $observed.Sha256 -ceq '' -or
                ($observed.Sha256 -is [string] -and $observed.Sha256 -cmatch '\A[0-9a-f]{64}\z')) -and
            $observed.StatusObserved -is [bool] -and $observed.TimedOut -is [bool] -and $observed.Redirected -is [bool]) {
            $record.result = 'returned'; $record.httpStatus = $observed.HttpStatus
            $record.statusClass = $observed.StatusClass; $record.s3Code = $observed.S3Code
            $record.bytes = $observed.Bytes
            $record.sha256 = if ($observed.Sha256 -ceq '') { $null } else { $observed.Sha256 }
            $record.statusObserved = $observed.StatusObserved; $record.timedOut = $observed.TimedOut
            $record.redirected = $observed.Redirected
        }
        return $observed
    } finally { $record.completedAt = [DateTimeOffset]::UtcNow.ToString('o') }
}

function Invoke-ArtifactObservedReadback([Collections.Generic.List[object]] $Records, [string] $Phase,
    [string] $Generation, [string] $Key, [long] $Bytes, [string] $Hash, [scriptblock] $Action) {
    $record = [ordered]@{
        phase = $Phase; operation = 'readback'; generation = $Generation; key = $Key
        expectedBytes = $Bytes; expectedSha256 = $Hash
        startedAt = [DateTimeOffset]::UtcNow.ToString('o'); completedAt = $null; result = 'unconfirmed'
        httpStatus = $null; statusClass = $null; s3Code = $null
        statusObserved = $false; timedOut = $false; redirected = $false
        bytes = $null; sha256 = $null; childExit = $null; childStatus = $null; childObservedAt = $null
    }
    $Records.Add($record)
    try {
        $observed = & $Action
        if ($null -ne $observed -and $observed.operation -ceq 'readback' -and
            $observed.generation -ceq $Generation -and $observed.key -ceq $Key -and
            ($null -eq $observed.bytes -or (($observed.bytes -is [int] -or $observed.bytes -is [long]) -and $observed.bytes -ge 0 -and $observed.bytes -le 256MB)) -and
            ($null -eq $observed.sha256 -or ($observed.sha256 -is [string] -and $observed.sha256 -cmatch '\A[0-9a-f]{64}\z')) -and
            $observed.childExit -is [int] -and $observed.childStatus -cin @('passed','failed','unconfirmed')) {
            $record.result = 'returned'; $record.bytes = if ($null -eq $observed.bytes) { $null } else { [long]$observed.bytes }
            $record.sha256 = $observed.sha256
            $record.childExit = $observed.childExit; $record.childStatus = $observed.childStatus
            if ($null -ne $observed.PSObject.Properties['httpStatus'] -and
                ($null -eq $observed.httpStatus -or ($observed.httpStatus -is [int] -and $observed.httpStatus -ge 100 -and $observed.httpStatus -le 599)) -and
                $observed.statusClass -cin @('success','unauthorized','forbidden','not-found','too-many-requests','server-error','redirect','other','timeout','transport-error','invalid-input','size-limit') -and
                ($null -eq $observed.s3Code -or ($observed.s3Code -is [string] -and $observed.s3Code -cmatch '\A[A-Za-z][A-Za-z0-9]{0,63}\z')) -and
                $observed.statusObserved -is [bool] -and $observed.timedOut -is [bool] -and $observed.redirected -is [bool]) {
                $record.httpStatus = $observed.httpStatus; $record.statusClass = $observed.statusClass
                $record.s3Code = $observed.s3Code; $record.statusObserved = $observed.statusObserved
                $record.timedOut = $observed.timedOut; $record.redirected = $observed.redirected
            }
            if ($observed.observedAt -is [string] -and $observed.observedAt -cmatch '\A[0-9T:.+-]{20,40}\z') {
                $record.childObservedAt = $observed.observedAt
            }
        }
        return $observed
    } catch {
        if ($_.Exception.Message -ceq 'Readback child unconfirmed.') { $record.result = 'stop-unconfirmed' }
        elseif ($_.Exception.Message -ceq 'Readback pipes unconfirmed.') { $record.result = 'pipes-unconfirmed' }
        elseif ($_.Exception.Message -ceq 'Readback child expired.') { $record.result = 'expired-stopped' }
        throw
    } finally { $record.completedAt = [DateTimeOffset]::UtcNow.ToString('o') }
}

function Write-ArtifactObservationRecord([string] $OperationRoot, $Result, $Config,
    [Collections.Generic.List[object]] $Records, $Context) {
    $raw = [ordered]@{ schemaVersion = 1; operationId = $Result.operationId; operation = $Result.operation
        configSha256 = if ($Config) { $Config.Hash } else { $null }
        profileId = if ($Config) { $Config.ProfileId } else { $null }
        context = $Context
        completedAt = [DateTimeOffset]::UtcNow.ToString('o'); result = $Result.status
        observations = @($Records.ToArray()) }
    $null = Write-ArtifactJson ([IO.Path]::Combine($OperationRoot, 'operation-observations.json')) $raw
}

function Invoke-ReadbackStop([scriptblock] $Stop, [scriptblock] $WaitExit,
    [scriptblock] $WaitPipes, [scriptblock] $Clock, [scriptblock] $OperationRemaining) {
    # A single monotonic allowance covers both child termination and pipe closure.
    $started = [long](& $Clock)
    $limit = [Math]::Min(15000, [int](& $OperationRemaining))
    & $Stop
    if ($limit -lt 1) { throw 'Readback child unconfirmed.' }
    $remaining = {
        $elapsed = [long](& $Clock) - $started
        $milliseconds = [int][Math]::Max(0, [Math]::Floor($elapsed * 1000.0 / [Diagnostics.Stopwatch]::Frequency))
        [Math]::Min(($limit - $milliseconds), [int](& $OperationRemaining))
    }.GetNewClosure()
    $exitBudget = & $remaining
    if ($exitBudget -lt 1 -or -not (& $WaitExit $exitBudget)) { throw 'Readback child unconfirmed.' }
    $pipeBudget = & $remaining
    if ($pipeBudget -lt 1 -or -not (& $WaitPipes $pipeBudget)) { throw 'Readback pipes unconfirmed.' }
    throw 'Readback child expired.'
}

function Invoke-ArtifactReadback([string] $OperationRoot, $Config, [string] $Generation,
    [string] $Key, [long] $Bytes, [string] $Hash, [Diagnostics.Stopwatch] $Timer) {
    if ($script:ReadbackHook) { return & $script:ReadbackHook $OperationRoot $Config $Generation $Key $Bytes $Hash }
    $requestPath = [IO.Path]::Combine($OperationRoot, 'readback-' + [Guid]::NewGuid().ToString('N') + '.json')
    $destination = [IO.Path]::Combine($OperationRoot, 'readback-' + [Guid]::NewGuid().ToString('N') + '.bin')
    $request = [ordered]@{ schemaVersion = 1; operationRoot = $OperationRoot; endpoint = $Config.Data.endpoint
        generation = $Generation; key = $Key; expectedBytes = $Bytes; expectedSha256 = $Hash
        destination = $destination; deadlineMilliseconds = (Get-Remaining $Timer) }
    $null = Write-ArtifactJson $requestPath $request
    Protect-ArtifactTree $OperationRoot
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = 'pwsh'; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile','-File',$script:ReadbackChildPath,'-RequestPath',$requestPath)) {
        [void]$info.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    try {
        if (-not $process.Start()) { throw 'Readback child unavailable.' }
        $outTask = $process.StandardOutput.ReadToEndAsync()
        $errTask = $process.StandardError.ReadToEndAsync()
        # Reserve the final 15 seconds of the operation budget for stop and pipe verification.
        $wait = [Math]::Min(135000, [Math]::Max(0, 600000 - [int]$Timer.ElapsedMilliseconds - 15000))
        if (-not $process.WaitForExit($wait)) {
            $stop = { if (-not $process.HasExited) { $process.Kill($true) } }.GetNewClosure()
            $waitExit = { param($milliseconds) $process.WaitForExit($milliseconds) }.GetNewClosure()
            $waitPipes = { param($milliseconds) [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($outTask,$errTask), $milliseconds) }.GetNewClosure()
            $clock = { [Diagnostics.Stopwatch]::GetTimestamp() }
            $operationRemaining = { [Math]::Max(0, 600000 - [int]$Timer.ElapsedMilliseconds) }.GetNewClosure()
            Invoke-ReadbackStop $stop $waitExit $waitPipes $clock $operationRemaining
        }
        $pipeBudget = [Math]::Min(15000, (Get-Remaining $Timer 15000))
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($outTask,$errTask), $pipeBudget)) { throw 'Readback pipe unconfirmed.' }
        if ($errTask.Result.Length -ne 0 -or $outTask.Result.Length -gt 4096) {
            return [pscustomobject]@{ operation = 'readback'; generation = $Generation; key = $Key
                bytes = $null; sha256 = $null; childExit = $process.ExitCode
                childStatus = 'unconfirmed'; observedAt = [DateTimeOffset]::UtcNow.ToString('o') }
        }
        $lines = @($outTask.Result.Trim() -split "`r?`n" | Where-Object { $_ -ne '' })
        if ($lines.Count -ne 1) {
            if ($lines.Count -eq 0) {
                return [pscustomobject]@{ operation = 'readback'; generation = $Generation; key = $Key
                    bytes = $null; sha256 = $null; childExit = $process.ExitCode
                    childStatus = 'unconfirmed'; observedAt = [DateTimeOffset]::UtcNow.ToString('o') }
            }
            throw 'Readback output unavailable.'
        }
        $document = [Text.Json.JsonDocument]::Parse($lines[0])
        try { Assert-JsonDuplicates $document.RootElement } finally { $document.Dispose() }
        $value = $lines[0] | ConvertFrom-Json -Depth 8
        Assert-Fields $value @('schemaVersion','operation','generation','key','bytes','sha256',
            'httpStatus','statusClass','s3Code','statusObserved','timedOut','redirected','status')
        if ($value.schemaVersion -isnot [long] -or $value.schemaVersion -ne 1 -or
            $value.operation -isnot [string] -or $value.operation -cne 'get' -or
            $value.generation -isnot [string] -or $value.generation -cne $Generation -or
            $value.key -isnot [string] -or $value.key -cne $Key -or
            ($null -ne $value.bytes -and ($value.bytes -isnot [long] -or $value.bytes -lt 0 -or $value.bytes -gt 256MB)) -or
            ($null -ne $value.sha256 -and ($value.sha256 -isnot [string] -or $value.sha256 -cnotmatch '\A[0-9a-f]{64}\z')) -or
            ($null -ne $value.httpStatus -and ($value.httpStatus -isnot [long] -or $value.httpStatus -lt 100 -or $value.httpStatus -gt 599)) -or
            $value.statusClass -isnot [string] -or $value.statusClass -cnotin @('success','unauthorized','forbidden','not-found','too-many-requests','server-error','redirect','other','timeout','transport-error','invalid-input','size-limit') -or
            ($null -ne $value.s3Code -and ($value.s3Code -isnot [string] -or $value.s3Code -cnotmatch '\A[A-Za-z][A-Za-z0-9]{0,63}\z')) -or
            $value.statusObserved -isnot [bool] -or $value.timedOut -isnot [bool] -or $value.redirected -isnot [bool] -or
            $value.status -isnot [string] -or $value.status -cnotin @('passed','failed')) {
            throw 'Readback mismatch.'
        }
        $status = if ($process.ExitCode -eq 0 -and $value.status -ceq 'passed' -and
            $value.httpStatus -eq 200 -and $value.statusClass -ceq 'success' -and
            $value.statusObserved -and -not $value.timedOut -and -not $value.redirected -and
            $value.bytes -eq $Bytes -and $value.sha256 -ceq $Hash) { 'passed' } else { 'failed' }
        return [pscustomobject]@{ operation = 'readback'; generation = $Generation; key = $Key
            bytes = $value.bytes; sha256 = $value.sha256; childExit = $process.ExitCode
            httpStatus = if ($null -eq $value.httpStatus) { $null } else { [int]$value.httpStatus }
            statusClass = $value.statusClass; s3Code = $value.s3Code
            statusObserved = $value.statusObserved; timedOut = $value.timedOut; redirected = $value.redirected
            childStatus = $status; observedAt = [DateTimeOffset]::UtcNow.ToString('o') }
    } finally { $process.Dispose() }
}

function Invoke-ArtifactPublish([string] $ConfigPath, [string] $InputListPath, [string] $Base, [string] $Head) {
    $id = [Guid]::NewGuid().ToString('N'); $result = New-ArtifactResult $id 'publish'
    $timer = [Diagnostics.Stopwatch]::StartNew(); $operationRoot = $null; $config = $null
    $observations = [Collections.Generic.List[object]]::new()
    $context = [ordered]@{ base = $Base; head = $Head; runId = $null; key = $null
        packageBytes = $null; packageSha256 = $null; manifestSha256 = $null; putStartedAt = $null }
    try {
        Assert-Hex $Base 40; Assert-Hex $Head 40
        $config = Read-ArtifactConfig $ConfigPath
        $list = Read-ArtifactJson $InputListPath
        Assert-Fields $list.Data @('schemaVersion','purpose','root','files')
        $sourceRoot = [string]$list.Data.root
        $artifactRoot = Get-ArtifactRoot
        $credentialRoot = [IO.Path]::Combine([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData), 'OneStarMaker', 'Artifacts', 'credentials')
        if ((Test-ArtifactWithin $sourceRoot $artifactRoot) -or (Test-ArtifactWithin $sourceRoot $credentialRoot)) {
            throw 'Protected input root.'
        }
        $operationRoot = New-ArtifactOperation
        $result.residue.localPath = $operationRoot
        Import-ArtifactPackage
        $runId = [Guid]::NewGuid().ToString('N')
        $package = [OneStarMaker.Artifacts.Packaging.PackageIO]::Create($InputListPath, $operationRoot,
            $Base, $Head, $config.Data.repositoryId, $runId, (Get-Remaining $timer 600000))
        $context.runId = $runId; $context.packageBytes = $package.Bytes
        $context.packageSha256 = $package.Sha256; $context.manifestSha256 = $package.ManifestSha256
        Protect-ArtifactTree $operationRoot
        $result.verification.snapshot = $true
        $generation = (Get-CredentialStatus 'osm').Generation
        $key = "probe/locked/$($config.Data.repositoryId)/$Head/$runId/bundle.zip"
        $context.key = $key
        $result.residue.remoteKeys = @($key)
        $putStartedAt = [DateTimeOffset]::UtcNow
        $context.putStartedAt = $putStartedAt.ToString('o')
        $retainUntil = $putStartedAt.AddSeconds(86400).ToString('o')
        $result.residue.retainUntil = $retainUntil
        $result.residue.remote = 'put-attempted-state-unknown'
        $request = New-ArtifactRequest $config 'put' $key $package.Path $null $timer
        $put = Invoke-ArtifactObservedNetwork $observations 'locked-put' $generation $request { Invoke-ArtifactNetwork $generation $request } $package.Bytes $package.Sha256
        if (-not (Test-ArtifactSuccess $put 'put' $generation)) { throw 'Publish PUT unconfirmed.' }
        $result.verification.put = $true
        $readback1 = Invoke-ArtifactObservedReadback $observations 'first-readback' $generation $key $package.Bytes $package.Sha256 { Invoke-ArtifactReadback $operationRoot $config $generation $key $package.Bytes $package.Sha256 $timer }
        if (-not (Test-ArtifactReadback $readback1 $generation $key $package.Bytes $package.Sha256)) { throw 'Readback unconfirmed.' }
        $result.verification.readback = $true
        $different = [IO.Path]::Combine($operationRoot, 'different.bin')
        $differentBytes = [Text.Encoding]::UTF8.GetBytes('synthetic-overwrite-' + $runId)
        [IO.File]::WriteAllBytes($different, $differentBytes)
        $differentHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($differentBytes)).ToLowerInvariant()
        Protect-ArtifactTree $operationRoot
        $request = New-ArtifactRequest $config 'put' $key $different $null $timer
        $overwrite = Invoke-ArtifactObservedNetwork $observations 'locked-overwrite' $generation $request { Invoke-ArtifactNetwork $generation $request } $differentBytes.Length $differentHash
        if (-not (Test-ArtifactLock $overwrite 'put' $generation)) { throw 'Overwrite protection unconfirmed.' }
        $request = New-ArtifactRequest $config 'delete' $key $null $null $timer
        $delete = Invoke-ArtifactObservedNetwork $observations 'locked-delete' $generation $request { Invoke-ArtifactNetwork $generation $request }
        if (-not (Test-ArtifactLock $delete 'delete' $generation)) { throw 'Delete protection unconfirmed.' }
        $readback2 = Invoke-ArtifactObservedReadback $observations 'final-readback' $generation $key $package.Bytes $package.Sha256 { Invoke-ArtifactReadback $operationRoot $config $generation $key $package.Bytes $package.Sha256 $timer }
        if (-not (Test-ArtifactReadback $readback2 $generation $key $package.Bytes $package.Sha256)) { throw 'Final readback unconfirmed.' }
        $result.verification.protection = $true
        $controlKey = 'probe/unlocked/' + [Guid]::NewGuid().ToString('N') + '/control.txt'
        $result.residue.remoteKeys = @($key,$controlKey)
        $controlPath = [IO.Path]::Combine($operationRoot, 'control.txt')
        $controlBytes = [Text.Encoding]::UTF8.GetBytes('synthetic-control-' + $runId)
        [IO.File]::WriteAllBytes($controlPath, $controlBytes)
        Protect-ArtifactTree $operationRoot
        $controlHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($controlBytes)).ToLowerInvariant()
        $request = New-ArtifactRequest $config 'put' $controlKey $controlPath $null $timer
        $controlPut = Invoke-ArtifactObservedNetwork $observations 'control-put' $generation $request { Invoke-ArtifactNetwork $generation $request } $controlBytes.Length $controlHash
        if (-not (Test-ArtifactSuccess $controlPut 'put' $generation)) { throw 'Control PUT unconfirmed.' }
        $request = New-ArtifactRequest $config 'get' $controlKey $null ([IO.Path]::Combine($operationRoot, 'control-get.bin')) $timer
        $controlGet = Invoke-ArtifactObservedNetwork $observations 'control-get' $generation $request { Invoke-ArtifactNetwork $generation $request } $controlBytes.Length $controlHash
        if (-not (Test-ArtifactSuccess $controlGet 'get' $generation) -or $controlGet.Bytes -ne $controlBytes.Length -or
            $controlGet.Sha256 -cne $controlHash) { throw 'Control GET unconfirmed.' }
        $request = New-ArtifactRequest $config 'delete' $controlKey $null $null $timer
        $controlDelete = Invoke-ArtifactObservedNetwork $observations 'control-delete' $generation $request { Invoke-ArtifactNetwork $generation $request }
        if (-not (Test-ArtifactSuccess $controlDelete 'delete' $generation)) { throw 'Control DELETE unconfirmed.' }
        $request = New-ArtifactRequest $config 'get' $controlKey $null ([IO.Path]::Combine($operationRoot, 'control-missing.bin')) $timer
        $missing = Invoke-ArtifactObservedNetwork $observations 'control-absence' $generation $request { Invoke-ArtifactNetwork $generation $request }
        if (-not (Test-ArtifactMissing $missing $generation)) { throw 'Control absence unconfirmed.' }
        $result.verification.control = $true
        # Bucket Lock age starts at the server's object creation, so use a local time before PUT.
        $receipt = [ordered]@{ schemaVersion = 1; operationId = $id; generation = $generation; configSha256 = $config.Hash
            profileId = $config.ProfileId; base = $Base; head = $Head; runId = $runId; key = $key
            packageBytes = $package.Bytes; packageSha256 = $package.Sha256; manifestSha256 = $package.ManifestSha256
            putStartedAt = $putStartedAt.ToString('o'); observations = @($observations.ToArray())
            completedAt = [DateTimeOffset]::UtcNow.ToString('o'); exit = 0 }
        $receiptPath = [IO.Path]::Combine($operationRoot, 'protection-receipt.json')
        $receiptHash = Write-ArtifactJson $receiptPath $receipt
        $reference = New-ArtifactReference $config $key $Base $Head $runId $package.Bytes $package.ManifestSha256 $retainUntil
        $result.ledger = [ordered]@{ reference = $reference; packageSha256 = $package.Sha256
            manifestSha256 = $package.ManifestSha256; base = $Base; head = $Head; packageBytes = $package.Bytes
            retainUntil = $retainUntil; protectionReceiptPath = $receiptPath; protectionReceiptSha256 = $receiptHash }
        $result.status = 'passed'; $result.reasonCode = 'complete'; $result.residue.remote = 'locked-retained'
        $result.residue.cleanup = 'local-retained'
    } catch { $result.reasonCode = 'unconfirmed'; $result.residue.cleanup = 'local-retained' }
    finally {
        if ($operationRoot) {
            try {
                Write-ArtifactObservationRecord $operationRoot $result $config $observations $context
                $null = Write-ArtifactJson ([IO.Path]::Combine($operationRoot, 'result.json')) $result
                Protect-ArtifactTree $operationRoot
            }
            catch { $result.status = 'failed'; $result.reasonCode = 'result-persistence-unconfirmed'; $result.ledger = $null; $result.outputPath = $null }
        }
    }
    return [pscustomobject]$result
}

function Invoke-ArtifactFetch([string] $ConfigPath, [string] $ReferenceText, [string] $ExpectedHash) {
    $id = [Guid]::NewGuid().ToString('N'); $result = New-ArtifactResult $id 'fetch'
    $timer = [Diagnostics.Stopwatch]::StartNew(); $operationRoot = $null; $config = $null
    $observations = [Collections.Generic.List[object]]::new()
    $context = [ordered]@{ base = $null; head = $null; runId = $null; key = $null
        expectedPackageBytes = $null; expectedPackageSha256 = $ExpectedHash }
    try {
        Assert-Hex $ExpectedHash 64
        $rawConfig = Read-ArtifactJson $ConfigPath
        $config = if ($null -ne $rawConfig.Data.PSObject.Properties['purpose'] -and $rawConfig.Data.purpose -ceq 'evidence') {
            Read-EvidenceConfig $ConfigPath
        } else { Read-ArtifactConfig $ConfigPath }
        $reference = Read-ArtifactReference $ReferenceText $config
        $context.base = $reference.base; $context.head = $reference.head
        $context.runId = $reference.runId; $context.key = $reference.key
        $context.expectedPackageBytes = $reference.packageBytes
        $operationRoot = New-ArtifactOperation
        $result.residue.localPath = $operationRoot
        Import-ArtifactPackage
        $generation = (Get-CredentialStatus 'osm').Generation
        $archive = [IO.Path]::Combine($operationRoot, 'download.zip')
        $request = New-ArtifactRequest $config 'get' $reference.key $null $archive $timer
        $download = Invoke-ArtifactObservedNetwork $observations 'download' $generation $request { Invoke-ArtifactNetwork $generation $request } $reference.packageBytes $ExpectedHash
        if (-not (Test-ArtifactSuccess $download 'get' $generation) -or $download.Bytes -ne $reference.packageBytes -or
            $download.Sha256 -cne $ExpectedHash) { throw 'Download unconfirmed.' }
        $result.verification.outerHash = $true
        Protect-ArtifactTree $operationRoot
        $package = [OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($archive, $operationRoot, $ExpectedHash,
            $reference.manifestSha256, $reference.base, $reference.head, $reference.repositoryId,
            $reference.runId, (Get-Remaining $timer 600000), $(if ($config.Data.prefix -ceq 'evidence/first-use/') {'evidence'} else {'synthetic'}))
        Protect-ArtifactTree $operationRoot
        $result.verification.entries = $true
        $result.outputPath = $package.Path
        $result.status = 'passed'; $result.reasonCode = 'complete'
        $result.residue.cleanup = 'local-retained'; $result.residue.remote = 'unchanged'
    } catch { $result.reasonCode = 'unconfirmed'; $result.residue.cleanup = 'local-retained' }
    finally {
        if ($operationRoot) {
            try {
                Write-ArtifactObservationRecord $operationRoot $result $config $observations $context
                $null = Write-ArtifactJson ([IO.Path]::Combine($operationRoot, 'result.json')) $result
                Protect-ArtifactTree $operationRoot
            }
            catch { $result.status = 'failed'; $result.reasonCode = 'result-persistence-unconfirmed'; $result.ledger = $null; $result.outputPath = $null }
        }
    }
    return [pscustomobject]$result
}

# This export is for EvidenceApplication only. The public CLI has no prepared-path grammar.
function Invoke-ArtifactPublishPrepared($Config,$Package,[string] $OperationRoot,[string] $OperationId,
    [string] $RunId,[string] $EvidenceBase,[string] $EvidenceHead,[string] $ProducerBase,[string] $ProducerHead,
    [string] $SourceManifestHash,[string] $SelectionReceiptHash,[object[]] $ExpectedFiles,
    [Diagnostics.Stopwatch] $Timer) {
    $result = New-ArtifactResult $OperationId 'evidence-publish'
    $result.residue.localPath = $OperationRoot
    $key = "evidence/first-use/$($Config.Data.repositoryId)/$EvidenceHead/$RunId/bundle.zip"
    $runPrefix = "evidence/first-use/$($Config.Data.repositoryId)/$EvidenceHead/$RunId/"
    $result.residue.remoteKeys = @($key,($runPrefix + 'protection-witness.txt'),('probe/unlocked/' + $OperationId + '/control.txt'))
    try {
        Assert-Hex $EvidenceBase 40; Assert-Hex $EvidenceHead 40
        Assert-Hex $ProducerBase 40; Assert-Hex $ProducerHead 40
        Assert-Hex $SourceManifestHash 64; Assert-Hex $SelectionReceiptHash 64
        if ($OperationId -cnotmatch '\A[0-9a-f]{32}\z' -or $RunId -cnotmatch '\A[0-9a-f]{32}\z' -or
            $Config.Data.purpose -cne 'evidence' -or $Package.Path -cne ([IO.Path]::Combine($OperationRoot,'bundle.zip'))) {
            throw 'Prepared package unavailable.'
        }
        Assert-ArtifactOperationFile $OperationRoot $Package.Path
        if ($Package.Bytes -lt 1 -or $Package.Bytes -gt 256MB -or
            ([IO.FileInfo]::new($Package.Path)).Length -ne $Package.Bytes -or
            (Get-FileHash -LiteralPath $Package.Path -Algorithm SHA256).Hash.ToLowerInvariant() -cne $Package.Sha256) {
            throw 'Prepared package changed.'
        }
        Import-ArtifactPackage
        $verify = [OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($Package.Path,$OperationRoot,$Package.Sha256,
            $Package.ManifestSha256,$EvidenceBase,$EvidenceHead,$Config.Data.repositoryId,$RunId,(Get-Remaining $Timer 600000),'evidence')
        if ($verify.Files.Length -ne $ExpectedFiles.Count) { throw 'Prepared entry count changed.' }
        foreach ($expected in $ExpectedFiles) {
            $actual = @($verify.Files | Where-Object { $_.Path -ceq $expected.Path })
            if ($actual.Count -ne 1 -or $actual[0].Bytes -ne $expected.Bytes -or $actual[0].Sha256 -cne $expected.Sha256) {
                throw 'Prepared entry changed.'
            }
        }
        Protect-ArtifactTree $OperationRoot
        $generation = (Get-CredentialStatus 'osm').Generation
        $networkInvoker = (Get-Command Invoke-ArtifactNetwork).ScriptBlock
        $readInvoker = (Get-Command Invoke-ArtifactReadback).ScriptBlock
        $remainingInvoker = (Get-Command Get-Remaining).ScriptBlock
        $send = { param($g,$request) & $networkInvoker $g $request }.GetNewClosure()
        $read = { param($readKey,$bytes,$hash)
            & $readInvoker $OperationRoot $Config $generation $readKey $bytes $hash $Timer
        }.GetNewClosure()
        $remaining = { & $remainingInvoker $Timer }.GetNewClosure()
        # Deny writers while verification and the one allowed upload use this exact ZIP.
        $archiveLock = [IO.FileStream]::new($Package.Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        try {
            $protection = Invoke-ArtifactEvidenceProtection $Config $Package $OperationRoot $OperationId $RunId `
                $EvidenceBase $EvidenceHead $ProducerBase $ProducerHead $generation $SourceManifestHash `
                $SelectionReceiptHash $send $read $remaining
        } finally { $archiveLock.Dispose() }
        $reference = New-ArtifactReference $Config $key $EvidenceBase $EvidenceHead $RunId $Package.Bytes `
            $Package.ManifestSha256 $protection.ServerLockLowerBound
        $result.ledger = [ordered]@{ reference=$reference; key=$key; packageBytes=$Package.Bytes
            packageSha256=$Package.Sha256; manifestSha256=$Package.ManifestSha256
            evidenceBase=$EvidenceBase; evidenceHead=$EvidenceHead; producerBase=$ProducerBase; producerHead=$ProducerHead
            runId=$RunId; sourceManifestSha256=$SourceManifestHash; selectionReceiptSha256=$SelectionReceiptHash
            entries=@($ExpectedFiles | ForEach-Object { [ordered]@{path=$_.Path;bytes=$_.Bytes;sha256=$_.Sha256} })
            protectionReceiptSha256=$protection.ReceiptSha256; protectionReceiptPath=$protection.ReceiptPath
            intentPath=$protection.IntentPath; intentSha256=$protection.IntentSha256
            putIntentPath=$protection.PutIntentPath; putIntentSha256=$protection.PutIntentSha256
            serverLockLowerBound=$protection.ServerLockLowerBound; configSha256=$Config.Hash
            settingsBeforeSha256=$Config.SettingsHash }
        $result.verification = [ordered]@{ snapshot=$true; entries=$true; witness=$true; control=$true
            put=$true; readback=$true }
        $result.status='passed'; $result.reasonCode='candidate'; $result.residue.remote='locked-retained'
        $result.residue.retainUntil=$protection.ServerLockLowerBound
        $result.residue.cleanup='local-retained'
    } catch {
        $result.ledger=$null; $result.outputPath=$null; $result.reasonCode='unconfirmed'
        $result.residue.cleanup='local-retained'
        $putIntentPath=[IO.Path]::Combine($OperationRoot,'put-intent.json')
        $result.residue.remote=if ([IO.File]::Exists($putIntentPath)) { 'may-have-been-sent' } else { 'pre-put-intent' }
        if ([IO.File]::Exists($putIntentPath)) {
            try { $result.residue.retainUntil=(Read-ArtifactJson $putIntentPath).Data.serverLockLowerBound } catch {}
        }
    } finally {
        try {
            $null = Write-ArtifactJson ([IO.Path]::Combine($OperationRoot,'result.json')) $result
            Protect-ArtifactTree $OperationRoot
        } catch { $result.status='failed'; $result.reasonCode='result-persistence-unconfirmed'; $result.ledger=$null; $result.outputPath=$null }
    }
    return [pscustomobject]$result
}

Export-ModuleMember -Function Invoke-ArtifactPublish, Invoke-ArtifactFetch, Invoke-ArtifactPublishPrepared,
    Get-Remaining, New-ArtifactRequest,
    Test-ArtifactSuccess, Test-ArtifactMissing, Write-ArtifactJson, New-ArtifactResult,
    Invoke-ArtifactObservedNetwork, Write-ArtifactObservationRecord
