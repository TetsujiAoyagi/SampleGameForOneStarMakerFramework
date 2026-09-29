param([string[]] $Case = @('*'), [string] $ResultPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '../Probe/RouteProof.ps1') -Library
$script:passed = [Collections.Generic.List[string]]::new()
$script:failed = [Collections.Generic.List[string]]::new()
$script:selected = [Collections.Generic.List[string]]::new()
$script:executed = [Collections.Generic.List[string]]::new()
$tokens=$null; $parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($PSCommandPath,[ref]$tokens,[ref]$parseErrors)
$script:registered=@($ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ceq 'Run' },$true) | ForEach-Object { $_.CommandElements[1].Value } | Where-Object { $_ -is [string] })
if ($parseErrors.Count -gt 0 -or $script:registered.Count -eq 0) { throw 'RouteProof case registration unavailable.' }

function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Invoke-PwshCapture([string[]] $Arguments) {
    $info=[Diagnostics.ProcessStartInfo]::new(); $info.FileName='pwsh'; $info.UseShellExecute=$false
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true; $info.CreateNoWindow=$true
    foreach($argument in $Arguments){ [void]$info.ArgumentList.Add($argument) }
    $process=[Diagnostics.Process]::new(); $process.StartInfo=$info
    try {
        if(-not $process.Start()){ throw 'JSON child process did not start' }
        $stdoutTask=$process.StandardOutput.ReadToEndAsync(); $stderrTask=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(10000)){
            try { $process.Kill($true) } catch {}
            [void]$process.WaitForExit(5000)
            [void]$stdoutTask.Wait(1000); [void]$stderrTask.Wait(1000)
            throw 'JSON child process exceeded its test deadline'
        }
        if(-not $stdoutTask.Wait(1000) -or -not $stderrTask.Wait(1000)){ throw 'JSON child pipes were not drained' }
        return [pscustomobject]@{ ExitCode=$process.ExitCode; Stdout=$stdoutTask.Result; Stderr=$stderrTask.Result }
    } finally { $process.Dispose() }
}
function Invoke-JsonEchoChild([string] $JsonLine) {
    # OS上の別pwshへJSONを渡してstdoutから回収し、親側が実process出力をparseする境界を確認します。
    $jsonBase64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($JsonLine))
    $command="[Console]::WriteLine([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('$jsonBase64')))"
    $encodedCommand=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
    $result=Invoke-PwshCapture @('-NoProfile','-EncodedCommand',$encodedCommand)
    if($result.ExitCode -ne 0 -or $result.Stderr){ throw 'JSON child process did not produce a clean result' }
    return $result.Stdout
}
function Run([string] $Name, [scriptblock] $Body) {
    if (-not (@($Case | Where-Object { $Name -like $_ }).Count -gt 0)) { return }
    $script:selected.Add($Name)
    try { & $Body; $script:passed.Add($Name) } catch { $script:failed.Add($Name + ': ' + $_.Exception.Message) }
    $script:executed.Add($Name)
}
function Fake-Observation(
    [string] $Operation = 'unsigned-get',
    [int] $HttpStatus = 403,
    [string] $StatusClass = 'forbidden',
    [object] $S3Code = 'AccessDenied',
    [int] $ByteCount = 12,
    [string] $BodySha256 = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa',
    [object] $PrefixSha256 = $null,
    [bool] $EofConfirmed = $true,
    [bool] $LimitReached = $false,
    [bool] $TimedOut = $false,
    [bool] $Redirected = $false,
    [object] $Method = 'GET',
    [object] $TargetUri = $null,
    [bool] $HasAuthHeader = $false,
    [bool] $SignatureQueryPresent = $false,
    [bool] $TargetChangingQueryPresent = $false,
    [string] $Generation = '0123456789abcdef0123456789abcdef') {
    if ($null -eq $TargetUri) { $TargetUri = 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com/osm-artifacts/probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt' }
    [pscustomobject][ordered]@{
        Operation = $Operation; HttpStatus = $HttpStatus; StatusClass = $StatusClass; S3Code = $S3Code
        ByteCount = $ByteCount; BodySha256 = $BodySha256; PrefixSha256 = $PrefixSha256
        EofConfirmed = $EofConfirmed; LimitReached = $LimitReached; TimedOut = $TimedOut
        Redirected = $Redirected; Generation = $Generation
        Method = $Method; TargetUri = $TargetUri
        HasAuthHeader = $HasAuthHeader; SignatureQueryPresent = $SignatureQueryPresent; TargetChangingQueryPresent = $TargetChangingQueryPresent
    }
}

$global:RouteProofTestExpectedHash = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'

try {
    Run 'strict endpoint and key grammar' {
        Assert (Test-Endpoint 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com') 'valid endpoint rejected'
        Assert (-not (Test-Endpoint 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com/')) 'endpoint slash accepted'
        Assert (-not (Test-Endpoint "https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com`n")) 'endpoint with a final line feed accepted'
        Assert (-not (Test-RunId (('a' * 32) + "`n"))) 'run id with a final line feed accepted'
        Assert (-not (Test-Hash (('a' * 64) + "`n"))) 'hash with a final line feed accepted'
        Assert (Test-Key 'probe/unlocked/0123456789abcdef0123456789abcdef/object.txt') 'valid key rejected'
        Assert (-not (Test-Key ("probe/unlocked/0123456789abcdef0123456789abcdef/object.txt`n"))) 'key with a final line feed accepted'
        $profile=[pscustomobject]@{Profile='osm';Bucket='osm-artifacts';Endpoint=$null;Generation='0123456789abcdef0123456789abcdef'}
        Assert (Test-RouteProofCredentialStatus $profile) 'valid credential v1 record with its required null endpoint was rejected'
        $profile.Endpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'
        Assert (-not (Test-RouteProofCredentialStatus $profile)) 'credential record accepted a non-null endpoint'
        Assert (-not (Test-Key 'probe/locked/0123456789abcdef0123456789abcdef/object.txt?x=1')) 'query accepted'
        Assert (Test-Hash '0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef') 'valid hash rejected'
        Assert (-not (Test-Hash 'not-a-hash')) 'invalid hash accepted'
        Assert (-not (Test-Key "probe/unlocked/0123456789abcdef0123456789abcdef/$([char]1)")) 'control character accepted'
        Assert (-not (Test-Key 'probe/locked/0123456789abcdef0123456789abcdef/object.txt' 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')) 'foreign run-id accepted'
        Assert (-not (Test-Key 'probe/unlocked/0123456789abcdef0123456789abcdef/..')) 'dot-segment object key accepted'
    }
    Run 'unsigned exact object is not private' {
        $script:ActiveEndpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'; $script:ActiveKey='probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt'
        $fake = Fake-Observation -BodySha256 $global:RouteProofTestExpectedHash -PrefixSha256 $global:RouteProofTestExpectedHash -ByteCount 23 -Method $null -TargetUri $null
        Assert (-not (Test-UnsignedPrivacy $fake $global:RouteProofTestExpectedHash 23)) 'exact object exposure accepted'
    }
    Run 'unsigned object plus bytes is not private' {
        $script:ActiveEndpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'; $script:ActiveKey='probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt'
        $fake = Fake-Observation -BodySha256 $global:RouteProofTestExpectedHash -PrefixSha256 $global:RouteProofTestExpectedHash -ByteCount 29 -Method $null -TargetUri $null
        Assert (-not (Test-UnsignedPrivacy $fake $global:RouteProofTestExpectedHash 23)) 'appended object exposure accepted'
    }
    Run 'unsigned rejection requires EOF, code and different hash' {
        $script:ActiveEndpoint = 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'
        $script:ActiveKey = 'probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt'
        Assert (Test-UnsignedPrivacy (Fake-Observation -BodySha256 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa') $global:RouteProofTestExpectedHash 23) 'valid denial rejected'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -HttpStatus 401 -StatusClass 'unauthorized' -S3Code 'AccessDenied') $global:RouteProofTestExpectedHash 23)) 'crossed 401/AccessDenied tuple accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -HttpStatus 403 -StatusClass 'forbidden' -S3Code 'Unauthorized') $global:RouteProofTestExpectedHash 23)) 'crossed 403/Unauthorized tuple accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -S3Code 'SignatureDoesNotMatch') $global:RouteProofTestExpectedHash 23)) 'signature mismatch accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -LimitReached $true) $global:RouteProofTestExpectedHash 23)) 'bounded body accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -EofConfirmed $false) $global:RouteProofTestExpectedHash 23)) 'non-EOF body accepted'
        $r2InvalidArgument = Fake-Observation -HttpStatus 400 -StatusClass 'other' -S3Code 'InvalidArgument' -BodySha256 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' -PrefixSha256 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        Assert (Test-UnsignedPrivacy $r2InvalidArgument $global:RouteProofTestExpectedHash 23) '400 InvalidArgument denial rejected'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -HttpStatus 400 -StatusClass 'other' -S3Code 'InvalidArgument' -BodySha256 $global:RouteProofTestExpectedHash) $global:RouteProofTestExpectedHash 23)) '400 with exact object body accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -HttpStatus 400 -StatusClass 'other' -S3Code 'InvalidArgument' -PrefixSha256 $global:RouteProofTestExpectedHash) $global:RouteProofTestExpectedHash 23)) '400 with object prefix accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -HttpStatus 400 -StatusClass 'other' -S3Code 'InvalidArgument' -EofConfirmed $false) $global:RouteProofTestExpectedHash 23)) '400 without EOF accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -HttpStatus 400 -StatusClass 'other' -S3Code 'AccessDenied') $global:RouteProofTestExpectedHash 23)) 'unpaired 400 accepted'
        $unsignedWithAuth = Fake-Observation -HasAuthHeader $true
        Assert (-not (Test-UnsignedPrivacy $unsignedWithAuth $global:RouteProofTestExpectedHash 23)) 'signed request was treated as unsigned'
    }
    Run 'unsigned 400 timeout is not private even with EOF' {
        $script:ActiveEndpoint = 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'
        $script:ActiveKey = 'probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt'
        $timedOutDenial = Fake-Observation -HttpStatus 400 -StatusClass 'other' -S3Code 'InvalidArgument' -TimedOut $true -EofConfirmed $true -BodySha256 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' -PrefixSha256 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        Assert (-not (Test-UnsignedPrivacy $timedOutDenial $global:RouteProofTestExpectedHash 23)) 'timed-out 400 InvalidArgument with EOF was accepted as private'
    }
    Run 'credential callback closure binds production helpers' {
        $deadlineBudget = 30000
        $startTimestamp = [Diagnostics.Stopwatch]::GetTimestamp()
        $script:callbackTransportCalls = 0
        $stubTransport = {
            param($id, $secret, $generation, $requestValue)
            $script:callbackTransportCalls++
            [pscustomobject]@{ DeadlineMilliseconds = $requestValue.DeadlineMilliseconds; CredentialId = $id }
        }
        $callback = New-RouteProofCredentialCallback $startTimestamp $deadlineBudget $stubTransport
        $request = [pscustomobject]@{ DeadlineMilliseconds = $deadlineBudget }
        $callbackResult = & $callback 'dummy-id' 'dummy-secret' '0123456789abcdef0123456789abcdef' $request
        Assert ($script:callbackTransportCalls -eq 1) 'credential callback did not invoke its injected transport'
        Assert ($callbackResult.DeadlineMilliseconds -ge 1 -and $callbackResult.DeadlineMilliseconds -le $deadlineBudget) 'callback did not pass the bounded remaining deadline'
        Assert ($callbackResult.CredentialId -ceq 'dummy-id') 'callback lost its bound transport invocation'

        $script:callbackTransportCalls = 0
        $expiredCallback = New-RouteProofCredentialCallback ($startTimestamp - [Diagnostics.Stopwatch]::Frequency * 40) 30000 $stubTransport
        $expired = $false
        try { & $expiredCallback 'dummy-id' 'dummy-secret' '0123456789abcdef0123456789abcdef' ([pscustomobject]@{ DeadlineMilliseconds = 30000 }) | Out-Null }
        catch { $expired = $true }
        Assert $expired 'expired callback did not stop before transport'
        Assert ($script:callbackTransportCalls -eq 0) 'expired callback invoked transport after the operation deadline'
    }
    Run 'lock requires exact provider code' {
        Assert (Test-LockRejection (Fake-Observation -Operation 'put' -S3Code 'ObjectLockedByBucketPolicy')) '403 lock rejection rejected'
        Assert (Test-LockRejection (Fake-Observation -Operation 'delete' -HttpStatus 409 -StatusClass 'other' -S3Code 'ObjectLockedByBucketPolicy')) '409 lock rejection rejected'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'put' -S3Code 'AccessDenied'))) 'generic 403 accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'put' -HttpStatus 409 -StatusClass 'other' -S3Code 'AccessDenied'))) 'generic 409 accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'put' -HttpStatus 409 -StatusClass 'forbidden' -S3Code 'ObjectLockedByBucketPolicy'))) '409 with crossed class accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'put' -HttpStatus 403 -StatusClass 'other' -S3Code 'ObjectLockedByBucketPolicy'))) '403 with crossed class accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'put' -S3Code 'ObjectLockedByBucketPolicy' -TimedOut $true -EofConfirmed $false))) 'timed out lock response accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'put' -S3Code 'ObjectLockedByBucketPolicy' -LimitReached $true))) 'truncated lock response accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'put' -S3Code 'ObjectLockedByBucketPolicy' -Redirected $true))) 'redirected lock response accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'delete' -HttpStatus 409 -StatusClass 'other' -S3Code 'ObjectLockedByBucketPolicy' -TimedOut $true))) 'timed out 409 accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'delete' -HttpStatus 409 -StatusClass 'other' -S3Code 'ObjectLockedByBucketPolicy' -EofConfirmed $false))) '409 without EOF accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'delete' -HttpStatus 409 -StatusClass 'other' -S3Code 'ObjectLockedByBucketPolicy' -LimitReached $true))) '409 at limit accepted'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'delete' -HttpStatus 409 -StatusClass 'other' -S3Code 'ObjectLockedByBucketPolicy' -Redirected $true))) 'redirected 409 accepted'
    }
    Run 'timeout and transport failures are inconclusive' {
        $fake = Fake-Observation -StatusClass 'timeout' -HttpStatus 0 -S3Code $null -EofConfirmed $false -TimedOut $true
        Assert (-not (Test-UnsignedPrivacy $fake $global:RouteProofTestExpectedHash 23)) 'timeout accepted'
        Assert ((Get-StatusClass 404) -eq 'not-found' -and (Get-StatusClass 429) -eq 'too-many-requests' -and (Get-StatusClass 503) -eq 'server-error') 'status classification'
        Assert (-not (Test-AuthenticatedMatch (Fake-Observation -Operation 'authenticated-get' -HttpStatus 200 -StatusClass 'success' -S3Code $null -ByteCount 23 -BodySha256 $global:RouteProofTestExpectedHash -Generation 'fedcba9876543210fedcba9876543210') $global:RouteProofTestExpectedHash 23)) 'wrong generation accepted'
    }
    Run 'pipe budget is measured from child start, not accumulated run time' {
        $script:RouteProofClock = { [TimeSpan]::FromSeconds(80) }
        $remaining = Get-RemainingPipeWait 45000 ([TimeSpan]::FromSeconds(50))
        Assert ($remaining -eq 15000) 'prior operation time consumed the child pipe budget'
        $script:RouteProofClock = { [TimeSpan]::FromSeconds(46) }
        $expired = Get-RemainingPipeWait 45000 ([TimeSpan]::Zero)
        Assert ($expired -eq 0) 'expired child budget remained available'
        $script:RouteProofClock = $null
    }
    Run 'transport timeout leaves result serialization and stop time inside the parent budget' {
        $budget = Get-RouteProofChildBudgetSchedule 45000
        Assert ($budget.ProcessWaitMilliseconds -eq 30000 -and $budget.TransportBudgetMilliseconds -eq 29000 -and
            $budget.ResultReserveMilliseconds -eq 1000 -and $budget.TerminationReserveMilliseconds -eq 15000) '45-second child budget was not divided into transport, result, and termination windows'
        $script:RouteProofClock = { [TimeSpan]::FromSeconds(5) }
        $remaining = Get-RemainingOperationBudget $budget.TransportBudgetMilliseconds 5000
        Assert ($remaining -eq 24000) 'child setup time was not deducted from transport timeout'
        Assert ((5000 + $remaining) -le ($budget.ProcessWaitMilliseconds - $budget.ResultReserveMilliseconds)) 'parent may kill the child before its transport cancellation can be serialized'
        $short = Get-RouteProofChildBudgetSchedule 30000
        Assert ($short.ProcessWaitMilliseconds -eq 20000 -and $short.TransportBudgetMilliseconds -eq 19000) 'short remaining run budget exceeded its parent wait'
        $expiredBudget = Get-RouteProofChildBudgetSchedule 0
        Assert ($expiredBudget.ProcessWaitMilliseconds -eq 0 -and $expiredBudget.TransportBudgetMilliseconds -eq 0 -and $expiredBudget.TerminationReserveMilliseconds -eq 0) 'expired run budget created a child wait window'
        $script:RouteProofClock = { [TimeSpan]::FromSeconds(30) }
        Assert ((Get-RemainingOperationBudget 29000 30000) -eq 0) 'expired child operation retained transport time'
        Assert ((Get-RemainingOperationBudget 29000 -1) -eq 0) 'future or invalid monotonic start timestamp extended the transport deadline'
        $script:RouteProofClock = $null
    }
    Run 'stdout and stderr consume one shared pipe deadline' {
        $script:pipeClockCalls=0; $script:pipeWaits=[Collections.Generic.List[int]]::new()
        $script:RouteProofClock={ $script:pipeClockCalls++; if($script:pipeClockCalls -eq 1){[TimeSpan]::Zero}else{[TimeSpan]::FromSeconds(4)} }
        $script:RouteProofTaskWaiter={ param($task,$milliseconds) $script:pipeWaits.Add($milliseconds); return $true }
        Assert (Wait-RouteProofPipes ([Threading.Tasks.Task]::CompletedTask) ([Threading.Tasks.Task]::CompletedTask) 5000 ([TimeSpan]::Zero)) 'completed pipe reads failed'
        Assert (($script:pipeWaits -join ',') -eq '5000,1000') "pipe waits did not share one 5-second budget: $($script:pipeWaits -join ',')"
        $script:RouteProofClock=$null; $script:RouteProofTaskWaiter=$null
    }
    Run 'child stop requires parent exit and both redirected pipes to close' {
        Assert (Test-RouteProofChildStopped $true $true $true) 'fully closed child was rejected'
        Assert (-not (Test-RouteProofChildStopped $true $true $false)) 'live descendant retaining stderr was treated as stopped'
        Assert (-not (Test-RouteProofChildStopped $false $true $true)) 'running parent was treated as stopped'

        # 実際の子pwshを期限途中で停止し、Kill呼出し直後でなくprocess終了と両pipe完了まで待つことを確認します。
        $info=[Diagnostics.ProcessStartInfo]::new(); $info.FileName='pwsh'; $info.UseShellExecute=$false
        $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true; $info.CreateNoWindow=$true
        [void]$info.ArgumentList.Add('-NoProfile'); [void]$info.ArgumentList.Add('-Command'); [void]$info.ArgumentList.Add('Start-Sleep -Seconds 30')
        $process=[Diagnostics.Process]::new(); $process.StartInfo=$info
        try {
            Assert ($process.Start()) 'stop fixture child did not start'
            $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
            $script:RouteProofClock=$null; Start-RouteClock; $childStarted=[TimeSpan]::Zero
            $stopped=Stop-RouteProofChild $process $stdout $stderr 45000 $childStarted
            Assert ($stopped -and $process.HasExited -and $stdout.IsCompleted -and $stderr.IsCompleted) 'kill returned before child and redirected pipes were confirmed stopped'
        } finally { $script:RouteProofClock=$null; $process.Dispose() }
    }
    Run 'child PUT payload is bounded, hash checked and bound to the run marker' {
        $runId='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'; $bytes=[Text.Encoding]::ASCII.GetBytes("OSM-ROUTE-PROOF:${runId}:original")
        $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        Assert (Test-ChildPayload 'put' $runId $hash $bytes.Length $bytes) 'valid run fixture rejected'
        Assert (-not (Test-ChildPayload 'put' 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb' $hash $bytes.Length $bytes)) 'fixture from another run accepted'
        $oversized=[byte[]]::new(1025)
        Assert (-not (Test-ChildPayload 'put' $runId $hash $oversized.Length $oversized)) 'oversized child fixture accepted'
        Assert (-not (Test-ChildPayload 'authenticated-get' $runId $hash $bytes.Length $bytes)) 'non-PUT operation accepted a payload'
    }
    Run 'invalid implementation revision is not echoed to the result' {
        $scriptPath=(Resolve-Path (Join-Path $PSScriptRoot '../Probe/RouteProof.ps1')).Path
        $valid='0123456789abcdef0123456789abcdef01234567'; $invalid='z'*40
        $child=Invoke-PwshCapture @('-NoProfile','-File',$scriptPath,'-Endpoint','https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com','-ImplementationBase',$invalid,'-ImplementationHead',$valid)
        $record=$child.Stdout.Trim() | ConvertFrom-Json
        Assert ($child.ExitCode -eq 3 -and $record.base -eq '' -and $record.head -ceq $valid) 'invalid revision leaked into fixed-schema result'
        Assert (-not $child.Stdout.Contains($invalid)) 'untrusted revision text was reflected'
    }
    Run 'child rejects oversized PUT before credential access' {
        $scriptPath=(Resolve-Path (Join-Path $PSScriptRoot '../Probe/RouteProof.ps1')).Path
        $runId='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'; $bytes=[byte[]]::new(1025)
        $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        $valid='0123456789abcdef0123456789abcdef01234567'; $payload=[Convert]::ToBase64String($bytes)
        $child=Invoke-PwshCapture @('-NoProfile','-File',$scriptPath,'-Endpoint','https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com','-Child','-Operation','put','-Key',"probe/unlocked/$runId/object.txt",'-ExpectedHash',$hash,'-ExpectedBytes','1025','-PayloadBase64',$payload,'-RunId',$runId,'-ImplementationBase',$valid,'-ImplementationHead',$valid)
        $record=$child.Stdout.Trim() | ConvertFrom-Json
        Assert ($child.ExitCode -eq 4 -and $record.StatusClass -eq 'inconclusive') 'child accepted or misclassified oversized PUT input'
        Assert (-not $child.Stdout.Contains($payload)) 'invalid payload was echoed into child output'
    }
    Run 'child result must describe the requested operation' {
        $script:RouteProofClock = { [TimeSpan]::Zero }
        $script:RouteProofChildRunner = { param($endpoint,$operation,$key,$hash,$bytes,$payload,$runId,$remaining,$deadline) Fake-Observation -Operation 'authenticated-get' }
        $mismatch = Invoke-ChildOperation 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' 'delete' 'probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt' $global:RouteProofTestExpectedHash 23 $null 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
        Assert ($null -eq $mismatch) 'child operation mismatch was accepted'
        $script:RouteProofClock = $null; $script:RouteProofChildRunner = $null
    }
    Run 'result schema is closed and secret-free' {
        $record = New-ObservationRecord 'pass' 'offline' 'unsigned-get' 'probe/unlocked/0123456789abcdef0123456789abcdef/object.txt' $global:RouteProofTestExpectedHash 23 (Fake-Observation -BodySha256 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa') 'not-needed' $null 'base' 'head'
        $names = @($record.PSObject.Properties.Name)
        Assert ((($names | Sort-Object) -join ',') -ceq 'base,cleanup,eofConfirmed,executedUtc,expectedBytes,expectedHash,generation,head,httpStatus,key,limitReached,lockRule,observedBytes,observedHash,operation,phase,prefixHash,result,s3Code,statusClass') 'result schema changed'
        Assert (-not ($record | ConvertTo-Json -Compress).Contains('AccessKey')) 'secret field leaked'
    }
    Run 'lock rule before evidence accepts ten fields and rejects invalid records' {
        $hash = 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        $json = [pscustomobject]@{ Prefix='probe/locked/'; Enabled=$true; Kind='Age'; RetentionSeconds=900; RuleCount=1; DateRules=0; IndefiniteRules=0; WriterCanConfigure=$false; LifecycleCompatible=$true; BeforeHash=$hash } | ConvertTo-Json -Compress
        $rule = Read-LockRule $json
        Assert ($rule.prefix -eq 'probe/locked/' -and $rule.retentionSeconds -eq 900) 'valid rule rejected'
        Assert ($rule.beforeHash -ceq $hash -and $null -eq $rule.afterHash) 'future After hash was inferred from Before'
        foreach ($badJson in @(
            ($json.TrimEnd('}') + ',"AfterHash":"' + $hash + '"}'),
            ($json -replace ',"BeforeHash":"[0-9a-f]{64}"',''),
            ($json -replace '"BeforeHash":"[0-9a-f]{64}"','"BeforeHash":42'),
            ($json.TrimEnd('}') + ',"Unknown":true}'),
            ($json -replace '"Prefix":"probe/locked/"','"Prefix":"probe/unlocked/"')
        )) {
            $rejected = $false
            try { Read-LockRule $badJson } catch { $rejected = $true }
            Assert $rejected 'invalid Before evidence or rule was accepted'
        }
        $bad = $json.Replace('"DateRules":0','"DateRules":1')
        $rejected = $false
        try { Read-LockRule $bad } catch { $rejected = $true }
        Assert $rejected 'date rule accepted'
    }
    Run 'child entry offline uses byte-identical production script and isolated modules' {
        $source = (Resolve-Path (Join-Path $PSScriptRoot '../Probe/RouteProof.ps1')).Path
        $fixture = Join-Path ([IO.Path]::GetTempPath()) ('route-proof-child-' + [Guid]::NewGuid().ToString('N'))
        $probe = Join-Path $fixture 'Probe'
        $credentials = Join-Path $fixture 'Credentials'
        [void](New-Item -ItemType Directory -Path $probe,$credentials -Force)
        try {
            $copy = Join-Path $probe 'RouteProof.ps1'
            Copy-Item -LiteralPath $source -Destination $copy
            Assert ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ceq (Get-FileHash -LiteralPath $copy -Algorithm SHA256).Hash) 'production script copy differs'
            # fixtureの相対import先はdummyのみ。実store、DPAPI、HTTP、DLLへ到達できません。
            @'
Set-StrictMode -Version Latest
function Get-CredentialStatus([string] $Profile) {
    if (Test-Path -LiteralPath (Join-Path $PSScriptRoot 'invalid-profile')) {
        return [pscustomobject]@{Profile='wrong';Bucket='osm-artifacts';Endpoint=$null;Generation='0123456789abcdef0123456789abcdef'}
    }
    return [pscustomobject]@{Profile='osm';Bucket='osm-artifacts';Endpoint=$null;Generation='0123456789abcdef0123456789abcdef'}
}
function Invoke-CredentialTransport([string] $Profile, [scriptblock] $Callback, [hashtable] $Request) {
    & $Callback 'dummy-id' 'dummy-secret-never-print' '0123456789abcdef0123456789abcdef' $Request
}
Export-ModuleMember -Function Get-CredentialStatus,Invoke-CredentialTransport
'@ | Set-Content -LiteralPath (Join-Path $credentials 'CredentialStore.psm1') -Encoding utf8
            @'
Set-StrictMode -Version Latest
$script:Marker = Join-Path $PSScriptRoot 'transport-called'
function Invoke-R2RouteTransport {
    param([string] $AccessKeyId, [string] $SecretAccessKey, [string] $Generation, [hashtable] $Request)
    if ($AccessKeyId -cne 'dummy-id' -or $SecretAccessKey -cne 'dummy-secret-never-print' -or
        $Generation -cne '0123456789abcdef0123456789abcdef' -or
        $Request.Operation -cne 'unsigned-get' -or $Request.DeadlineMilliseconds -lt 1 -or
        $Request.DeadlineMilliseconds -gt 30000) { throw 'Invalid dummy callback input.' }
    Set-Content -LiteralPath $script:Marker -Value $Request.Operation
    return [pscustomobject]@{
        Operation='unsigned-get';HttpStatus=403;StatusClass='forbidden';S3Code='AccessDenied'
        ByteCount=12;BodySha256='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
        PrefixSha256=$null;EofConfirmed=$true;LimitReached=$false;TimedOut=$false;Redirected=$false
        Generation=$Generation;Method='GET';TargetUri=$null;HasAuthHeader=$false
        SignatureQueryPresent=$false;TargetChangingQueryPresent=$false
    }
}
Export-ModuleMember -Function Invoke-R2RouteTransport
'@ | Set-Content -LiteralPath (Join-Path $probe 'R2RouteTransport.psm1') -Encoding utf8
            $endpoint = 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'
            $runId = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
            $hash = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
            $commit = 'cccccccccccccccccccccccccccccccccccccccc'
            $args = @('-NoProfile','-File',$copy,'-Endpoint',$endpoint,'-Child','-Operation','unsigned-get',
                '-Key',"probe/unlocked/$runId/object.txt",'-ExpectedHash',$hash,'-ExpectedBytes','23',
                '-RunId',$runId,'-ImplementationBase',$commit,'-ImplementationHead',$commit,
                '-OperationBudgetMilliseconds','30000','-OperationStartTimestamp',
                [string][Diagnostics.Stopwatch]::GetTimestamp())
            $normal = Invoke-PwshCapture $args
            $lines = @($normal.Stdout.Trim() -split "`r?`n" | Where-Object { $_ -ne '' })
            Assert ($normal.ExitCode -eq 0 -and $lines.Count -eq 1 -and -not $normal.Stderr) 'normal child failed or emitted extra output'
            $observation = $lines[0] | ConvertFrom-Json
            Assert ($observation.Operation -ceq 'unsigned-get' -and $observation.Generation -ceq '0123456789abcdef0123456789abcdef') 'child observation mismatch'
            Assert ((Test-Path -LiteralPath (Join-Path $probe 'transport-called'))) 'normal child did not call dummy transport'
            Assert (-not (($normal.Stdout + $normal.Stderr) -like '*dummy-secret-never-print*')) 'dummy secret escaped'
            Remove-Item -LiteralPath (Join-Path $probe 'transport-called')
            [void](New-Item -ItemType File -Path (Join-Path $credentials 'invalid-profile'))
            $args[-1] = [string][Diagnostics.Stopwatch]::GetTimestamp()
            $invalid = Invoke-PwshCapture $args
            Assert ($invalid.ExitCode -eq 3 -and -not $invalid.Stderr -and -not (Test-Path -LiteralPath (Join-Path $probe 'transport-called'))) 'invalid profile reached transport'
            Assert (-not (($invalid.Stdout + $invalid.Stderr) -like '*dummy-secret-never-print*')) 'dummy secret escaped on blocked path'
            Remove-Item -LiteralPath (Join-Path $credentials 'invalid-profile')
            $args[-1] = [string]([Diagnostics.Stopwatch]::GetTimestamp() - 60 * [Diagnostics.Stopwatch]::Frequency)
            $expired = Invoke-PwshCapture $args
            Assert ($expired.ExitCode -eq 4 -and -not $expired.Stderr -and -not (Test-Path -LiteralPath (Join-Path $probe 'transport-called'))) 'expired child reached transport'
            Assert (-not (($expired.Stdout + $expired.Stderr) -like '*dummy-secret-never-print*')) 'dummy secret escaped on expired path'
        } finally {
            Remove-Item -LiteralPath $fixture -Recurse -Force
        }
    }
    Run 'production operation loop uses bounded injected boundaries' {
        $runId = 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
        $script:Base='offline-base'; $script:Head='offline-head'
        $ruleHash = 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        $script:RouteProofClock = { [TimeSpan]::Zero }
        $script:RouteProofTransport = {
            param($endpoint,$operation,$key,$hash,$bytes,$payload,$expectedRunId,$remaining)
            if ($remaining -gt 300000) { throw 'run deadline exceeded' }
            if (-not (Test-Key $key $expectedRunId)) { throw 'child key/run mismatch' }
            $status = 200; $class = 'success'; $code = $null; $bodyHash = $hash; $prefixHash = $hash; $eof = $true
            if ($operation -eq 'unsigned-get') { $status=403; $class='forbidden'; $code='AccessDenied'; $bodyHash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'; $prefixHash=$hash }
            elseif ($operation -eq 'authenticated-get' -and $key -like 'probe/unlocked/*' -and $script:fakeUnlockedDeleted) { $status=404; $class='not-found'; $code='NoSuchKey'; $bodyHash=$null; $prefixHash=$null }
            elseif ($operation -eq 'delete') { $status=204 }
            elseif ($key -like 'probe/locked/*' -and $operation -in @('put','delete') -and $hash -eq $global:RouteProofTestExpectedHash -and $operation -eq 'put') { $status=403; $class='forbidden'; $code='ObjectLockedByBucketPolicy' }
            elseif ($key -like 'probe/locked/*' -and $operation -eq 'delete' -and $hash -eq $global:RouteProofTestExpectedHash) { $status=403; $class='forbidden'; $code='ObjectLockedByBucketPolicy' }
            $generation='0123456789abcdef0123456789abcdef'
            if ($operation -eq 'put' -and $key -like 'probe/unlocked/*' -and $hash -eq $global:RouteProofTestChangedHash) { $script:fakeUnlockedDeleted=$false }
            if ($operation -eq 'delete' -and $key -like 'probe/unlocked/*') { $script:fakeUnlockedDeleted=$true }
            $uriSegments=@($key -split '/' | ForEach-Object { [uri]::EscapeDataString($_) })
            $requestUri=([uri]($endpoint+'/osm-artifacts/'+($uriSegments -join '/'))).AbsoluteUri
            $method=$null; $observedUri=$null
            if ($operation -eq 'unsigned-get') { $method='GET'; $observedUri=$requestUri }
            [pscustomobject]@{ Operation=$operation;HttpStatus=$status;StatusClass=$class;S3Code=$code;ByteCount=$bytes;BodySha256=$bodyHash;PrefixSha256=$prefixHash;EofConfirmed=$eof;LimitReached=$false;TimedOut=$false;Redirected=$false;Generation=$generation;Method=$method;TargetUri=$observedUri;HasAuthHeader=$false;SignatureQueryPresent=$false;TargetChangingQueryPresent=$false }
        }
        $script:fakeUnlockedDeleted=$false
        $global:RouteProofTestChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedOriginalHash='bcde92159116d1e36c9f98c9e2657f69d1935db4527e4203aa953f2cd67379b1'
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        Assert ($result.ExitCode -eq 4) 'single-observation prefix exposure was promoted to a terminal capability failure'
        Assert ($result.Records.Count -eq 6 -and $result.Records[2].phase -eq 'unsigned-get' -and $result.Records[3].phase -eq 'recovery-cleanup-delete' -and $result.Records[4].phase -eq 'recovery-cleanup-confirm' -and $result.Records[-1].phase -eq 'complete') ("production loop record count $($result.Records.Count), phases $(($result.Records.phase -join ',') )")
        Assert ($result.Records[4].cleanup -eq 'removed' -and $result.Records[-1].cleanup -eq 'removed') 'cleanup was called removed without NoSuchKey confirmation'
        Assert ($script:fakeUnlockedDeleted) 'failed run did not attempt cleanup'
        $script:RouteProofClock=$null; $script:RouteProofTransport=$null; $script:fakeUnlockedDeleted=$false
    }
    Run 'production loop completes all twelve operations through the JSON boundary' {
        $runId = 'dddddddddddddddddddddddddddddddd'
        $script:Base='0123456789abcdef0123456789abcdef01234567'; $script:Head='fedcba9876543210fedcba9876543210fedcba98'
        $ruleHash = 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        $global:RouteProofTestChangedHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::ASCII.GetBytes("OSM-ROUTE-PROOF:${runId}:changed"))).ToLowerInvariant()
        $script:RouteProofClock = { [TimeSpan]::Zero }
        $script:RouteProofChildRunner = {
            param($endpoint,$operation,$key,$hash,$bytes,$payload,$expectedRunId,$remaining,$deadline)
            if (-not (Test-Key $key $expectedRunId)) { throw 'child key/run mismatch' }
            $status=200; $class='success'; $code=$null; $bodyHash=$hash; $prefixHash=$hash
            if ($operation -eq 'unsigned-get') { $status=400; $class='other'; $code='InvalidArgument'; $bodyHash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'; $prefixHash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' }
            elseif ($operation -eq 'delete' -and $key -like 'probe/unlocked/*') { $status=204 }
            elseif ($key -like 'probe/unlocked/*' -and $operation -eq 'authenticated-get' -and $script:fakeUnlockedDeleted) { $status=404; $class='not-found'; $code='NoSuchKey'; $bodyHash=$null; $prefixHash=$null }
            elseif ($key -like 'probe/locked/*' -and (($operation -eq 'put' -and $hash -eq $global:RouteProofTestChangedHash) -or $operation -eq 'delete')) { $status=409; $class='other'; $code='ObjectLockedByBucketPolicy' }
            elseif ($operation -eq 'authenticated-get' -and $key -like 'probe/locked/*') {
                $script:fakeLockedGetCount++
                if ($script:fakeWrongFinalHash -and $script:fakeLockedGetCount -eq 2) { $bodyHash=$global:RouteProofTestChangedHash; $prefixHash=$global:RouteProofTestChangedHash }
            }
            if ($operation -eq 'delete' -and $key -like 'probe/unlocked/*') { $script:fakeUnlockedDeleted=$true }
            $uriSegments=@($key -split '/' | ForEach-Object { [uri]::EscapeDataString($_) })
            $method=$null; $uri=$null
            if ($operation -eq 'unsigned-get') { $method='GET'; $uri=([uri]($endpoint+'/osm-artifacts/'+($uriSegments -join '/'))).AbsoluteUri }
            $observation=[pscustomobject]@{ Operation=$operation;HttpStatus=$status;StatusClass=$class;S3Code=$code;ByteCount=$bytes;BodySha256=$bodyHash;PrefixSha256=$prefixHash;EofConfirmed=$true;LimitReached=$false;TimedOut=$false;Redirected=$false;Generation='0123456789abcdef0123456789abcdef';Method=$method;TargetUri=$uri;HasAuthHeader=$false;SignatureQueryPresent=$false;TargetChangingQueryPresent=$false }
            # 別pwshが書いた1行を親へ戻し、実際のprocess stdoutとJSON parse/schema検査を通します。
            $jsonLine=$observation | ConvertTo-Json -Compress -Depth 4
            return Invoke-JsonEchoChild $jsonLine
        }
        $script:fakeUnlockedDeleted=$false
        $script:fakeWrongFinalHash=$false; $script:fakeLockedGetCount=0
        $global:RouteProofTestLockedChangedHash=$global:RouteProofTestChangedHash
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        Assert ($result.ExitCode -eq 0) "valid production loop failed: $(($result.Records.phase -join ','))"
        Assert ($result.Records.Count -eq 13 -and $result.Records[-1].phase -eq 'complete') 'full loop did not emit 12 operations plus completion'
        Assert ($result.Records[2].httpStatus -eq 400 -and $result.Records[2].s3Code -ceq 'InvalidArgument') 'unsigned 400 was rewritten in the observation record'
        Assert (($result.Records | Where-Object { $_.phase -ne 'complete' } | Measure-Object).Count -eq 12) 'not all production operations were observed'
        $lockedOverwrite=$result.Records | Where-Object phase -eq 'locked-overwrite' | Select-Object -First 1
        $lockedDelete=$result.Records | Where-Object phase -eq 'locked-delete' | Select-Object -First 1
        $lockedConfirm=$result.Records | Where-Object phase -eq 'locked-confirm' | Select-Object -First 1
        Assert ($lockedOverwrite.httpStatus -eq 409 -and $lockedOverwrite.statusClass -ceq 'other' -and $lockedOverwrite.s3Code -ceq 'ObjectLockedByBucketPolicy') 'locked overwrite did not preserve the 409 tuple'
        Assert ($lockedDelete.httpStatus -eq 409 -and $lockedDelete.statusClass -ceq 'other' -and $lockedDelete.s3Code -ceq 'ObjectLockedByBucketPolicy') 'locked DELETE did not preserve the 409 tuple'
        Assert ($lockedConfirm.observedHash -ceq $lockedConfirm.expectedHash -and $lockedConfirm.observedBytes -eq $lockedConfirm.expectedBytes) 'final authenticated GET did not confirm original bytes and hash'
        Assert (($result.Records | Where-Object { $_.phase -like 'locked-*' -and $_.phase -ne 'locked-confirm' } | Where-Object cleanup -ne 'unconfirmed' | Measure-Object).Count -eq 0) 'intermediate lock operation claimed retention before final GET'
        Assert ($result.Records[-1].cleanup -eq 'removed; locked object retained') 'completed lock verification did not record final retention'
        Assert (@($result.Records | Where-Object { $null -ne $_.lockRule -and $null -ne $_.lockRule.afterHash }).Count -eq 0) 'normal loop invented an After hash'
        $script:fakeUnlockedDeleted=$false; $script:fakeWrongFinalHash=$true; $script:fakeLockedGetCount=0
        $mismatch=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        $failedConfirm=$mismatch.Records | Where-Object phase -eq 'locked-confirm' | Select-Object -First 1
        Assert ($mismatch.ExitCode -eq 4 -and $failedConfirm.result -eq 'inconclusive' -and $mismatch.Records[-1].result -eq 'inconclusive') 'changed final GET hash was accepted as completed lock proof'
        $script:RouteProofClock=$null; $script:RouteProofChildRunner=$null; $script:fakeUnlockedDeleted=$false; $script:fakeWrongFinalHash=$false; $script:fakeLockedGetCount=0
    }
    Run 'monotonic operation start timestamp is comparable in child pwsh' {
        # 親の起動前timestampを子pwshへ引数で渡し、同じ時計基準で起動時間を測れることを実processで確認します。
        $started=[Diagnostics.Stopwatch]::GetTimestamp()
        $command='$start=[long]'+$started.ToString([Globalization.CultureInfo]::InvariantCulture)+'; [Console]::WriteLine([Diagnostics.Stopwatch]::GetElapsedTime($start).TotalMilliseconds.ToString([Globalization.CultureInfo]::InvariantCulture))'
        $encoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))
        $result=Invoke-PwshCapture @('-NoProfile','-EncodedCommand',$encoded)
        $elapsed=[double]::Parse($result.Stdout.Trim(),[Globalization.CultureInfo]::InvariantCulture)
        Assert ($result.ExitCode -eq 0 -and -not $result.Stderr -and $elapsed -ge 0 -and $elapsed -lt 30000) 'parent monotonic timestamp was not safely observed by child pwsh'
    }
    Run 'locked object is unconfirmed when lock operations succeed' {
        $runId='ffffffffffffffffffffffffffffffff'; $script:Base='0123456789abcdef0123456789abcdef01234567'; $script:Head='fedcba9876543210fedcba9876543210fedcba98'
        $global:RouteProofTestChangedHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::ASCII.GetBytes("OSM-ROUTE-PROOF:${runId}:changed"))).ToLowerInvariant()
        $script:RouteProofClock={ [TimeSpan]::Zero }; $script:fakeUnlockedDeleted=$false
        $script:RouteProofChildRunner={
            param($endpoint,$operation,$key,$hash,$bytes,$payload,$expectedRunId,$remaining,$deadline)
            $status=200; $class='success'; $code=$null; $bodyHash=$hash; $prefixHash=$hash
            if($operation -eq 'unsigned-get'){$status=403;$class='forbidden';$code='AccessDenied';$bodyHash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa';$prefixHash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'}
            elseif($operation -eq 'delete' -and $key -like 'probe/unlocked/*'){$status=204;$script:fakeUnlockedDeleted=$true}
            elseif($operation -eq 'authenticated-get' -and $key -like 'probe/unlocked/*' -and $script:fakeUnlockedDeleted){$status=404;$class='not-found';$code='NoSuchKey';$bodyHash=$null;$prefixHash=$null}
            $segments=@($key -split '/' | ForEach-Object {[uri]::EscapeDataString($_)});$method=$null;$uri=$null
            if($operation -eq 'unsigned-get'){$method='GET';$uri=([uri]($endpoint+'/osm-artifacts/'+($segments -join '/'))).AbsoluteUri}
            [pscustomobject]@{Operation=$operation;HttpStatus=$status;StatusClass=$class;S3Code=$code;ByteCount=$bytes;BodySha256=$bodyHash;PrefixSha256=$prefixHash;EofConfirmed=$true;LimitReached=$false;TimedOut=$false;Redirected=$false;Generation='0123456789abcdef0123456789abcdef';Method=$method;TargetUri=$uri;HasAuthHeader=$false;SignatureQueryPresent=$false;TargetChangingQueryPresent=$false}
        }
        $ruleHash='cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        $failedLock=$result.Records | Where-Object phase -eq 'locked-overwrite' | Select-Object -First 1
        Assert ($result.ExitCode -eq 4 -and $failedLock.cleanup -eq 'unconfirmed') 'lock success without re-GET was labeled retained'
        $script:RouteProofClock=$null;$script:RouteProofChildRunner=$null;$script:fakeUnlockedDeleted=$false
    }
    Run 'environment-blocked credential preflight stops before a keyed observation' {
        $runId='abababababababababababababababab'; $script:Base='0123456789abcdef0123456789abcdef01234567'; $script:Head='fedcba9876543210fedcba9876543210fedcba98'
        $script:RouteProofClock={ [TimeSpan]::Zero }
        $script:RouteProofChildRunner={
            param($endpoint,$operation,$key,$hash,$bytes,$payload,$expectedRunId,$remaining,$deadline)
            [pscustomobject]@{ Operation=$operation;HttpStatus=$null;StatusClass='environment-blocked';S3Code=$null;ByteCount=$null;BodySha256=$null;PrefixSha256=$null;EofConfirmed=$false;LimitReached=$false;TimedOut=$false;Redirected=$false;Generation='00000000000000000000000000000000';Method=$null;TargetUri=$null;HasAuthHeader=$false;SignatureQueryPresent=$false;TargetChangingQueryPresent=$false }
        }
        $ruleHash='cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        Assert ($result.ExitCode -eq 3 -and $result.Records[0].result -eq 'environment-blocked') 'missing credential profile was not identified as environment-blocked'
        Assert ($null -eq $result.Records[0].key -and $result.Records.Count -eq 2) "preflight failure exposed a key or continued the route: count=$($result.Records.Count), firstKey=$($result.Records[0].key), phases=$(($result.Records.phase -join ','))"
        $script:RouteProofClock=$null; $script:RouteProofChildRunner=$null
    }
    Run 'ambiguous initial PUT still receives an independent recovery budget' {
        $runId = 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
        $script:Base='offline-base'; $script:Head='offline-head'
        $script:fakeElapsed = [TimeSpan]::FromMinutes(5) - [TimeSpan]::FromSeconds(1)
        $script:RouteProofClock = { $script:fakeElapsed }
        $script:RouteProofChildRunner = {
            param($endpoint,$operation,$key,$hash,$bytes,$payload,$expectedRunId,$remaining,$deadline)
            if ($operation -eq 'put') { $script:fakeElapsed += [TimeSpan]::FromSeconds(2); return $null }
            if ($operation -eq 'authenticated-get') { Assert ($remaining -gt 0 -and $remaining -le 20000) 'cleanup confirmation did not use the remaining shared 30-second budget' }
            else { Assert ($remaining -gt 0 -and $remaining -le 30000) 'recovery cleanup did not receive its separate 30-second budget' }
            $status=204; $class='success'; $code=$null; $bodyHash=$hash; $prefixHash=$hash
            if ($operation -eq 'delete') { $script:fakeElapsed += [TimeSpan]::FromSeconds(10) }
            if ($operation -eq 'authenticated-get') { $status=404; $class='not-found'; $code='NoSuchKey'; $bodyHash=$null; $prefixHash=$null }
            [pscustomobject]@{ Operation=$operation;HttpStatus=$status;StatusClass=$class;S3Code=$code;ByteCount=$bytes;BodySha256=$bodyHash;PrefixSha256=$prefixHash;EofConfirmed=$true;LimitReached=$false;TimedOut=$false;Redirected=$false;Generation='0123456789abcdef0123456789abcdef';Method=$null;TargetUri=$null;HasAuthHeader=$false;SignatureQueryPresent=$false;TargetChangingQueryPresent=$false }
        }
        $ruleHash = 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        Assert ($result.ExitCode -eq 4) 'ambiguous initial PUT was treated as a successful run'
        Assert (($result.Records.phase -join ',') -eq 'unlocked-put,recovery-cleanup-delete,recovery-cleanup-confirm,complete') "unexpected recovery record order: $(($result.Records.phase -join ','))"
        Assert ($result.Records[-1].cleanup -eq 'removed') 'ambiguous PUT cleanup was not confirmed'
        $script:RouteProofClock=$null; $script:RouteProofChildRunner=$null
    }
    Run 'unconfirmed child termination suppresses racing recovery cleanup' {
        $runId='eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee'; $script:Base='offline-base'; $script:Head='offline-head'
        $script:RouteProofClock={ [TimeSpan]::Zero }
        $script:RouteProofChildRunner={ param($endpoint,$operation) $script:ChildTerminationConfirmed=$false; return $null }
        $ruleHash='cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        Assert (($result.Records.phase -join ',') -eq 'unlocked-put,recovery-cleanup,complete') 'cleanup raced a child whose termination was not confirmed'
        Assert ($result.Records[-1].cleanup -eq 'unconfirmed') 'unconfirmed child cleanup was reported removed'
        $script:RouteProofClock=$null; $script:RouteProofChildRunner=$null
    }
} catch { $script:failed.Add('test harness') }

if ($ResultPath) {
    $result = [ordered]@{ registered = @($script:registered); selected = @($script:selected); executed = @($script:executed); failed = @($script:failed) }
    [IO.File]::WriteAllText($ResultPath, (ConvertTo-Json -InputObject $result -Depth 5), [Text.UTF8Encoding]::new($false))
}
if ($script:failed.Count -gt 0) {
    [Console]::Error.WriteLine("RouteProof tests failed: $($script:failed -join ', ')")
    exit 1
}
[Console]::WriteLine("RouteProof tests passed: $($script:passed.Count)")
exit 0
        $global:RouteProofTestChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedOriginalHash='bcde92159116d1e36c9f98c9e2657f69d1935db4527e4203aa953f2cd67379b1'
