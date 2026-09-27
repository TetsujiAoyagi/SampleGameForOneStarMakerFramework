param([string[]] $Case = @('*'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot '../Probe/RouteProof.ps1') -Library
$script:passed = [Collections.Generic.List[string]]::new()
$script:failed = [Collections.Generic.List[string]]::new()

function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Run([string] $Name, [scriptblock] $Body) {
    if (-not (@($Case | Where-Object { $Name -like $_ }).Count -gt 0)) { return }
    try { & $Body; $script:passed.Add($Name) } catch { $script:failed.Add($Name) }
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
    [string] $Generation = '0123456789abcdef0123456789abcdef') {
    [pscustomobject][ordered]@{
        Operation = $Operation; HttpStatus = $HttpStatus; StatusClass = $StatusClass; S3Code = $S3Code
        ByteCount = $ByteCount; BodySha256 = $BodySha256; PrefixSha256 = $PrefixSha256
        EofConfirmed = $EofConfirmed; LimitReached = $LimitReached; TimedOut = $TimedOut
        Redirected = $Redirected; Generation = $Generation
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
    }
    Run 'unsigned exact object is not private' {
        $fake = Fake-Observation -BodySha256 $global:RouteProofTestExpectedHash -PrefixSha256 $global:RouteProofTestExpectedHash -ByteCount 23
        Assert (-not (Test-UnsignedPrivacy $fake $global:RouteProofTestExpectedHash 23)) 'exact object exposure accepted'
    }
    Run 'unsigned object plus bytes is not private' {
        $fake = Fake-Observation -BodySha256 $global:RouteProofTestExpectedHash -PrefixSha256 $global:RouteProofTestExpectedHash -ByteCount 29
        Assert (-not (Test-UnsignedPrivacy $fake $global:RouteProofTestExpectedHash 23)) 'appended object exposure accepted'
    }
    Run 'unsigned rejection requires EOF, code and different hash' {
        Assert (Test-UnsignedPrivacy (Fake-Observation -BodySha256 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa') $global:RouteProofTestExpectedHash 23) 'valid denial rejected'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -S3Code 'SignatureDoesNotMatch') $global:RouteProofTestExpectedHash 23)) 'signature mismatch accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -LimitReached $true) $global:RouteProofTestExpectedHash 23)) 'bounded body accepted'
        Assert (-not (Test-UnsignedPrivacy (Fake-Observation -EofConfirmed $false) $global:RouteProofTestExpectedHash 23)) 'non-EOF body accepted'
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
} catch { $script:failed.Add('test harness') }

if ($script:failed.Count -gt 0) {
    [Console]::Error.WriteLine("RouteProof tests failed: $($script:failed -join ', ')")
    exit 1
}
[Console]::WriteLine("RouteProof tests passed: $($script:passed.Count)")
exit 0
