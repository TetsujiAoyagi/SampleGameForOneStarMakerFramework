param([string] $ResultPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
# Sidecarは実際にこのprocessでロードしたassemblyだけを記録する。build出力の推測を代用しない。
function Get-LoadedArtifactBinaries {
    foreach($name in @('ArtifactPackaging','R2ArtifactTransport')) {
        $assembly=[AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -ceq $name}|Select-Object -First 1
        if(-not $assembly){continue}
        $dependencies=@($assembly.GetReferencedAssemblies()|ForEach-Object {
            $reference=$_;$loaded=[AppDomain]::CurrentDomain.GetAssemblies()|Where-Object {$_.GetName().Name -ceq $reference.Name}|Select-Object -First 1
            if(-not $loaded){[pscustomobject]@{name=$reference.Name;status='not-loaded';path=$null;sha256=$null}}
            elseif(-not $loaded.Location){[pscustomobject]@{name=$reference.Name;status='in-memory';path=$null;sha256=$null}}
            else{[pscustomobject]@{name=$reference.Name;status='loaded';path=$loaded.Location;sha256=(Get-FileHash -LiteralPath $loaded.Location -Algorithm SHA256).Hash.ToLowerInvariant()}}
        })
        [pscustomobject]@{path=$assembly.Location;sha256=(Get-FileHash -LiteralPath $assembly.Location -Algorithm SHA256).Hash.ToLowerInvariant();moduleVersionId=$assembly.ManifestModule.ModuleVersionId.ToString();dependencies=$dependencies}
    }
}
$rotation = Import-Module (Join-Path $PSScriptRoot '../Credentials/CredentialRotation.psm1') -Force -PassThru
$store = $rotation.NestedModules | Where-Object Name -eq 'CredentialStore' | Select-Object -Last 1
$paths = $rotation.NestedModules | Where-Object Name -eq 'ArtifactPaths' | Select-Object -Last 1
$app = $rotation.NestedModules | Where-Object Name -eq 'ArtifactApplication' | Select-Object -First 1
$root = [IO.Path]::Combine([IO.Path]::GetTempPath(),'osm-rotation-test-'+[Guid]::NewGuid().ToString('N'))
$cases = @('candidate-pending-confirm','candidate-failure-keeps-old','generation-cas','precommit-fault','postcommit-cleanup','strict-revocation-evidence',
    'revoked-tuple-policy','exact-401-confirm','confirmation-failures','confirmation-cas-mismatch','confirmation-cleanup-fault')
$executed = [Collections.Generic.List[string]]::new(); $failed = [Collections.Generic.List[string]]::new()
$script:objects = @{}; $script:failCandidate = $false
$script:retiredObservation = $null; $script:confirmMode = $false; $script:confirmFaultPhase = 0
$script:confirmMutation = $null
$script:confirmCalls = [Collections.Generic.List[string]]::new()
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
    if($script:confirmMode) {
        $script:confirmCalls.Add("$which/$($request.Operation)")
        $phase=$script:confirmCalls.Count
        if($phase -eq $script:confirmFaultPhase) {
            switch($phase) {
                1 { return New-Observation 'put' $generation 503 'other' '' }
                2 { return New-Observation 'get' $generation 200 'success' '' 1 ('0'*64) }
                3 { return New-Observation 'get' $generation 403 'forbidden' 'Unauthorized' }
                4 { return New-Observation 'get' $generation 200 'success' '' 1 ('0'*64) }
                5 { return New-Observation 'delete' $generation 503 'other' '' }
                6 { return New-Observation 'get' $generation 200 'success' '' 1 ('0'*64) }
            }
        }
        if($phase -eq 6 -and $script:confirmMutation) { & $script:confirmMutation }
    }
    $key=$request.Key
    if($which -eq 'retired') {
        if($script:retiredObservation) { return New-Observation 'get' $generation $script:retiredObservation.Status $script:retiredObservation.Class $script:retiredObservation.Code }
        return New-Observation 'get' $generation 403 'forbidden' 'AccessDenied'
    }
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
    $script:objects=@{}; $script:failCandidate=$false; $script:retiredObservation=$null
    $script:confirmMode=$false; $script:confirmFaultPhase=0; $script:confirmMutation=$null; $script:confirmCalls.Clear()
    $null = & $store { Write-CredentialRecord 'osm' 'DUMMY_OLD_ID' 'DUMMY_OLD_SECRET' $false }
    return @{Config=(New-Config $work);Work=$work}
}
function New-Evidence([string] $Work,[string] $Generation) {
    $evidence=[IO.Path]::Combine($Work,'revocation.json')
    Write-Json $evidence ([ordered]@{schemaVersion=1;generation=$Generation;action='revoked'
        observedAt=[DateTimeOffset]::UtcNow.ToString('o');observer='offline-owner';tokenReference='old-writer-id'})
    return $evidence
}
function Test-Revoked($Observation,[string] $Generation) {
    return & $rotation {param($o,$g) Test-RevokedCredential $o $g} $Observation $Generation
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
                'revoked-tuple-policy' {
                    $generation='a'*32
                    foreach($status in @(401,403)) {
                        $class=if($status -eq 401){'unauthorized'}else{'forbidden'}
                        foreach($code in @('InvalidAccessKeyId','InvalidToken','AccessDenied')) {
                            Assert (Test-Revoked (New-Observation 'get' $generation $status $class $code) $generation) "existing tuple rejected: $status/$code"
                        }
                    }
                    Assert (Test-Revoked (New-Observation 'get' $generation 401 'unauthorized' 'Unauthorized') $generation) 'exact 401 tuple rejected'
                    $invalid=@(
                        @{HttpStatus=403;StatusClass='forbidden';S3Code='Unauthorized'},
                        @{HttpStatus=401;StatusClass='forbidden';S3Code='Unauthorized'},
                        @{HttpStatus=401;StatusClass='Unauthorized';S3Code='Unauthorized'},
                        @{HttpStatus=401;StatusClass='unauthorized';S3Code='unauthorized'},
                        @{HttpStatus=401;StatusClass='unauthorized';S3Code='UNAUTHORIZED'},
                        @{HttpStatus=401;StatusClass='unauthorized';S3Code='UnknownCode'},
                        @{HttpStatus=401;StatusClass='unauthorized';S3Code='SignatureDoesNotMatch'},
                        @{HttpStatus=404;StatusClass='not-found';S3Code='Unauthorized'},
                        @{HttpStatus=500;StatusClass='other';S3Code='Unauthorized'},
                        @{Operation='put'}, @{Generation=('b'*32)}, @{StatusObserved=$false},
                        @{TimedOut=$true}, @{Redirected=$true}
                    )
                    foreach($change in $invalid) {
                        $observation=New-Observation 'get' $generation 401 'unauthorized' 'Unauthorized'
                        foreach($property in $change.Keys) { $observation.$property=$change[$property] }
                        Assert (-not (Test-Revoked $observation $generation)) ('invalid revoked observation accepted: '+(ConvertTo-Json -InputObject $change -Compress))
                    }
                    Assert (-not (Test-Revoked $null $generation)) 'unobserved rejection accepted'
                }
                'exact-401-confirm' {
                    $rotated=Invoke-CredentialRotation $fixture.Config
                    Assert ($rotated.status -ceq 'pending') 'rotation fixture did not reach pending'
                    $pending=& $store { Get-CredentialStatus 'osm' }
                    $script:retiredObservation=@{Status=401;Class='unauthorized';Code='Unauthorized'}
                    $script:confirmMode=$true
                    $confirmed=Invoke-CredentialRevocationConfirmation $fixture.Config $old (New-Evidence $fixture.Work $old)
                    Assert ($confirmed.status -ceq 'passed' -and $confirmed.reasonCode -ceq 'complete') 'exact 401 confirmation failed'
                    $expected=@('active/put','active/get','retired/get','active/get','active/delete','active/get')
                    Assert ($script:confirmCalls.Count -eq 6 -and (@(Compare-Object $expected @($script:confirmCalls) -SyncWindow 0).Count -eq 0)) 'six-operation order changed'
                    $raw=Get-Content -Raw -LiteralPath ([IO.Path]::Combine($confirmed.residue.localPath,'operation-observations.json')) | ConvertFrom-Json
                    Assert ($raw.observations.Count -eq 6 -and $raw.observations[2].httpStatus -eq 401 -and
                        $raw.observations[2].statusClass -ceq 'unauthorized' -and $raw.observations[2].s3Code -ceq 'Unauthorized' -and
                        $raw.observations[1].sha256 -ceq $raw.observations[3].sha256) 'exact 401 or active control raw missing'
                    $stable=& $store { Get-CredentialStatus 'osm' }
                    Assert ($stable.State -ceq 'stable' -and $null -eq $stable.RetiredGeneration -and $stable.Generation -ceq $pending.Generation) 'exact 401 did not clear retired'
                }
                'confirmation-failures' {
                    foreach($phase in @(1,2,3,4,5,6)) {
                        $scenario=Set-Fixture "confirmation-failure-$phase"
                        $original=(& $store { Get-CredentialStatus 'osm' }).Generation
                        $rotated=Invoke-CredentialRotation $scenario.Config
                        Assert ($rotated.status -ceq 'pending') "rotation fixture $phase did not reach pending"
                        $pending=& $store { Get-CredentialStatus 'osm' }
                        $script:retiredObservation=@{Status=401;Class='unauthorized';Code='Unauthorized'}
                        $script:confirmMode=$true; $script:confirmFaultPhase=$phase
                        $confirmed=Invoke-CredentialRevocationConfirmation $scenario.Config $original (New-Evidence $scenario.Work $original)
                        $after=& $store { Get-CredentialStatus 'osm' }
                        Assert ($confirmed.status -ceq 'failed' -and $confirmed.reasonCode -ceq 'unconfirmed' -and
                            $after.State -ceq 'pending-revocation' -and $after.Generation -ceq $pending.Generation -and
                            $after.RetiredGeneration -ceq $original -and $after.TransitionId -ceq $pending.TransitionId) "failure $phase cleared retired"
                        Assert ($script:confirmCalls.Count -eq $phase) "failure $phase continued network operations"
                    }
                }
                'confirmation-cas-mismatch' {
                    $rotated=Invoke-CredentialRotation $fixture.Config
                    Assert ($rotated.status -ceq 'pending') 'CAS fixture did not reach pending'
                    $firstPending=& $store { Get-CredentialStatus 'osm' }
                    $script:retiredObservation=@{Status=401;Class='unauthorized';Code='Unauthorized'}
                    $script:confirmMode=$true
                    $script:confirmMutation={
                        $null=& $store { Confirm-CredentialRetired 'osm' $firstPending.TransitionId $firstPending.Generation $firstPending.RetiredGeneration }
                        $candidate=& $store { New-CredentialCandidate 'osm' 'DUMMY_NEXT_ID' 'DUMMY_NEXT_SECRET' }
                        $null=& $store {param($c) Set-CredentialCandidateActive 'osm' $c.Path $c.ExpectedGeneration $c.Generation} $candidate
                    }.GetNewClosure()
                    $confirmed=Invoke-CredentialRevocationConfirmation $fixture.Config $old (New-Evidence $fixture.Work $old)
                    $after=& $store { Get-CredentialStatus 'osm' }
                    Assert ($confirmed.status -ceq 'failed' -and $confirmed.reasonCode -ceq 'unconfirmed' -and
                        $after.State -ceq 'pending-revocation' -and $after.Generation -cne $firstPending.Generation -and
                        $after.RetiredGeneration -ceq $firstPending.Generation -and $after.TransitionId -cne $firstPending.TransitionId) 'stale confirmation changed newer pending generation'
                    Assert ($script:confirmCalls.Count -eq 6) 'CAS mismatch occurred before network verification completed'
                }
                'confirmation-cleanup-fault' {
                    $rotated=Invoke-CredentialRotation $fixture.Config
                    Assert ($rotated.status -ceq 'pending') 'cleanup fixture did not reach pending'
                    $pending=& $store { Get-CredentialStatus 'osm' }
                    $script:retiredObservation=@{Status=401;Class='unauthorized';Code='Unauthorized'}
                    $script:confirmMode=$true
                    & $store { $script:Fault={param($point) if($point -eq 'after-commit'){throw 'synthetic confirmation cleanup fault'}} }
                    $confirmed=Invoke-CredentialRevocationConfirmation $fixture.Config $old (New-Evidence $fixture.Work $old)
                    $after=& $store { Get-CredentialStatus 'osm' }
                    Assert ($confirmed.status -ceq 'pending' -and $confirmed.reasonCode -ceq 'cleanup-pending' -and
                        $confirmed.residue.cleanup -ceq 'committed with cleanup pending' -and $after.State -ceq 'stable' -and
                        $null -eq $after.RetiredGeneration -and $after.Generation -ceq $pending.Generation) 'confirmation cleanup fault lost committed state'
                }
            }
            $executed.Add($case)
        } catch { $failed.Add($case+': '+$_.Exception.GetType().Name+': '+$_.Exception.Message) }
    }
    if($ResultPath) {
        $result=[ordered]@{registered=$cases;selected=$cases;executed=@($executed);failed=@($failed);binaries=@(Get-LoadedArtifactBinaries)}
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
