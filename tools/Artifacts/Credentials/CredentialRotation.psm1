Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'CredentialCommands.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'CredentialStore.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../ArtifactApplication.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../ArtifactCommands.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../ArtifactPaths.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../Transport/R2ArtifactTransport.psm1') -Force

$script:RotationTransportHook = $null
$script:RotationInputHook = $null

function Invoke-RotationNetwork([string] $Which, [string] $CandidatePath, [string] $TransitionId,
    [string] $Generation, [hashtable] $Request) {
    if ($script:RotationTransportHook) { return & $script:RotationTransportHook $Which $Generation $Request }
    $transportInvoker = (Get-Command Invoke-R2ArtifactOperation -ErrorAction Stop).ScriptBlock
    $callback = {
        param($id, $secret, $actualGeneration, $details)
        if ($actualGeneration -cne $Generation) { throw 'Credential generation changed.' }
        & $transportInvoker $id $secret $actualGeneration $details
    }.GetNewClosure()
    switch ($Which) {
        'candidate' { return Invoke-CredentialCandidateTransport 'osm' $CandidatePath $callback $Request }
        'retired' { return Invoke-CredentialRetiredTransport 'osm' $TransitionId $callback $Request }
        'active' { return Invoke-CredentialTransport 'osm' $callback $Request }
        default { throw 'Credential operation unavailable.' }
    }
}

function Test-RevokedCredential($Observation, [string] $Generation) {
    return $null -ne $Observation -and $Observation.Operation -ceq 'get' -and
        $Observation.Generation -ceq $Generation -and $Observation.StatusObserved -eq $true -and
        -not $Observation.TimedOut -and -not $Observation.Redirected -and
        $Observation.HttpStatus -in @(401,403) -and
        (($Observation.HttpStatus -eq 401 -and $Observation.StatusClass -ceq 'unauthorized') -or
         ($Observation.HttpStatus -eq 403 -and $Observation.StatusClass -ceq 'forbidden')) -and
        ($Observation.S3Code -cin @('InvalidAccessKeyId','InvalidToken','AccessDenied') -or
         ($Observation.HttpStatus -eq 401 -and $Observation.S3Code -ceq 'Unauthorized'))
}

