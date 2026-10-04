param([string] $ResultPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$rotation = Import-Module (Join-Path $PSScriptRoot '../Credentials/CredentialRotation.psm1') -Force -PassThru
$store = $rotation.NestedModules | Where-Object Name -eq 'CredentialStore' | Select-Object -Last 1
$paths = $rotation.NestedModules | Where-Object Name -eq 'ArtifactPaths' | Select-Object -Last 1
$app = $rotation.NestedModules | Where-Object Name -eq 'ArtifactApplication' | Select-Object -First 1
$root = [IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-rotation-test-'+[Guid]::NewGuid().ToString('N'))
$cases = @('candidate-pending-confirm','candidate-failure-keeps-old','generation-cas','precommit-fault','postcommit-cleanup','strict-revocation-evidence')
$executed = [Collections.Generic.List[string]]::new(); $failed = [Collections.Generic.List[string]]::new()
$script:objects = @{}; $script:failCandidate = $false
function Assert([bool] $Condition,[string] $Message) { if(-not $Condition){throw $Message} }
function Write-Json([string] $Path,$Value){[IO.File]::WriteAllText($Path,(ConvertTo-Json -InputObject $Value -Compress -Depth 8),[Text.UTF8Encoding]::new($false))}
function New-Observation([string] $Operation,[string] $Generation,[int] $Status,[string] $Class,[string] $Code,[long] $Bytes=0,[string] $Hash='') {
    return [pscustomobject]@{Operation=$Operation;Generation=$Generation;HttpStatus=$Status;StatusClass=$Class
        S3Code=if($Code){$Code}else{$null};Bytes=if($Operation -eq 'get'){$Bytes}else{$null}
        Sha256=if($Operation -eq 'get'){$Hash}else{$null};StatusObserved=$true;TimedOut=$false;Redirected=$false}
}
$network = {
    param($which,$generation,$request)
    if($script:failCandidate -and $which -eq 'candidate') { return New-Observation $request.Operation $generation 503 'other' '' }
    $key=$request.Key
    if($which -eq 'retired') { return New-Observation 'get' $generation 403 'forbidden' 'AccessDenied' }
    switch($request.Operation){
        'put' {$script:objects[$key]=[IO.File]::ReadAllBytes($request.SourcePath);return New-Observation 'put' $generation 200 'success' ''}
        'delete' {$script:objects.Remove($key);return New-Observation 'delete' $generation 204 'success' ''}
        'get' {
            if(-not $script:objects.ContainsKey($key)){return New-Observation 'get' $generation 404 'not-found' 'NoSuchKey'}
            $bytes=$script:objects[$key]
            [IO.File]::WriteAllBytes($request.DestinationPath,$bytes)
            $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
            return New-Observation 'get' $generation 200 'success' '' $bytes.Length $hash
        }
    }
}
$inputHook = { [pscustomobject]@{AccessKeyId='DUMMY_NEW_ID';SecretAccessKey='DUMMY_NEW_SECRET'} }
function New-Config([string] $Work) {
    [IO.Directory]::CreateDirectory($Work)|Out-Null
    $observed=[DateTimeOffset]::UtcNow.AddMinutes(-1).ToString('o')
    $endpoint='https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'
    $settings=[IO.Path]::Combine($Work,'settings.json')
    Write-Json $settings ([ordered]@{schemaVersion=1;endpoint=$endpoint;bucket='osm-artifacts';prefix='probe/locked/'
        retentionSeconds=86400;publicDevelopmentUrlEnabled=$false;customDomainCount=0;writerCanConfigure=$false
        bucketScopedWriter=$true;lockEnabled=$true;ageRuleCount=1;dateRuleCount=0;indefiniteRuleCount=0
        lifecycleCompatible=$true;observedAt=$observed;observer='offline-test'})
    $hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $settings).Hash.ToLowerInvariant()
    $config=[IO.Path]::Combine($Work,'config.json')
    Write-Json $config ([ordered]@{schemaVersion=1;profile='osm';endpoint=$endpoint;bucket='osm-artifacts'
        repositoryId=('c'*64);prefix='probe/locked/';retentionSeconds=86400;observedAt=$observed
        validUntil=[DateTimeOffset]::UtcNow.AddHours(1).ToString('o');settingsEvidencePath=$settings
        settingsEvidenceSha256=$hash})
    return $config
}
function Set-Fixture([string] $Name) {
    $work=[IO.Path]::Combine($root,$Name)
    [IO.Directory]::CreateDirectory($work)|Out-Null
    $credential=[IO.Path]::Combine($work,'credentials')
    $transfer=[IO.Path]::Combine($work,'transfers')
    & $store {param($p) $script:TestRoot=$p;$script:TestBase=$null;$script:Fault=$null} $credential
    & $paths {param($p) $script:TestRoot=$p} $transfer
    foreach($module in @($app.NestedModules | Where-Object Name -eq 'ArtifactPaths')) { & $module {param($p) $script:TestRoot=$p} $transfer }
    $script:objects=@{}; $script:failCandidate=$false
    $null = & $store { Write-CredentialRecord 'osm' 'DUMMY_OLD_ID' 'DUMMY_OLD_SECRET' $false }
    return @{Config=(New-Config $work);Work=$work}
}
try {
    & $rotation {param($n,$i) $script:RotationTransportHook=$n;$script:RotationInputHook=$i} $network $inputHook
    foreach($case in $cases) {
        try {
            $fixture=Set-Fixture $case
            $old=(& $store { Get-CredentialStatus 'osm' }).Generation
            switch($case){
                'candidate-pending-confirm' {
                    $rotated=Invoke-CredentialRotation $fixture.Config
                    Assert ($rotated.status -ceq 'pending' -and $rotated.reasonCode -ceq 'revocation-required') ("rotation did not commit pending: " + (ConvertTo-Json -InputObject $rotated -Compress -Depth 8))
                    $pending=& $store { Get-CredentialStatus 'osm' }
                    Assert ($pending.State -ceq 'pending-revocation' -and $pending.RetiredGeneration -ceq $old -and $pending.Generation -cne $old) 'pending state'
                    $blocked=$false
                    try { $null = & $store { Write-CredentialRecord 'osm' 'ANOTHER_ID' 'ANOTHER_SECRET' $true } } catch {$blocked=$true}
                    Assert $blocked 'set during pending was accepted'
                    $blocked=$false
                    try { $null = & $store { Remove-CredentialRecord 'osm' } } catch {$blocked=$true}
                    Assert $blocked 'remove during pending was accepted'
                    $evidence=[IO.Path]::Combine($fixture.Work,'revocation.json')
                    Write-Json $evidence ([ordered]@{schemaVersion=1;generation=$old;action='revoked'
                        observedAt=[DateTimeOffset]::UtcNow.ToString('o');observer='offline-owner';tokenReference='old-writer-id'})
                    $confirmed=Invoke-CredentialRevocationConfirmation $fixture.Config $old $evidence
                    Assert ($confirmed.status -ceq 'passed') 'confirmation failed'
                    $confirmRaw=Get-Content -Raw -LiteralPath ([IO.Path]::Combine($confirmed.residue.localPath,'operation-observations.json')) | ConvertFrom-Json
                    Assert ($confirmRaw.observations.Count -eq 6 -and $confirmRaw.observations[2].phase -ceq 'retired-get' -and
                        $confirmRaw.observations[2].generation -ceq $old -and $confirmRaw.observations[2].httpStatus -eq 403 -and
                        $confirmRaw.observations[2].s3Code -ceq 'AccessDenied' -and
                        $confirmRaw.observations[1].sha256 -ceq $confirmRaw.observations[3].sha256) ('retired/active primary raw missing: ' + (ConvertTo-Json -InputObject $confirmRaw.observations -Compress -Depth 5))
                    $stable=& $store { Get-CredentialStatus 'osm' }
                    Assert ($stable.State -ceq 'stable' -and $null -eq $stable.RetiredGeneration -and $stable.Generation -ceq $pending.Generation) 'stable transition'
                }
                'candidate-failure-keeps-old' {
                    $script:failCandidate=$true
                    $rotated=Invoke-CredentialRotation $fixture.Config
                    Assert ($rotated.status -ceq 'failed') 'candidate failure accepted'
                    $failureRaw=Get-Content -Raw -LiteralPath ([IO.Path]::Combine($rotated.residue.localPath,'operation-observations.json')) | ConvertFrom-Json
                    Assert ($failureRaw.observations.Count -eq 1 -and $failureRaw.observations[0].httpStatus -eq 503 -and
                        $failureRaw.observations[0].generation -cne $old) 'candidate failure raw missing'
                    $status=& $store { Get-CredentialStatus 'osm' }
                    Assert ($status.State -ceq 'stable' -and $status.Generation -ceq $old) 'old credential changed on failure'
                }
                'generation-cas' {
                    $candidate=& $store { New-CredentialCandidate 'osm' 'DUMMY_CAS_ID' 'DUMMY_CAS_SECRET' }
                    $null = & $store { Write-CredentialRecord 'osm' 'DUMMY_OTHER_ID' 'DUMMY_OTHER_SECRET' $true }
                    $blocked=$false
                    try { $null = & $store {param($c) Set-CredentialCandidateActive 'osm' $c.Path $c.ExpectedGeneration $c.Generation} $candidate } catch {$blocked=$true}
                    Assert $blocked 'stale candidate changed active generation'
                    Assert ((& $store { Get-CredentialStatus 'osm' }).State -ceq 'stable') 'stale CAS left pending state'
                }
                'precommit-fault' {
                    & $store { $script:Fault = { param($point) if($point -eq 'before-commit'){throw 'synthetic fault'} } }
                    $rotated=Invoke-CredentialRotation $fixture.Config
                    Assert ($rotated.status -ceq 'failed') 'precommit fault accepted'
                    $status=& $store { Get-CredentialStatus 'osm' }
                    Assert ($status.State -ceq 'stable' -and $status.Generation -ceq $old) 'old generation changed before commit'
                }
                'postcommit-cleanup' {
                    & $store { $script:Fault = { param($point) if($point -eq 'after-commit'){throw 'synthetic cleanup fault'} } }
                    $rotated=Invoke-CredentialRotation $fixture.Config
                    Assert ($rotated.status -ceq 'pending' -and $rotated.residue.cleanup -ceq 'committed with cleanup pending') 'postcommit cleanup classified as uncommitted'
                    $status=& $store { Get-CredentialStatus 'osm' }
                    Assert ($status.State -ceq 'pending-revocation' -and $status.RetiredGeneration -ceq $old) 'committed pending state absent'
                }
                'strict-revocation-evidence' {
                    $rotated=Invoke-CredentialRotation $fixture.Config
                    Assert ($rotated.status -ceq 'pending') ("rotation fixture failed: " + (ConvertTo-Json -InputObject $rotated -Compress -Depth 8))
                    $evidence=[IO.Path]::Combine($fixture.Work,'revocation.json')
                    Write-Json $evidence ([ordered]@{schemaVersion=1;generation=$old;action='revoked'
                        observedAt=[DateTimeOffset]::UtcNow.ToString('o');observer='offline-owner';tokenReference='old-id';extra='unexpected'})
                    $confirmed=Invoke-CredentialRevocationConfirmation $fixture.Config $old $evidence
                    Assert ($confirmed.status -ceq 'failed' -and (& $store { Get-CredentialStatus 'osm' }).State -ceq 'pending-revocation') 'bad owner evidence accepted'
                }
            }
            $executed.Add($case)
        } catch { $failed.Add($case+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message) }
    }
    if($ResultPath) {
        $result=[ordered]@{registered=$cases;selected=$cases;executed=@($executed);failed=@($failed)}
        [IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -InputObject $result -Depth 6),[Text.UTF8Encoding]::new($false))
    }
    foreach($item in $executed){"PASS $item"};foreach($item in $failed){"FAIL $item"}
    "Cases: $($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if($failed.Count -gt 0 -or $executed.Count -ne $cases.Count){exit 1}
} finally {
    & $rotation {$script:RotationTransportHook=$null;$script:RotationInputHook=$null}
    & $store {$script:TestRoot=$null;$script:TestBase=$null;$script:Fault=$null}
    & $paths {$script:TestRoot=$null}
    foreach($module in @($app.NestedModules | Where-Object Name -eq 'ArtifactPaths')) { & $module {$script:TestRoot=$null} }
    if([IO.Directory]::Exists($root)){[IO.Directory]::Delete($root,$true)}
}
