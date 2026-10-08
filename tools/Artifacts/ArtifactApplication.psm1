Set-StrictMode -Version Latest

# This is an invocation-local deadline, never a persisted policy clock or CLI input.
$script:Budget=$null
function Assert-EvidenceBudget { if($script:Budget -and (& $script:Budget.Remaining) -le 0){throw 'evidence-deadline'} }
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'Transport/R2ArtifactTransport.psm1') -Force

$script:PackageAssembly = Join-Path $PSScriptRoot 'Packaging/artifacts/package/ArtifactPackaging.dll'
$script:ReadbackChildPath = Join-Path $PSScriptRoot 'Transport/ArtifactReadback.ps1'
# Private test hooks are not exported and are unavailable through the public CLI.
$script:TransportHook = $null
$script:ReadbackHook = $null
$script:ReadbackLaunchHook = $null

function Import-ArtifactPackage {
    if (-not [IO.File]::Exists($script:PackageAssembly)) { throw 'Artifact package unavailable.' }
    $assembly = [Reflection.Assembly]::LoadFrom($script:PackageAssembly)
    if ($null -eq $assembly.GetType('OneStarMaker.Artifacts.Packaging.PackageIO', $true)) {
        throw 'Artifact package unavailable.'
    }
}

function Get-Remaining([Diagnostics.Stopwatch] $Timer, [int] $Maximum = 120000) {
    $remaining = [Math]::Min($Maximum, 600000 - [int]$Timer.ElapsedMilliseconds)
    if($script:Budget){$remaining=[Math]::Min($remaining,[long](& $script:Budget.Remaining))}
    if ($remaining -lt 1) { throw 'Artifact operation expired.' }
    return $remaining
}

