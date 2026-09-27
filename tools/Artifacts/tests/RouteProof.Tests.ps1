param([string[]] $Case = @('*'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '../Probe/RouteProof.ps1') -Library
$script:passed = [Collections.Generic.List[string]]::new()
$script:failed = [Collections.Generic.List[string]]::new()

function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Invoke-PwshCapture([string[]] $Arguments) {
    $info=[Diagnostics.ProcessStartInfo]::new(); $info.FileName='pwsh'; $info.UseShellExecute=$false
    $info.RedirectStandardOutput=$true; $info.RedirectStandardError=$true; $info.CreateNoWindow=$true
    foreach($argument in $Arguments){ [void]$info.ArgumentList.Add($argument) }
    $process=[Diagnostics.Process]::new(); $process.StartInfo=$info
    try {
        if(-not $process.Start()){ throw 'JSON child process did not start' }
        $stdoutTask=$process.StandardOutput.ReadToEndAsync(); $stderrTask=$process.StandardError.ReadToEndAsync()
        if(-not $process.WaitForExit(10000)){ try{$process.Kill($true)}catch{}; throw 'JSON child process exceeded its test deadline' }
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
    try { & $Body; $script:passed.Add($Name) } catch { $script:failed.Add($Name + ': ' + $_.Exception.Message) }
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
        Assert (Test-Key 'probe/unlocked/0123456789abcdef0123456789abcdef/object.txt') 'valid key rejected'
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
        $sameBodyPrefixDiffers = Fake-Observation -HttpStatus 400 -StatusClass 'other' -S3Code 'InvalidArgument' -BodySha256 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' -PrefixSha256 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        Assert (-not (Test-UnsignedPrivacy $sameBodyPrefixDiffers $global:RouteProofTestExpectedHash 23)) '400 InvalidArgument was treated as an allowed private denial'
        $unsignedWithAuth = Fake-Observation -HasAuthHeader $true
        Assert (-not (Test-UnsignedPrivacy $unsignedWithAuth $global:RouteProofTestExpectedHash 23)) 'signed request was treated as unsigned'
    }
    Run 'lock requires exact provider code' {
        Assert (Test-LockRejection (Fake-Observation -Operation 'put' -S3Code 'ObjectLockedByBucketPolicy')) 'lock rejection rejected'
        Assert (-not (Test-LockRejection (Fake-Observation -Operation 'put' -S3Code 'AccessDenied'))) 'generic 403 accepted'
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
        Assert ($remaining -eq 5000) 'prior operation time consumed the child pipe budget'
        $script:RouteProofClock = { [TimeSpan]::FromSeconds(46) }
        $expired = Get-RemainingPipeWait 45000 ([TimeSpan]::Zero)
        Assert ($expired -eq 0) 'expired child budget remained available'
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
    Run 'valid lock rule is accepted and date rules are refused' {
        $hash = 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        $json = [pscustomobject]@{ Prefix='probe/locked/'; Enabled=$true; Kind='Age'; RetentionSeconds=900; RuleCount=1; DateRules=0; IndefiniteRules=0; WriterCanConfigure=$false; LifecycleCompatible=$true; BeforeHash=$hash; AfterHash=$hash } | ConvertTo-Json -Compress
        $rule = Read-LockRule $json
        Assert ($rule.prefix -eq 'probe/locked/' -and $rule.retentionSeconds -eq 900) 'valid rule rejected'
        $bad = $json.Replace('"DateRules":0','"DateRules":1')
        $rejected = $false
        try { Read-LockRule $bad } catch { $rejected = $true }
        Assert $rejected 'date rule accepted'
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
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash;AfterHash=$ruleHash}|ConvertTo-Json -Compress
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
            if ($operation -eq 'unsigned-get') { $status=403; $class='forbidden'; $code='AccessDenied'; $bodyHash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'; $prefixHash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' }
            elseif ($operation -eq 'delete' -and $key -like 'probe/unlocked/*') { $status=204 }
            elseif ($key -like 'probe/unlocked/*' -and $operation -eq 'authenticated-get' -and $script:fakeUnlockedDeleted) { $status=404; $class='not-found'; $code='NoSuchKey'; $bodyHash=$null; $prefixHash=$null }
            elseif ($key -like 'probe/locked/*' -and (($operation -eq 'put' -and $hash -eq $global:RouteProofTestChangedHash) -or $operation -eq 'delete')) { $status=403; $class='forbidden'; $code='ObjectLockedByBucketPolicy' }
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
        $global:RouteProofTestLockedChangedHash=$global:RouteProofTestChangedHash
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash;AfterHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        Assert ($result.ExitCode -eq 0) "valid production loop failed: $(($result.Records.phase -join ','))"
        Assert ($result.Records.Count -eq 13 -and $result.Records[-1].phase -eq 'complete') 'full loop did not emit 12 operations plus completion'
        Assert (($result.Records | Where-Object { $_.phase -ne 'complete' } | Measure-Object).Count -eq 12) 'not all production operations were observed'
        Assert (($result.Records | Where-Object { $_.phase -like 'locked-*' -and $_.phase -ne 'locked-confirm' } | Where-Object cleanup -ne 'unconfirmed' | Measure-Object).Count -eq 0) 'intermediate lock operation claimed retention before final GET'
        Assert ($result.Records[-1].cleanup -eq 'removed; locked object retained') 'completed lock verification did not record final retention'
        $script:RouteProofClock=$null; $script:RouteProofChildRunner=$null; $script:fakeUnlockedDeleted=$false
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
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash;AfterHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        $failedLock=$result.Records | Where-Object phase -eq 'locked-overwrite' | Select-Object -First 1
        Assert ($result.ExitCode -eq 4 -and $failedLock.cleanup -eq 'unconfirmed') 'lock success without re-GET was labeled retained'
        $script:RouteProofClock=$null;$script:RouteProofChildRunner=$null;$script:fakeUnlockedDeleted=$false
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
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash;AfterHash=$ruleHash}|ConvertTo-Json -Compress
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
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash;AfterHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        Assert (($result.Records.phase -join ',') -eq 'unlocked-put,recovery-cleanup,complete') 'cleanup raced a child whose termination was not confirmed'
        Assert ($result.Records[-1].cleanup -eq 'unconfirmed') 'unconfirmed child cleanup was reported removed'
        $script:RouteProofClock=$null; $script:RouteProofChildRunner=$null
    }
} catch { $script:failed.Add('test harness') }

if ($script:failed.Count -gt 0) {
    [Console]::Error.WriteLine("RouteProof tests failed: $($script:failed -join ', ')")
    exit 1
}
[Console]::WriteLine("RouteProof tests passed: $($script:passed.Count)")
exit 0
        $global:RouteProofTestChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedOriginalHash='bcde92159116d1e36c9f98c9e2657f69d1935db4527e4203aa953f2cd67379b1'
