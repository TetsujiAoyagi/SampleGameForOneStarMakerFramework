param([string[]] $Case = @('*'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '../Probe/RouteProof.ps1') -Library
$script:passed = [Collections.Generic.List[string]]::new()
$script:failed = [Collections.Generic.List[string]]::new()

function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
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
    [object] $RequestMethod = 'GET',
    [object] $RequestUri = $null,
    [bool] $AuthorizationPresent = $false,
    [bool] $SignatureQueryPresent = $false,
    [bool] $TargetChangingQueryPresent = $false,
    [string] $Generation = '0123456789abcdef0123456789abcdef') {
    if ($null -eq $RequestUri) { $RequestUri = 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com/osm-artifacts/probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt' }
    [pscustomobject][ordered]@{
        Operation = $Operation; HttpStatus = $HttpStatus; StatusClass = $StatusClass; S3Code = $S3Code
        ByteCount = $ByteCount; BodySha256 = $BodySha256; PrefixSha256 = $PrefixSha256
        EofConfirmed = $EofConfirmed; LimitReached = $LimitReached; TimedOut = $TimedOut
        Redirected = $Redirected; Generation = $Generation
        RequestMethod = $RequestMethod; RequestUri = $RequestUri
        AuthorizationPresent = $AuthorizationPresent; SignatureQueryPresent = $SignatureQueryPresent; TargetChangingQueryPresent = $TargetChangingQueryPresent
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
    }
    Run 'unsigned exact object is not private' {
        $script:ActiveEndpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'; $script:ActiveKey='probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt'
        $fake = Fake-Observation -BodySha256 $global:RouteProofTestExpectedHash -PrefixSha256 $global:RouteProofTestExpectedHash -ByteCount 23 -RequestMethod $null -RequestUri $null
        Assert (-not (Test-UnsignedPrivacy $fake $global:RouteProofTestExpectedHash 23)) 'exact object exposure accepted'
    }
    Run 'unsigned object plus bytes is not private' {
        $script:ActiveEndpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'; $script:ActiveKey='probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt'
        $fake = Fake-Observation -BodySha256 $global:RouteProofTestExpectedHash -PrefixSha256 $global:RouteProofTestExpectedHash -ByteCount 29 -RequestMethod $null -RequestUri $null
        Assert (-not (Test-UnsignedPrivacy $fake $global:RouteProofTestExpectedHash 23)) 'appended object exposure accepted'
    }
    Run 'unsigned rejection requires EOF, code and different hash' {
        $script:ActiveEndpoint = 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'
        $script:ActiveKey = 'probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt'
        Assert (Test-UnsignedPrivacy (Fake-Observation -BodySha256 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa') $global:RouteProofTestExpectedHash 23) 'valid denial rejected'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -S3Code 'SignatureDoesNotMatch') $global:RouteProofTestExpectedHash 23)) 'signature mismatch accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -LimitReached $true) $global:RouteProofTestExpectedHash 23)) 'bounded body accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -EofConfirmed $false) $global:RouteProofTestExpectedHash 23)) 'non-EOF body accepted'
        $sameBodyPrefixDiffers = Fake-Observation -HttpStatus 400 -StatusClass 'other' -S3Code 'InvalidArgument' -BodySha256 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' -PrefixSha256 'cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc'
        Assert (-not (Test-UnsignedPrivacy $sameBodyPrefixDiffers $global:RouteProofTestExpectedHash 23)) '400 InvalidArgument was treated as an allowed private denial'
        $unsignedWithAuth = Fake-Observation -AuthorizationPresent $true
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
        $script:RouteProofChildRunner = {
            param($endpoint,$operation,$key,$hash,$bytes,$payload,$expectedRunId,$remaining)
            if ($remaining -gt 300000) { throw 'run deadline exceeded' }
            if (-not (Test-Key $key $expectedRunId)) { throw 'child key/run mismatch' }
            $status = 200; $class = 'success'; $code = $null; $bodyHash = $hash; $prefixHash = $hash; $eof = $true
            if ($operation -eq 'unsigned-get') { $status=403; $class='forbidden'; $code='AccessDenied'; $bodyHash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'; $prefixHash='aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa' }
            elseif ($operation -eq 'authenticated-get' -and $key -like 'probe/unlocked/*' -and $script:fakeUnlockedDeleted -and $hash -eq $global:RouteProofTestChangedHash) { $status=404; $class='not-found'; $code='NoSuchKey'; $bodyHash=$null; $prefixHash=$null }
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
            [pscustomobject]@{ Operation=$operation;HttpStatus=$status;StatusClass=$class;S3Code=$code;ByteCount=$bytes;BodySha256=$bodyHash;PrefixSha256=$prefixHash;EofConfirmed=$eof;LimitReached=$false;TimedOut=$false;Redirected=$false;Generation=$generation;RequestMethod=$method;RequestUri=$observedUri;AuthorizationPresent=$false;SignatureQueryPresent=$false;TargetChangingQueryPresent=$false }
        }
        $script:fakeUnlockedDeleted=$false
        $global:RouteProofTestChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedOriginalHash='bcde92159116d1e36c9f98c9e2657f69d1935db4527e4203aa953f2cd67379b1'
        $json=[pscustomobject]@{Prefix='probe/locked/';Enabled=$true;Kind='Age';RetentionSeconds=900;RuleCount=1;DateRules=0;IndefiniteRules=0;WriterCanConfigure=$false;LifecycleCompatible=$true;BeforeHash=$ruleHash;AfterHash=$ruleHash}|ConvertTo-Json -Compress
        $result=Invoke-RouteProofLoop 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com' $runId $json
        Assert ($result.ExitCode -eq 2) 'prefix exposure was not classified as a provider capability failure'
        Assert ($result.Records.Count -eq 11 -and $result.Records[2].phase -eq 'unsigned-get' -and $result.Records[-1].phase -eq 'complete') ("production loop record count $($result.Records.Count), phases $(($result.Records.phase -join ',') )")
        Assert ($script:fakeUnlockedDeleted) 'failed run did not attempt cleanup'
        $script:RouteProofClock=$null; $script:RouteProofChildRunner=$null; $script:fakeUnlockedDeleted=$false
    }
} catch { $script:failed.Add('test harness') }

if ($script:failed.Count -gt 0) {
    [Console]::Error.WriteLine("RouteProof tests failed: $($script:failed -join ', ')")
    exit 1
}
[Console]::WriteLine("RouteProof tests passed: $($script:passed.Count)")
exit 0
        $global:RouteProofTestChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedChangedHash='efc900f91b1b41cf7ad4e882d7f697a1b05503dc58bc7f51af2d2a271d60ba33'; $global:RouteProofTestLockedOriginalHash='bcde92159116d1e36c9f98c9e2657f69d1935db4527e4203aa953f2cd67379b1'