function Invoke-ArtifactNetwork([string] $Generation, [hashtable] $Request) {
    Assert-EvidenceBudget
    $requestCopy = $Request.Clone();if($script:Budget){$requestCopy.DeadlineMilliseconds=[Math]::Min([long]$requestCopy.DeadlineMilliseconds,[long](& $script:Budget.Remaining))}
    if ($script:TransportHook) { return & $script:TransportHook $Generation $requestCopy }
    $transportInvoker = (Get-Command Invoke-R2ArtifactOperation -ErrorAction Stop).ScriptBlock
    $budget=$script:Budget
    $callback = {
        param($id, $secret, $actualGeneration, $details)
        if ($actualGeneration -cne $Generation) { throw 'Credential generation changed.' }
        if($budget){$left=[long](& $budget.Remaining);if($left -lt 1){throw 'Artifact operation expired.'};$details.DeadlineMilliseconds=[Math]::Min([long]$details.DeadlineMilliseconds,$left)}
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
    Assert-EvidenceBudget
    $json = ConvertTo-Json -InputObject $Value -Compress -Depth 20
    Assert-EvidenceBudget;[IO.File]::WriteAllText($Path, $json, [Text.UTF8Encoding]::new($false));Assert-EvidenceBudget
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

# Only a proven non-launch may turn the unknown marker into confirmed stop. The
# fixed small journal uses the caller's hard reserve, never reopens ordinary work.
function Confirm-ArtifactReadbackNotStarted([string]$Path) {
    if($script:Budget -and (& $script:Budget.HardRemaining) -le 0){throw 'Readback child unconfirmed.'}
    $root=[IO.Path]::GetDirectoryName($Path);$lock=[IO.Path]::Combine($root,'operation.lock')
    Assert-ArtifactOperationFile $root $lock;Assert-NoArtifactReparse $Path
    $temporary=$Path+'.'+[Guid]::NewGuid().ToString('N')+'.pending';$stream=$null
    $bytes=[Text.Encoding]::UTF8.GetBytes('{"process":{"pid":0,"startedAt":null},"exited":true,"pipesClosed":true}')
    try {
        $stream=[IO.FileStream]::new($temporary,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        $acl=[IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($lock))
        # A freshly read FileSecurity has no dirty sections; copy its descriptor
        # into a new instance so SetAccessControl actually persists the private DACL.
        $private=[Security.AccessControl.FileSecurity]::new();$private.SetSecurityDescriptorBinaryForm($acl.GetSecurityDescriptorBinaryForm(),[Security.AccessControl.AccessControlSections]'Access,Owner,Group')
        [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($temporary),$private)
        $stream.Write($bytes,0,$bytes.Length);$stream.Flush($true);$stream.Dispose();$stream=$null
        if($script:Budget -and (& $script:Budget.HardRemaining) -le 0){throw 'Readback child unconfirmed.'}
        Assert-NoArtifactReparse $Path;[IO.File]::Move($temporary,$Path,$true)
    } finally {if($stream){$stream.Dispose()};if([IO.File]::Exists($temporary)){[IO.File]::Delete($temporary)}}
}

# The parent owns this invocation's previously absent GUID destination. Only
# confirmed child exit and EOF permit protecting its late-created output file;
# unrelated existing files and unknown children never enter this path.
function Protect-ArtifactReadbackOutput([string]$OperationRoot,[string]$Path,[Diagnostics.Stopwatch]$Timer) {
    $null=Get-Remaining $Timer 600000
    $lock=[IO.Path]::Combine($OperationRoot,'operation.lock');Assert-ArtifactOperationFile $OperationRoot $lock
    Assert-NoArtifactReparse $Path
    if([IO.Directory]::Exists($Path)){throw 'Readback destination unavailable.'}
    if([IO.File]::Exists($Path)){
        $acl=[IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($lock))
        # A freshly read FileSecurity has no dirty sections; copy its descriptor
        # into a new instance so SetAccessControl actually persists the private DACL.
        $private=[Security.AccessControl.FileSecurity]::new();$private.SetSecurityDescriptorBinaryForm($acl.GetSecurityDescriptorBinaryForm(),[Security.AccessControl.AccessControlSections]'Access,Owner,Group')
        $null=Get-Remaining $Timer 600000
        [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($Path),$private)
        Assert-ArtifactOperationFile $OperationRoot $Path
    }
    $null=Get-Remaining $Timer 600000
}

function Invoke-ArtifactReadback([string] $OperationRoot, $Config, [string] $Generation,
    [string] $Key, [long] $Bytes, [string] $Hash, [Diagnostics.Stopwatch] $Timer) {
    Assert-EvidenceBudget
    if((Get-Remaining $Timer 600000) -le 15000){throw 'Readback child unconfirmed.'}
    if ($script:ReadbackHook) { return & $script:ReadbackHook $OperationRoot $Config $Generation $Key $Bytes $Hash }
    $requestPath = [IO.Path]::Combine($OperationRoot, 'readback-' + [Guid]::NewGuid().ToString('N') + '.json')
    $destination = [IO.Path]::Combine($OperationRoot, 'readback-' + [Guid]::NewGuid().ToString('N') + '.bin')
    Assert-NoArtifactReparse $destination
    if([IO.File]::Exists($destination) -or [IO.Directory]::Exists($destination)){throw 'Readback destination unavailable.'}
    $request = [ordered]@{ schemaVersion = 1; operationRoot = $OperationRoot; endpoint = $Config.Data.endpoint
        generation = $Generation; key = $Key; expectedBytes = $Bytes; expectedSha256 = $Hash
        destination = $destination; deadlineMilliseconds = [Math]::Min(120000,(Get-Remaining $Timer 600000)-15000) }
    $null = Write-ArtifactJson $requestPath $request
    Protect-ArtifactTree $OperationRoot
    $info = [Diagnostics.ProcessStartInfo]::new()
    $info.FileName = 'pwsh'; $info.UseShellExecute = $false; $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    foreach ($argument in @('-NoProfile','-File',$script:ReadbackChildPath,'-RequestPath',$requestPath)) {
        [void]$info.ArgumentList.Add($argument)
    }
    $process = [Diagnostics.Process]::new(); $process.StartInfo = $info
    $started=$false;$launchAttempted=$false;$notStartedConfirmed=$false;$outTask=$null;$errTask=$null
    $budget=$script:Budget
    $operationRemaining = { $left=[Math]::Max(0,600000-[int]$Timer.ElapsedMilliseconds);if($budget){$left=[Math]::Min($left,[long](& $budget.Remaining))};return $left }.GetNewClosure()
    $childMarker=[IO.Path]::Combine($OperationRoot,'readback-process.json')
    # A crash/expiry between launch and PID journaling must leave an unknown-child
    # marker rather than let the next worker infer that no child was started.
    $childRecord=[ordered]@{process=[ordered]@{pid=0;startedAt=$null};exited=$false;pipesClosed=$false}
    try {
        $null=Write-ArtifactJson $childMarker $childRecord
        if($script:ReadbackLaunchHook){$null=& $script:ReadbackLaunchHook 'before-start' $process}
        Assert-EvidenceBudget
        if((Get-Remaining $Timer 600000) -le 15000){throw 'Readback child unconfirmed.'}
        $launchAttempted=$true
        $didStart=if($script:ReadbackLaunchHook){& $script:ReadbackLaunchHook 'start' $process}else{$process.Start()}
        if (-not $didStart) { $notStartedConfirmed=$true;throw 'Readback child unavailable.' }
        $started=$true
        $outTask = $process.StandardOutput.ReadToEndAsync()
        $errTask = $process.StandardError.ReadToEndAsync()
        if($script:ReadbackLaunchHook){$null=& $script:ReadbackLaunchHook 'after-start' $process}
        $childRecord.process=[ordered]@{pid=$process.Id;startedAt=$process.StartTime.ToUniversalTime().ToString('o')}
        $null=Write-ArtifactJson $childMarker $childRecord;Protect-ArtifactTree $OperationRoot
        # Reserve the final 15 seconds of the operation budget for stop and pipe verification.
        $wait = [Math]::Min(135000, [Math]::Max(0, (Get-Remaining $Timer 600000) - 15000))
        if (-not $process.WaitForExit($wait)) {
            $stop = { if (-not $process.HasExited) { $process.Kill($true) } }.GetNewClosure()
            $waitExit = { param($milliseconds) $process.WaitForExit($milliseconds) }.GetNewClosure()
            $waitPipes = { param($milliseconds) [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($outTask,$errTask), $milliseconds) }.GetNewClosure()
            $clock = { [Diagnostics.Stopwatch]::GetTimestamp() }
            Invoke-ReadbackStop $stop $waitExit $waitPipes $clock $operationRemaining
        }
        $pipeBudget = [Math]::Min(15000, (Get-Remaining $Timer 15000))
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($outTask,$errTask), $pipeBudget)) { throw 'Readback pipe unconfirmed.' }
        Protect-ArtifactReadbackOutput $OperationRoot $destination $Timer
        $childRecord.exited=$true;$childRecord.pipesClosed=$true;$null=Write-ArtifactJson $childMarker $childRecord
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
    } catch {
        $failure=$_
        if(-not $launchAttempted -or $notStartedConfirmed){
            try{Confirm-ArtifactReadbackNotStarted $childMarker}catch{}
        }elseif(-not $started){
            # Start exceptions do not prove absence. An associated process may be
            # stopped, but a missing PID leaves the original unknown marker intact.
            try{$null=$process.Id;$started=$true;$outTask=$process.StandardOutput.ReadToEndAsync();$errTask=$process.StandardError.ReadToEndAsync()}catch{}
        }
        # PID journaling or a deadline check may fail after launch. Attempt the
        # same bounded stop; an unconfirmed marker still protects the payload.
        if($started -and -not $process.HasExited){
            try{Invoke-ReadbackStop {if(-not $process.HasExited){$process.Kill($true)}}.GetNewClosure() {param($ms)$process.WaitForExit($ms)}.GetNewClosure() {param($ms)[Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($outTask,$errTask),$ms)}.GetNewClosure() {[Diagnostics.Stopwatch]::GetTimestamp()} $operationRemaining}catch{}
        }
        throw $failure
    } finally { $process.Dispose() }
}

Export-ModuleMember -Function Get-Remaining, New-ArtifactRequest,
    Test-ArtifactSuccess, Test-ArtifactMissing, Write-ArtifactJson, New-ArtifactResult,
    Invoke-ArtifactObservedNetwork, Write-ArtifactObservationRecord, Import-ArtifactPackage, Invoke-ArtifactNetwork, Invoke-ArtifactReadback, Test-ArtifactReadback
