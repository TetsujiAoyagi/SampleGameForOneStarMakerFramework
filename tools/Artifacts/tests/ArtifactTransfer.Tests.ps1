param([string] $ResultPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$app = Import-Module (Join-Path $PSScriptRoot '../ArtifactApplication.psm1') -Force -PassThru
$store = $app.NestedModules | Where-Object Name -eq 'CredentialStore' | Select-Object -First 1
$paths = $app.NestedModules | Where-Object Name -eq 'ArtifactPaths' | Select-Object -First 1
$root = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'osm-transfer-test-' + [Guid]::NewGuid().ToString('N'))
$credentialRoot = [IO.Path]::Combine($root,'credentials')
$transferRoot = [IO.Path]::Combine($root,'transfers')
$cases = @('transport-config','credential-callback-dto','publish-fetch-roundtrip','wrong-outer-hash','lock-witness-refusal','readback-closed-mismatch','readback-child-protocol','observation-persistence-fail-closed','readback-stop-budget','strict-config-and-reference')
$executed = [Collections.Generic.List[string]]::new(); $failed = [Collections.Generic.List[string]]::new()
$script:objects = @{}; $script:lockedWrites = 0
$script:transportIdentity = $null; $script:blockObservationPersist = $false; $script:badReadback = $false
function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Write-Json([string] $Path, $Value) { [IO.File]::WriteAllText($Path,(ConvertTo-Json -InputObject $Value -Compress -Depth 8),[Text.UTF8Encoding]::new($false)) }
function New-Observation([string] $Operation,[string] $Generation,[int] $Status,[string] $Class,[string] $Code,
    [long] $Bytes = 0,[string] $Hash = '') {
    return [pscustomobject]@{ Operation=$Operation; Generation=$Generation; HttpStatus=$Status; StatusClass=$Class
        S3Code=if($Code){$Code}else{$null}; Bytes=if($Operation -eq 'get'){$Bytes}else{$null}
        Sha256=if($Operation -eq 'get'){$Hash}else{$null}; StatusObserved=$true; TimedOut=$false; Redirected=$false }
}
function New-Fixture([string] $Name) {
    $work=[IO.Path]::Combine($root,$Name)
    [IO.Directory]::CreateDirectory($work) | Out-Null
    $source=[IO.Path]::Combine($work,'source'); [IO.Directory]::CreateDirectory($source) | Out-Null
    [IO.File]::WriteAllText([IO.Path]::Combine($source,'data.txt'),'synthetic-transfer-'+$Name)
    $input=[IO.Path]::Combine($work,'input.json')
    Write-Json $input ([ordered]@{schemaVersion=1;purpose='synthetic';root=$source;files=@('data.txt')})
    $settings=[IO.Path]::Combine($work,'settings.json')
    $observed=[DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o')
    $endpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'
    Write-Json $settings ([ordered]@{schemaVersion=1;endpoint=$endpoint;bucket='osm-artifacts';prefix='probe/locked/'
        retentionSeconds=86400;publicDevelopmentUrlEnabled=$false;customDomainCount=0;writerCanConfigure=$false
        bucketScopedWriter=$true;lockEnabled=$true;ageRuleCount=1;dateRuleCount=0;indefiniteRuleCount=0
        lifecycleCompatible=$true;observedAt=$observed;observer='offline-test'})
    $settingsHash=(Get-FileHash -Algorithm SHA256 -LiteralPath $settings).Hash.ToLowerInvariant()
    $config=[IO.Path]::Combine($work,'config.json')
    Write-Json $config ([ordered]@{schemaVersion=1;profile='osm';endpoint=$endpoint;bucket='osm-artifacts'
        repositoryId=('c'*64);prefix='probe/locked/';retentionSeconds=86400;observedAt=$observed
        validUntil=[DateTimeOffset]::UtcNow.AddHours(1).ToString('o');settingsEvidencePath=$settings
        settingsEvidenceSha256=$settingsHash})
    return @{Input=$input;Config=$config;Settings=$settings;Work=$work}
}
$transport = {
    param($generation,$request)
    $key=$request.Key
    if($request.Operation -eq 'put') {
        if($script:blockObservationPersist -and $key.StartsWith('probe/locked/')) {
            [IO.Directory]::CreateDirectory([IO.Path]::Combine([IO.Path]::GetDirectoryName($request.SourcePath),'operation-observations.json')) | Out-Null
        }
        if($key.StartsWith('probe/locked/')) {
            $script:lockedWrites++
            if($script:lockedWrites -gt 1) { return New-Observation 'put' $generation 403 'forbidden' 'ObjectLockedByBucketPolicy' }
        }
        $script:objects[$key]=[IO.File]::ReadAllBytes($request.SourcePath)
        return New-Observation 'put' $generation 200 'success' ''
    }
    if($request.Operation -eq 'delete') {
        if($key.StartsWith('probe/locked/')) { return New-Observation 'delete' $generation 409 'other' 'ObjectLockedByBucketPolicy' }
        $script:objects.Remove($key)
        return New-Observation 'delete' $generation 204 'success' ''
    }
    if(-not $script:objects.ContainsKey($key)) { return New-Observation 'get' $generation 404 'not-found' 'NoSuchKey' }
    $bytes=$script:objects[$key]
    if($request.DestinationPath) { [IO.File]::WriteAllBytes($request.DestinationPath,$bytes) }
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    return New-Observation 'get' $generation 200 'success' '' $bytes.Length $hash
}
$readback = {
    param($operationRoot,$config,$generation,$key,$bytes,$hash)
    if(-not $script:objects.ContainsKey($key)) { throw 'missing readback' }
    $data=$script:objects[$key]
    $actual=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($data)).ToLowerInvariant()
    if($script:badReadback) { return [pscustomobject]@{operation='readback';generation=$generation;key=$key;bytes=[long]$data.Length
        sha256=('0'*64);httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false
        childExit=1;childStatus='failed';observedAt=[DateTimeOffset]::UtcNow.ToString('o')} }
    if($data.Length -ne $bytes -or $actual -cne $hash) { throw 'readback mismatch' }
    return [pscustomobject]@{operation='readback';generation=$generation;key=$key;bytes=[long]$data.Length
        sha256=$actual;httpStatus=200;statusClass='success';s3Code=$null;statusObserved=$true;timedOut=$false;redirected=$false
        childExit=0;childStatus='passed';observedAt=[DateTimeOffset]::UtcNow.ToString('o')}
}
try {
    & $store {param($p) $script:TestRoot=$p; $script:TestBase=$null} $credentialRoot
    & $paths {param($p) $script:TestRoot=$p} $transferRoot
    & $app {param($t,$r) $script:TransportHook=$t; $script:ReadbackHook=$r} $transport $readback
    $null = & $store { Write-CredentialRecord 'osm' 'DUMMY_TRANSFER_ID' 'DUMMY_TRANSFER_SECRET' $false }
    foreach($case in $cases) {
        $script:objects=@{}; $script:lockedWrites=0; $script:blockObservationPersist=$false; $script:badReadback=$false
        try {
            $fixture=New-Fixture $case
            if($case -eq 'publish-fetch-roundtrip') { $null = & $app {param($p) Read-ArtifactConfig $p} $fixture.Config }
            switch($case) {
                'transport-config' {
                    $dll=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Transport/artifacts/transport/R2ArtifactTransport.dll'))
                    Assert ([IO.File]::Exists($dll)) 'dedicated R2 transport DLL missing'
                    $assembly=[Reflection.Assembly]::LoadFrom($dll)
                    Assert ($assembly.Location.Equals($dll,[StringComparison]::OrdinalIgnoreCase)) 'wrong transport DLL loaded'
                    $script:transportIdentity=[ordered]@{loadedPath=$assembly.Location
                        loadedHashBefore=(Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash.ToLowerInvariant()
                        moduleVersionId=$assembly.ManifestModule.ModuleVersionId.ToString()}
                    $type=$assembly.GetType('OneStarMaker.Artifacts.Transport.R2ArtifactTransport',$true)
                    $factory=$type.GetMethod('CreateClient',[Reflection.BindingFlags]'NonPublic,Static')
                    $client=$factory.Invoke($null,[object[]]@('DUMMY_ID','DUMMY_SECRET','https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'))
                    try {
                        Assert (-not $client.Config.AllowAutoRedirect -and $client.Config.MaxErrorRetry -eq 0) 'transport redirect/retry config'
                        Assert ($client.Config.HttpClientFactory.GetType().Name -ceq 'RejectRedirectFactory') 'redirect guard factory absent'
                    } finally { $client.Dispose() }
                    $invalid=$type.GetMethod('Execute').Invoke($null,[object[]]@('DUMMY_ID','DUMMY_SECRET','https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com','list','probe/locked/invalid',$null,$null,1000))
                    Assert ($invalid.StatusClass -ceq 'invalid-input') 'generic operation unexpectedly enabled'
                }
                'credential-callback-dto' {
                    $dto=New-Observation 'get' ('a'*32) 200 'success' '' 3 ('b'*64)
                    $accepted=& $store {
                        param($value)
                        $record=[pscustomobject]@{AccessKeyId='DUMMY_CALLBACK_ID';SecretAccessKey='DUMMY_CALLBACK_SECRET';Generation=('a'*32)}
                        $callback={param($id,$secret,$generation,$request) $value}.GetNewClosure()
                        Invoke-RestrictedTransport $record $callback @{Operation='get'}
                    } $dto
                    Assert ($accepted.StatusObserved -eq $true -and $accepted.Sha256 -ceq ('b'*64)) 'closed production DTO refused'
                    $unsafe=[pscustomobject]@{Operation='get';StatusObserved=$true;SdkResponse='not-safe'}
                    $rejected=$false
                    try { & $store {param($value) $record=[pscustomobject]@{AccessKeyId='DUMMY_ID';SecretAccessKey='DUMMY_SECRET';Generation=('a'*32)}; $callback={param($id,$secret,$generation,$request) $value}.GetNewClosure(); Invoke-RestrictedTransport $record $callback @{Operation='get'}} $unsafe | Out-Null }
                    catch { $rejected=$true }
                    Assert $rejected 'SDK response field accepted'
                }
                'publish-fetch-roundtrip' {
                    $published=Invoke-ArtifactPublish $fixture.Config $fixture.Input ('a'*40) ('b'*40)
                    Assert ($published.status -ceq 'passed' -and $published.ledger -ne $null) ("publish failed: " + (ConvertTo-Json -InputObject $published -Compress -Depth 8))
                    $receipt=Get-Content -Raw -LiteralPath $published.ledger.protectionReceiptPath | ConvertFrom-Json
                    Assert ($receipt.observations.Count -eq 9) 'receipt lacks both child readbacks and control observations'
                    Assert ($receipt.putStartedAt -and $receipt.packageSha256 -ceq $published.ledger.packageSha256) 'receipt identity'
                    $fetched=Invoke-ArtifactFetch $fixture.Config $published.ledger.reference $published.ledger.packageSha256
                    Assert ($fetched.status -ceq 'passed') 'fetch failed'
                    Assert ([IO.File]::ReadAllText([IO.Path]::Combine($fetched.outputPath,'data.txt')) -ceq 'synthetic-transfer-'+$case) 'fetched bytes'
                }
                'wrong-outer-hash' {
                    $published=Invoke-ArtifactPublish $fixture.Config $fixture.Input ('a'*40) ('b'*40)
                    Assert ($published.status -ceq 'passed') ("publish fixture failed: " + (ConvertTo-Json -InputObject $published -Compress -Depth 8))
                    $fetched=Invoke-ArtifactFetch $fixture.Config $published.ledger.reference ('0'*64)
                    Assert ($fetched.status -ceq 'failed' -and $null -eq $fetched.outputPath) 'wrong hash became ready'
                }
                'lock-witness-refusal' {
                    $script:lockedWrites=1
                    $published=Invoke-ArtifactPublish $fixture.Config $fixture.Input ('a'*40) ('b'*40)
                    Assert ($published.status -ceq 'failed' -and $null -eq $published.ledger) 'lock witness failed open'
                    $raw=Get-Content -Raw -LiteralPath ([IO.Path]::Combine($published.residue.localPath,'operation-observations.json')) | ConvertFrom-Json
                    Assert ($raw.observations.Count -eq 1 -and $raw.observations[0].httpStatus -eq 403 -and
                        $raw.observations[0].s3Code -ceq 'ObjectLockedByBucketPolicy') 'failed publish lost HTTP/code raw'
                }
                'readback-closed-mismatch' {
                    $script:badReadback=$true
                    $published=Invoke-ArtifactPublish $fixture.Config $fixture.Input ('a'*40) ('b'*40)
                    Assert ($published.status -ceq 'failed' -and $null -eq $published.ledger) 'mismatched child readback passed'
                    $raw=Get-Content -Raw -LiteralPath ([IO.Path]::Combine($published.residue.localPath,'operation-observations.json')) | ConvertFrom-Json
                    Assert ($raw.observations.Count -eq 2 -and $raw.observations[1].sha256 -ceq ('0'*64) -and
                        $raw.observations[1].httpStatus -eq 200 -and $raw.observations[1].statusClass -ceq 'success' -and
                        $raw.observations[1].childExit -eq 1 -and $raw.observations[1].childStatus -ceq 'failed') 'closed failed child facts lost'
                }
                'readback-child-protocol' {
                    $child=[IO.Path]::Combine($fixture.Work,'synthetic-child.ps1')
                    $source=@'
param([string] $RequestPath)
$data=Get-Content -Raw -LiteralPath $RequestPath | ConvertFrom-Json
$value=[ordered]@{schemaVersion=1;operation='get';generation=$data.generation;key=$data.key
    bytes=[long]12;sha256=('0'*64);httpStatus=403;statusClass='forbidden';s3Code='AccessDenied'
    statusObserved=$true;timedOut=$false;redirected=$false;status='failed'}
[Console]::WriteLine((ConvertTo-Json -InputObject $value -Compress))
exit 1
'@
                    [IO.File]::WriteAllText($child,$source,[Text.UTF8Encoding]::new($false))
                    $defaultChild=& $app { $script:ReadbackChildPath }
                    $operationRoot=& $app { New-ArtifactOperation }
                    try {
                        & $app {param($path) $script:ReadbackHook=$null; $script:ReadbackChildPath=$path} $child
                        $config=& $app {param($path) Read-ArtifactConfig $path} $fixture.Config
                        $timer=[Diagnostics.Stopwatch]::StartNew()
                        $key='probe/locked/'+('c'*64)+'/'+('b'*40)+'/'+('d'*32)+'/bundle.zip'
                        $actual=& $app {param($root,$cfg,$gen,$name,$watch) Invoke-ArtifactReadback $root $cfg $gen $name 12 ('a'*64) $watch} $operationRoot $config ('e'*32) $key $timer
                        Assert ($actual.childExit -eq 1 -and $actual.childStatus -ceq 'failed' -and
                            $actual.httpStatus -eq 403 -and $actual.s3Code -ceq 'AccessDenied' -and
                            $actual.bytes -eq 12 -and $actual.sha256 -ceq ('0'*64)) 'closed child status tuple lost'
                    } finally { & $app {param($hook,$path) $script:ReadbackHook=$hook; $script:ReadbackChildPath=$path} $readback $defaultChild }
                }
                'observation-persistence-fail-closed' {
                    $script:blockObservationPersist=$true
                    $published=Invoke-ArtifactPublish $fixture.Config $fixture.Input ('a'*40) ('b'*40)
                    Assert ($published.status -ceq 'failed' -and $published.reasonCode -ceq 'result-persistence-unconfirmed' -and
                        $null -eq $published.ledger) 'raw persistence failure passed'
                }
                'readback-stop-budget' {
                    $script:clockValues=[Collections.Generic.Queue[long]]::new()
                    $script:clockValues.Enqueue(0L); $script:clockValues.Enqueue(0L)
                    $script:clockValues.Enqueue([long]([Diagnostics.Stopwatch]::Frequency*14))
                    $script:exitBudget=0; $script:pipeBudget=0; $script:stopped=$false
                    $stop={ $script:stopped=$true }
                    $exit={param($ms) $script:exitBudget=$ms; $true}
                    $pipes={param($ms) $script:pipeBudget=$ms; $true}
                    $clock={ $script:clockValues.Dequeue() }
                    $remaining={ 20000 }
                    $expired=$false
                    try { & $app {param($s,$e,$p,$c,$r) Invoke-ReadbackStop $s $e $p $c $r} $stop $exit $pipes $clock $remaining } catch { $expired=$true }
                    Assert ($expired -and $script:stopped -and $script:exitBudget -eq 15000 -and
                        $script:pipeBudget -le 1000 -and $script:pipeBudget -gt 0) 'stop and pipe waits used separate budgets'
                    $script:stopped=$false; $script:exitBudget=0; $script:pipeBudget=0
                    $script:clockValues=[Collections.Generic.Queue[long]]::new(); $script:clockValues.Enqueue(0L)
                    $zero={ 0 }
                    $expired=$false
                    try { & $app {param($s,$e,$p,$c,$r) Invoke-ReadbackStop $s $e $p $c $r} $stop $exit $pipes $clock $zero } catch { $expired=$true }
                    Assert ($expired -and $script:stopped -and $script:exitBudget -eq 0 -and $script:pipeBudget -eq 0) 'zero budget skipped child stop or extended wait'
                    $script:stopped=$false; $script:exitBudget=0; $script:pipeBudget=0
                    $script:clockValues=[Collections.Generic.Queue[long]]::new()
                    $script:clockValues.Enqueue(0L); $script:clockValues.Enqueue(0L); $script:clockValues.Enqueue(0L)
                    $script:remainingValues=[Collections.Generic.Queue[int]]::new()
                    $script:remainingValues.Enqueue(500); $script:remainingValues.Enqueue(500); $script:remainingValues.Enqueue(200)
                    $limited={ $script:remainingValues.Dequeue() }
                    $expired=$false
                    try { & $app {param($s,$e,$p,$c,$r) Invoke-ReadbackStop $s $e $p $c $r} $stop $exit $pipes $clock $limited } catch { $expired=$true }
                    Assert ($expired -and $script:stopped -and $script:exitBudget -eq 500 -and $script:pipeBudget -eq 200) 'operation budget was not shared with stop and pipes'
                }
                'strict-config-and-reference' {
                    $raw=[IO.File]::ReadAllText($fixture.Config)
                    [IO.File]::WriteAllText($fixture.Config,$raw.Replace('"schemaVersion":1','"schemaVersion":1,"schemaVersion":1'))
                    $published=Invoke-ArtifactPublish $fixture.Config $fixture.Input ('a'*40) ('b'*40)
                    Assert ($published.status -ceq 'failed' -and $null -eq $published.ledger) 'duplicate config field accepted'
                }
            }
            $executed.Add($case)
        } catch { $failed.Add($case+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message) }
    }
    if($ResultPath) {
        $after = if($script:transportIdentity){(Get-FileHash -Algorithm SHA256 -LiteralPath $script:transportIdentity.loadedPath).Hash.ToLowerInvariant()}else{$null}
        $result=[ordered]@{registered=$cases;selected=$cases;executed=@($executed);failed=@($failed)
            transport=$script:transportIdentity;loadedHashAfter=$after}
        [IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -InputObject $result -Depth 6),[Text.UTF8Encoding]::new($false))
    }
    foreach($item in $executed){"PASS $item"}; foreach($item in $failed){"FAIL $item"}
    "Cases: $($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -gt 0 -or $executed.Count -ne $cases.Count){exit 1}
} finally {
    & $app {$script:TransportHook=$null; $script:ReadbackHook=$null}
    & $store {$script:TestRoot=$null; $script:TestBase=$null}
    & $paths {$script:TestRoot=$null}
    if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
}
