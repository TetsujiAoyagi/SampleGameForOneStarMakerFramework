Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1') -Force
$script:FaultHook = $null # Offline tests inject interruption only inside this module's private scope.

function Invoke-ProtectionFault([string] $Stage,[string] $OperationRoot) {
    if ($script:FaultHook) { $null = & $script:FaultHook $Stage $OperationRoot }
}

function Write-ProtectionIntent([string] $Path, $Value) {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes((ConvertTo-Json -InputObject $Value -Compress -Depth 16))
    $stream = [IO.FileStream]::new($Path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
    try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    Set-ArtifactAcl $Path $false
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Test-ProtectionSuccess($Value,[string] $Operation,[string] $Generation) {
    return $null -ne $Value -and $Value.Operation -ceq $Operation -and $Value.Generation -ceq $Generation -and
        $Value.StatusClass -ceq 'success' -and $Value.HttpStatus -ge 200 -and $Value.HttpStatus -le 299 -and
        $Value.StatusObserved -eq $true -and $Value.TimedOut -eq $false -and $Value.Redirected -eq $false
}
function Test-ProtectionLock($Value,[string] $Operation,[string] $Generation) {
    return $null -ne $Value -and $Value.Operation -ceq $Operation -and $Value.Generation -ceq $Generation -and
        $Value.StatusObserved -eq $true -and -not $Value.TimedOut -and -not $Value.Redirected -and
        $Value.S3Code -ceq 'ObjectLockedByBucketPolicy' -and
        (($Value.HttpStatus -eq 403 -and $Value.StatusClass -ceq 'forbidden') -or
         ($Value.HttpStatus -eq 409 -and $Value.StatusClass -ceq 'other'))
}
function Test-ProtectionMissing($Value,[string] $Generation) {
    return $null -ne $Value -and $Value.Operation -ceq 'get' -and $Value.Generation -ceq $Generation -and
        $Value.HttpStatus -eq 404 -and $Value.StatusClass -ceq 'not-found' -and $Value.S3Code -ceq 'NoSuchKey' -and
        $Value.StatusObserved -eq $true -and -not $Value.TimedOut -and -not $Value.Redirected
}
function Test-ProtectionReadback($Value,[string] $Generation,[string] $Key,[long] $Bytes,[string] $Hash) {
    return $null -ne $Value -and $Value.operation -ceq 'readback' -and $Value.generation -ceq $Generation -and
        $Value.key -ceq $Key -and $Value.bytes -eq $Bytes -and $Value.sha256 -ceq $Hash -and
        $Value.childExit -eq 0 -and $Value.childStatus -ceq 'passed' -and
        $Value.httpStatus -eq 200 -and $Value.statusClass -ceq 'success' -and
        $Value.statusObserved -eq $true -and $Value.timedOut -eq $false -and $Value.redirected -eq $false
}

function Invoke-ArtifactEvidenceProtection($Config,$Package,[string] $OperationRoot,[string] $OperationId,
    [string] $RunId,[string] $EvidenceBase,[string] $EvidenceHead,[string] $ProducerBase,[string] $ProducerHead,
    [string] $Generation,[string] $SourceManifestHash,[string] $SelectionReceiptHash,
    [scriptblock] $Send,[scriptblock] $Readback,[scriptblock] $Remaining) {
    $prefix = 'evidence/first-use/' + $Config.Data.repositoryId + '/' + $EvidenceHead + '/' + $RunId + '/'
    $bundleKey = $prefix + 'bundle.zip'; $witnessKey = $prefix + 'protection-witness.txt'
    $controlKey = 'probe/unlocked/' + $OperationId + '/control.txt'
    $observations = [Collections.Generic.List[object]]::new()
    $context = [ordered]@{ bundleKey=$bundleKey; witnessKey=$witnessKey; controlKey=$controlKey
        putIntent=$false; serverLockLowerBound=$null }
    # The initial intent is durable before any network call. After put-intent exists, a crash is
    # conservatively interpreted as may-have-been-sent and the same key must never be retried.
    $intent = [ordered]@{ schemaVersion=1; operationId=$OperationId; runId=$RunId
        bundleKey=$bundleKey; witnessKey=$witnessKey; controlKey=$controlKey
        packageBytes=$Package.Bytes; packageSha256=$Package.Sha256; manifestSha256=$Package.ManifestSha256
        generation=$Generation; configPath=$Config.Path; configSha256=$Config.Hash; settingsBeforeSha256=$Config.SettingsHash
        evidenceBase=$EvidenceBase; evidenceHead=$EvidenceHead; producerBase=$ProducerBase; producerHead=$ProducerHead
        sourceManifestSha256=$SourceManifestHash; selectionReceiptSha256=$SelectionReceiptHash
        createdAt=[DateTimeOffset]::UtcNow.ToString('o') }
    $intentPath = [IO.Path]::Combine($OperationRoot,'intent.json')
    Invoke-ProtectionFault 'before-intent' $OperationRoot
    $intentHash = Write-ProtectionIntent $intentPath $intent
    Invoke-ProtectionFault 'after-intent' $OperationRoot
    $controlPath = [IO.Path]::Combine($OperationRoot,'control.txt')
    $witnessPath = [IO.Path]::Combine($OperationRoot,'protection-witness.txt')
    $differentPath = [IO.Path]::Combine($OperationRoot,'different-witness.txt')
    [IO.File]::WriteAllText($witnessPath,'evidence-witness-' + $RunId,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($differentPath,'different-evidence-witness-' + $RunId,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($controlPath,'evidence-control-' + $RunId,[Text.UTF8Encoding]::new($false))
    Protect-ArtifactTree $OperationRoot
    $witnessInfo = [IO.FileInfo]::new($witnessPath)
    $witnessHash = (Get-FileHash -LiteralPath $witnessPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $controlInfo = [IO.FileInfo]::new($controlPath)
    $controlHash = (Get-FileHash -LiteralPath $controlPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $request = { param($operation,$key,$source,$destination)
        @{ Endpoint=$Config.Data.endpoint; Operation=$operation; Key=$key; SourcePath=$source
           DestinationPath=$destination; DeadlineMilliseconds=(& $Remaining) }
    }.GetNewClosure()
    $observedSend = { param($phase,$req)
        $record = [ordered]@{ phase=$phase; operation=$req.Operation; key=$req.Key; generation=$Generation
            at=[DateTimeOffset]::UtcNow.ToString('o'); status='unconfirmed'; httpStatus=$null; statusClass=$null
            s3Code=$null; bytes=$null; sha256=$null; statusObserved=$false; timedOut=$false; redirected=$false }
        $observations.Add($record)
        $value = & $Send $Generation $req
        if ($null -ne $value -and $value.Operation -ceq $req.Operation -and $value.Generation -ceq $Generation) {
            $record.status='returned'; $record.httpStatus=$value.HttpStatus; $record.statusClass=$value.StatusClass
            $record.s3Code=$value.S3Code; $record.bytes=$value.Bytes; $record.sha256=$value.Sha256
            $record.statusObserved=$value.StatusObserved; $record.timedOut=$value.TimedOut; $record.redirected=$value.Redirected
        }
        return $value
    }.GetNewClosure()
    $observedRead = { param($phase,$key,$bytes,$hash)
        $record = [ordered]@{ phase=$phase; operation='readback'; key=$key; generation=$Generation
            at=[DateTimeOffset]::UtcNow.ToString('o'); status='unconfirmed'; bytes=$null; sha256=$null
            childExit=$null; childStatus=$null; httpStatus=$null; statusClass=$null; s3Code=$null
            statusObserved=$false; timedOut=$false; redirected=$false }
        $observations.Add($record)
        $value = & $Readback $key $bytes $hash
        if ($null -ne $value) {
            $record.status='returned'; $record.bytes=$value.bytes; $record.sha256=$value.sha256
            $record.childExit=$value.childExit; $record.childStatus=$value.childStatus
            $record.httpStatus=$value.httpStatus; $record.statusClass=$value.statusClass; $record.s3Code=$value.s3Code
            $record.statusObserved=$value.statusObserved; $record.timedOut=$value.timedOut; $record.redirected=$value.redirected
        }
        return $value
    }.GetNewClosure()
    try {
        $wput = & $observedSend 'witness-put' (& $request 'put' $witnessKey $witnessPath $null)
        if (-not (Test-ProtectionSuccess $wput 'put' $Generation)) { throw 'Witness PUT unconfirmed.' }
        $wr1 = & $observedRead 'witness-first-readback' $witnessKey $witnessInfo.Length $witnessHash
        if (-not (Test-ProtectionReadback $wr1 $Generation $witnessKey $witnessInfo.Length $witnessHash)) { throw 'Witness readback unconfirmed.' }
        $over = & $observedSend 'witness-overwrite' (& $request 'put' $witnessKey $differentPath $null)
        if (-not (Test-ProtectionLock $over 'put' $Generation)) { throw 'Witness overwrite unconfirmed.' }
        $del = & $observedSend 'witness-delete' (& $request 'delete' $witnessKey $null $null)
        if (-not (Test-ProtectionLock $del 'delete' $Generation)) { throw 'Witness delete unconfirmed.' }
        $wr2 = & $observedRead 'witness-final-readback' $witnessKey $witnessInfo.Length $witnessHash
        if (-not (Test-ProtectionReadback $wr2 $Generation $witnessKey $witnessInfo.Length $witnessHash)) { throw 'Witness final readback unconfirmed.' }
        $cput = & $observedSend 'control-put' (& $request 'put' $controlKey $controlPath $null)
        if (-not (Test-ProtectionSuccess $cput 'put' $Generation)) { throw 'Control PUT unconfirmed.' }
        $cget = & $observedSend 'control-get' (& $request 'get' $controlKey $null ([IO.Path]::Combine($OperationRoot,'control-get.bin')))
        if (-not (Test-ProtectionSuccess $cget 'get' $Generation) -or $cget.Bytes -ne $controlInfo.Length -or $cget.Sha256 -cne $controlHash) { throw 'Control GET unconfirmed.' }
        $cdel = & $observedSend 'control-delete' (& $request 'delete' $controlKey $null $null)
        if (-not (Test-ProtectionSuccess $cdel 'delete' $Generation)) { throw 'Control DELETE unconfirmed.' }
        $cmiss = & $observedSend 'control-absence' (& $request 'get' $controlKey $null ([IO.Path]::Combine($OperationRoot,'control-missing.bin')))
        if (-not (Test-ProtectionMissing $cmiss $Generation)) { throw 'Control absence unconfirmed.' }
        $missing = & $observedSend 'bundle-absence' (& $request 'get' $bundleKey $null ([IO.Path]::Combine($OperationRoot,'bundle-missing.bin')))
        if (-not (Test-ProtectionMissing $missing $Generation)) { throw 'Bundle existence unconfirmed.' }
        $putAt = [DateTimeOffset]::UtcNow
        $lowerBound = $putAt.AddSeconds(2592000).ToString('o')
        $putIntent = [ordered]@{ schemaVersion=1; operationId=$OperationId; bundleKey=$bundleKey
            packageBytes=$Package.Bytes; packageSha256=$Package.Sha256; manifestSha256=$Package.ManifestSha256
            intentSha256=$intentHash; putIntentAt=$putAt.ToString('o'); serverLockLowerBound=$lowerBound
            state='may-have-been-sent' }
        $putIntentPath = [IO.Path]::Combine($OperationRoot,'put-intent.json')
        $putIntentHash = Write-ProtectionIntent $putIntentPath $putIntent
        $context.putIntent=$true; $context.serverLockLowerBound=$lowerBound
        Invoke-ProtectionFault 'after-put-intent' $OperationRoot
        $archiveHash = (Get-FileHash -LiteralPath $Package.Path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($archiveHash -cne $Package.Sha256 -or ([IO.FileInfo]::new($Package.Path)).Length -ne $Package.Bytes) { throw 'Archive changed.' }
        # Exactly one bundle PUT occurs here. Failure or timeout never retries this key.
        $bput = & $observedSend 'bundle-put' (& $request 'put' $bundleKey $Package.Path $null)
        if (-not (Test-ProtectionSuccess $bput 'put' $Generation)) { throw 'Bundle PUT unconfirmed.' }
        $br = & $observedRead 'bundle-readback' $bundleKey $Package.Bytes $Package.Sha256
        if (-not (Test-ProtectionReadback $br $Generation $bundleKey $Package.Bytes $Package.Sha256)) { throw 'Bundle readback unconfirmed.' }
        $wr3 = & $observedRead 'witness-after-bundle' $witnessKey $witnessInfo.Length $witnessHash
        if (-not (Test-ProtectionReadback $wr3 $Generation $witnessKey $witnessInfo.Length $witnessHash)) { throw 'Witness post-readback unconfirmed.' }
        Invoke-ProtectionFault 'before-candidate' $OperationRoot
        $receipt = [ordered]@{ schemaVersion=1; operationId=$OperationId; runId=$RunId; generation=$Generation
            bundleKey=$bundleKey; witnessKey=$witnessKey; controlKey=$controlKey
            intentSha256=$intentHash; putIntentSha256=$putIntentHash; configSha256=$Config.Hash
            packageBytes=$Package.Bytes; packageSha256=$Package.Sha256; manifestSha256=$Package.ManifestSha256
            serverLockLowerBound=$lowerBound; observations=@($observations.ToArray()); completedAt=[DateTimeOffset]::UtcNow.ToString('o') }
        $receiptPath = [IO.Path]::Combine($OperationRoot,'protection-receipt.json')
        $receiptHash = Write-ProtectionIntent $receiptPath $receipt
        return [pscustomobject]@{ ReceiptPath=$receiptPath; ReceiptSha256=$receiptHash; IntentPath=$intentPath
            IntentSha256=$intentHash; PutIntentPath=$putIntentPath; PutIntentSha256=$putIntentHash
            ServerLockLowerBound=$lowerBound; BundleKey=$bundleKey; WitnessKey=$witnessKey; ControlKey=$controlKey
            Observations=@($observations.ToArray()) }
    } catch {
        # The observation file remains useful after a failed or interrupted attempt. No remote
        # cleanup is attempted when child/process state or bundle PUT state is uncertain.
        $status = [ordered]@{ schemaVersion=1; operationId=$OperationId; context=$context
            observations=@($observations.ToArray()); at=[DateTimeOffset]::UtcNow.ToString('o') }
        try { $null = Write-ProtectionIntent ([IO.Path]::Combine($OperationRoot,'protection-failure.json')) $status } catch {}
        throw
    }
}

Export-ModuleMember -Function Invoke-ArtifactEvidenceProtection