function Invoke-CredentialRotation([string] $ConfigPath) {
    $id = [Guid]::NewGuid().ToString('N'); $result = New-ArtifactResult $id 'rotate'
    $timer = [Diagnostics.Stopwatch]::StartNew(); $operationRoot = $null; $candidate = $null; $config = $null
    $observations = [Collections.Generic.List[object]]::new()
    $context = [ordered]@{ oldGeneration = $null; candidateGeneration = $null; transitionId = $null
        runId = $null; key = $null; expectedBytes = $null; expectedSha256 = $null }
    try {
        $config = Read-ArtifactConfig $ConfigPath
        $status = Get-CredentialStatus 'osm'
        $context.oldGeneration = $status.Generation
        if ($status.State -cne 'stable') { throw 'Rotation pending.' }
        $input = if ($script:RotationInputHook) { & $script:RotationInputHook } else { Read-CredentialRotationInput }
        try { $candidate = New-CredentialCandidate 'osm' $input.AccessKeyId $input.SecretAccessKey }
        finally { $input.AccessKeyId = $null; $input.SecretAccessKey = $null }
        $context.candidateGeneration = $candidate.Generation
        $result.residue.candidatePath = $candidate.Path
        $operationRoot = New-ArtifactOperation
        $result.residue.localPath = $operationRoot
        $run = [Guid]::NewGuid().ToString('N')
        $key = "probe/unlocked/$run/control.txt"
        $context.runId = $run; $context.key = $key
        $result.residue.remoteKeys = @($key)
        $bytes = [Text.Encoding]::UTF8.GetBytes('synthetic-candidate-' + $run)
        $source = [IO.Path]::Combine($operationRoot, 'candidate-control.txt')
        [IO.File]::WriteAllBytes($source, $bytes)
        Protect-ArtifactTree $operationRoot
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        $context.expectedBytes = $bytes.Length; $context.expectedSha256 = $hash
        $request = New-ArtifactRequest $config 'put' $key $source $null $timer
        $put = Invoke-ArtifactObservedNetwork $observations 'candidate-put' $candidate.Generation $request { Invoke-RotationNetwork 'candidate' $candidate.Path $null $candidate.Generation $request } $bytes.Length $hash
        if (-not (Test-ArtifactSuccess $put 'put' $candidate.Generation)) { throw 'Candidate PUT unconfirmed.' }
        $request = New-ArtifactRequest $config 'get' $key $null ([IO.Path]::Combine($operationRoot, 'candidate-get.bin')) $timer
        $get = Invoke-ArtifactObservedNetwork $observations 'candidate-get' $candidate.Generation $request { Invoke-RotationNetwork 'candidate' $candidate.Path $null $candidate.Generation $request } $bytes.Length $hash
        if (-not (Test-ArtifactSuccess $get 'get' $candidate.Generation) -or $get.Bytes -ne $bytes.Length -or $get.Sha256 -cne $hash) {
            throw 'Candidate GET unconfirmed.'
        }
        $request = New-ArtifactRequest $config 'delete' $key $null $null $timer
        $delete = Invoke-ArtifactObservedNetwork $observations 'candidate-delete' $candidate.Generation $request { Invoke-RotationNetwork 'candidate' $candidate.Path $null $candidate.Generation $request }
        if (-not (Test-ArtifactSuccess $delete 'delete' $candidate.Generation)) { throw 'Candidate DELETE unconfirmed.' }
        $request = New-ArtifactRequest $config 'get' $key $null ([IO.Path]::Combine($operationRoot, 'candidate-missing.bin')) $timer
        $missing = Invoke-ArtifactObservedNetwork $observations 'candidate-absence' $candidate.Generation $request { Invoke-RotationNetwork 'candidate' $candidate.Path $null $candidate.Generation $request }
        if (-not (Test-ArtifactMissing $missing $candidate.Generation)) { throw 'Candidate absence unconfirmed.' }
        $result.verification.candidate = $true
        $commit = Set-CredentialCandidateActive 'osm' $candidate.Path $candidate.ExpectedGeneration $candidate.Generation
        $context.transitionId = $commit.TransitionId
        $result.status = 'pending'; $result.reasonCode = 'revocation-required'
        $result.verification.atomicSwitch = $true
        $result.residue.remote = 'control-deleted'
        $result.residue.cleanup = $commit.Outcome
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

function Read-RevocationEvidence([string] $Path, [string] $Generation) {
    $record = Read-ArtifactJson $Path
    $data = $record.Data
    Assert-Fields $data @('schemaVersion','generation','action','observedAt','observer','tokenReference')
    if ($data.schemaVersion -isnot [long] -or $data.schemaVersion -ne 1 -or
        $data.generation -isnot [string] -or $data.generation -cne $Generation -or
        $data.action -isnot [string] -or $data.action -cne 'revoked' -or
        $data.observer -isnot [string] -or [string]::IsNullOrWhiteSpace($data.observer) -or $data.observer.Length -gt 128 -or
        $data.tokenReference -isnot [string] -or [string]::IsNullOrWhiteSpace($data.tokenReference) -or
        $data.tokenReference.Length -gt 128) { throw 'Revocation evidence unavailable.' }
    $when = Assert-Utc $data.observedAt
    if ($when -gt [DateTimeOffset]::UtcNow -or $when -lt [DateTimeOffset]::UtcNow.AddHours(-24)) {
        throw 'Revocation evidence expired.'
    }
    return $record
}

function Invoke-CredentialRevocationConfirmation([string] $ConfigPath, [string] $Generation, [string] $EvidencePath) {
    $id = [Guid]::NewGuid().ToString('N'); $result = New-ArtifactResult $id 'confirm-revocation'
    $timer = [Diagnostics.Stopwatch]::StartNew(); $operationRoot = $null; $config = $null
    $observations = [Collections.Generic.List[object]]::new()
    $context = [ordered]@{ activeGeneration = $null; retiredGeneration = $Generation; transitionId = $null
        revocationEvidenceSha256 = $null; runId = $null; key = $null; expectedBytes = $null; expectedSha256 = $null }
    try {
        Assert-Hex $Generation 32
        $config = Read-ArtifactConfig $ConfigPath
        $status = Get-CredentialStatus 'osm'
        if ($status.State -cne 'pending-revocation' -or $status.RetiredGeneration -cne $Generation) {
            throw 'Retired generation unavailable.'
        }
        $evidence = Read-RevocationEvidence $EvidencePath $Generation
        $context.revocationEvidenceSha256 = $evidence.Sha256
        $operationRoot = New-ArtifactOperation
        $result.residue.localPath = $operationRoot
        $run = [Guid]::NewGuid().ToString('N')
        $key = "probe/unlocked/$run/control.txt"
        $context.runId = $run; $context.key = $key
        $result.residue.remoteKeys = @($key)
        $bytes = [Text.Encoding]::UTF8.GetBytes('synthetic-revocation-' + $run)
        $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
        $context.expectedBytes = $bytes.Length; $context.expectedSha256 = $hash
        $source = [IO.Path]::Combine($operationRoot, 'revocation-control.txt')
        [IO.File]::WriteAllBytes($source, $bytes)
        Protect-ArtifactTree $operationRoot
        $active = $status.Generation; $transition = $status.TransitionId
        $context.activeGeneration = $active; $context.transitionId = $transition
        $request = New-ArtifactRequest $config 'put' $key $source $null $timer
        $put = Invoke-ArtifactObservedNetwork $observations 'active-put' $active $request { Invoke-RotationNetwork 'active' $null $null $active $request } $bytes.Length $hash
        if (-not (Test-ArtifactSuccess $put 'put' $active)) { throw 'Active PUT unconfirmed.' }
        $request = New-ArtifactRequest $config 'get' $key $null ([IO.Path]::Combine($operationRoot, 'active-first.bin')) $timer
        $first = Invoke-ArtifactObservedNetwork $observations 'active-first-get' $active $request { Invoke-RotationNetwork 'active' $null $null $active $request } $bytes.Length $hash
        if (-not (Test-ArtifactSuccess $first 'get' $active) -or $first.Bytes -ne $bytes.Length -or $first.Sha256 -cne $hash) {
            throw 'Active GET unconfirmed.'
        }
        $request = New-ArtifactRequest $config 'get' $key $null ([IO.Path]::Combine($operationRoot, 'retired-get.bin')) $timer
        $retired = Invoke-ArtifactObservedNetwork $observations 'retired-get' $Generation $request { Invoke-RotationNetwork 'retired' $null $transition $Generation $request }
        if (-not (Test-RevokedCredential $retired $Generation)) { throw 'Retired rejection unconfirmed.' }
        $request = New-ArtifactRequest $config 'get' $key $null ([IO.Path]::Combine($operationRoot, 'active-second.bin')) $timer
        $second = Invoke-ArtifactObservedNetwork $observations 'active-second-get' $active $request { Invoke-RotationNetwork 'active' $null $null $active $request } $bytes.Length $hash
        if (-not (Test-ArtifactSuccess $second 'get' $active) -or $second.Bytes -ne $bytes.Length -or $second.Sha256 -cne $hash) {
            throw 'Active control unconfirmed.'
        }
        $request = New-ArtifactRequest $config 'delete' $key $null $null $timer
        $delete = Invoke-ArtifactObservedNetwork $observations 'active-delete' $active $request { Invoke-RotationNetwork 'active' $null $null $active $request }
        if (-not (Test-ArtifactSuccess $delete 'delete' $active)) { throw 'Control delete unconfirmed.' }
        $request = New-ArtifactRequest $config 'get' $key $null ([IO.Path]::Combine($operationRoot, 'control-missing.bin')) $timer
        $missing = Invoke-ArtifactObservedNetwork $observations 'active-absence' $active $request { Invoke-RotationNetwork 'active' $null $null $active $request }
        if (-not (Test-ArtifactMissing $missing $active)) { throw 'Control absence unconfirmed.' }
        $result.verification.revoked = $true
        $outcome = Confirm-CredentialRetired 'osm' $transition $active $Generation
        $result.verification.atomicClear = $true
        $result.residue.remote = 'control-deleted'
        $result.residue.cleanup = $outcome
        if ($outcome -cne 'success') {
            $result.status = 'pending'; $result.reasonCode = 'cleanup-pending'
        } else {
            $result.status = 'passed'; $result.reasonCode = 'complete'
        }
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

Export-ModuleMember -Function Invoke-CredentialRotation, Invoke-CredentialRevocationConfirmation
