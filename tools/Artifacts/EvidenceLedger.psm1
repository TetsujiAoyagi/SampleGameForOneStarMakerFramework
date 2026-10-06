Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactProtectionPolicy.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'EvidencePaths.psm1') -Force

function Read-EvidenceChecked([string] $Path,[string] $Hash) {
    Assert-Hex $Hash 64
    $item = Read-ArtifactJson $Path
    if ($item.Sha256 -cne $Hash) { throw 'Evidence hash mismatch.' }
    return $item.Data
}
function Assert-EvidenceChild([string] $Root,[string] $Path,[string] $Name) {
    if (-not [IO.Path]::IsPathFullyQualified($Path) -or
        -not [IO.Path]::GetFullPath($Path).Equals([IO.Path]::Combine($Root,$Name),[StringComparison]::OrdinalIgnoreCase)) {
        throw 'Evidence provenance path unavailable.'
    }
}
function Assert-EvidenceLedgerPath([string] $Path,$Ledger) {
    if ($Ledger.ledgerId -cnotmatch '\A[0-9a-f]{32}\z' -or
        -not [IO.Path]::GetFullPath($Path).Equals([IO.Path]::Combine((Get-EvidenceRoot 'evidence-ledgers'),$Ledger.ledgerId,'ledger.json'),
            [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence ledger path unavailable.' }
}
function Assert-EvidenceObservationIdentity($Observation,[string] $Phase,[string] $Operation,[string] $Key,[string] $Generation) {
    if ($Observation.phase -cne $Phase -or $Observation.operation -cne $Operation -or
        $Observation.key -cne $Key -or $Observation.generation -cne $Generation -or
        $Observation.status -cne 'returned') { throw 'Evidence observation identity mismatch.' }
    $null=Assert-Utc $Observation.at
}
function Assert-EvidenceNetworkObservation($Observation,[string] $Phase,[string] $Operation,
    [string] $Key,[string] $Generation,[string] $Expected,[long] $Bytes=0,[string] $Hash='') {
    Assert-Fields $Observation @('phase','operation','key','generation','at','status','httpStatus','statusClass',
        's3Code','bytes','sha256','statusObserved','timedOut','redirected')
    Assert-EvidenceObservationIdentity $Observation $Phase $Operation $Key $Generation
    if ($Observation.statusObserved -isnot [bool] -or -not $Observation.statusObserved -or
        $Observation.timedOut -isnot [bool] -or $Observation.timedOut -or
        $Observation.redirected -isnot [bool] -or $Observation.redirected -or
        $Observation.httpStatus -isnot [long]) { throw 'Evidence network observation unconfirmed.' }
    switch ($Expected) {
        'success' {
            if ($Observation.httpStatus -lt 200 -or $Observation.httpStatus -gt 299 -or
                $Observation.statusClass -cne 'success' -or $null -ne $Observation.s3Code) {
                throw 'Evidence network success mismatch.'
            }
            if ($Operation -ceq 'get' -and ($Observation.bytes -ne $Bytes -or $Observation.sha256 -cne $Hash)) {
                throw 'Evidence GET bytes mismatch.'
            }
        }
        'lock' {
            if ($Observation.s3Code -cne 'ObjectLockedByBucketPolicy' -or
                (($Observation.httpStatus -ne 403 -or $Observation.statusClass -cne 'forbidden') -and
                 ($Observation.httpStatus -ne 409 -or $Observation.statusClass -cne 'other'))) {
                throw 'Evidence lock tuple mismatch.'
            }
        }
        'missing' {
            if ($Observation.httpStatus -ne 404 -or $Observation.statusClass -cne 'not-found' -or
                $Observation.s3Code -cne 'NoSuchKey') { throw 'Evidence absence tuple mismatch.' }
        }
        default { throw 'Evidence observation expectation unavailable.' }
    }
}
function Assert-EvidenceReadbackObservation($Observation,[string] $Phase,[string] $Key,[string] $Generation,
    [long] $Bytes,[string] $Hash) {
    Assert-Fields $Observation @('phase','operation','key','generation','at','status','bytes','sha256',
        'childExit','childStatus','httpStatus','statusClass','s3Code','statusObserved','timedOut','redirected')
    Assert-EvidenceObservationIdentity $Observation $Phase 'readback' $Key $Generation
    if ($Observation.childExit -ne 0 -or $Observation.childStatus -cne 'passed' -or
        $Observation.bytes -ne $Bytes -or $Observation.sha256 -cne $Hash -or
        $Observation.httpStatus -ne 200 -or $Observation.statusClass -cne 'success' -or
        $null -ne $Observation.s3Code -or $Observation.statusObserved -isnot [bool] -or
        -not $Observation.statusObserved -or $Observation.timedOut -isnot [bool] -or
        $Observation.timedOut -or $Observation.redirected -isnot [bool] -or $Observation.redirected) {
        throw 'Evidence readback mismatch.'
    }
}

function Invoke-EvidenceCommit([string] $CandidatePath,[string] $CandidateHash,[string] $SettingsAfterPath,[string] $SettingsAfterHash) {
    try {
        $result = Read-EvidenceChecked $CandidatePath $CandidateHash
        Assert-Fields $result @('schemaVersion','operationId','operation','status','reasonCode','verification','ledger','outputPath','residue')
        if ($result.schemaVersion -isnot [long] -or $result.schemaVersion -ne 1 -or
            $result.operation -cne 'evidence-publish' -or $result.status -cne 'passed' -or
            $result.reasonCode -cne 'candidate' -or $result.outputPath -ne $null -or
            $result.ledger -eq $null -or $result.operationId -cnotmatch '\A[0-9a-f]{32}\z') {
            throw 'Evidence candidate unavailable.'
        }
        foreach ($name in @('snapshot','entries','witness','control','put','readback')) {
            if ($result.verification.$name -isnot [bool] -or -not $result.verification.$name) {
                throw 'Evidence candidate verification unavailable.'
            }
        }
        $root = $result.residue.localPath
        Assert-EvidenceChild $root $CandidatePath 'result.json'
        Assert-ArtifactOperationFile $root $CandidatePath
        $candidate = $result.ledger
        Assert-Fields $candidate @('reference','key','packageBytes','packageSha256','manifestSha256',
            'evidenceBase','evidenceHead','producerBase','producerHead','runId','sourceManifestSha256',
            'selectionReceiptSha256','entries','protectionReceiptSha256','protectionReceiptPath','intentPath',
            'intentSha256','putIntentPath','putIntentSha256','serverLockLowerBound','configSha256','settingsBeforeSha256')
        foreach ($name in @('packageSha256','manifestSha256','sourceManifestSha256','selectionReceiptSha256',
                'protectionReceiptSha256','intentSha256','putIntentSha256','configSha256','settingsBeforeSha256')) {
            Assert-Hex $candidate.$name 64
        }
        foreach ($name in @('evidenceBase','evidenceHead','producerBase','producerHead')) { Assert-Hex $candidate.$name 40 }
        Assert-Hex $candidate.runId 32
        if ($candidate.producerBase -cne '6d804ca637cf42fb876e602c8ccc0656cfbd255d' -or
            $candidate.packageBytes -isnot [long] -or $candidate.packageBytes -lt 1 -or
            $candidate.packageBytes -gt 256MB -or
            $candidate.key -cne "evidence/first-use/4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b/$($candidate.evidenceHead)/$($candidate.runId)/bundle.zip") {
            throw 'Evidence candidate identity mismatch.'
        }
        Assert-EvidenceChild $root $candidate.intentPath 'intent.json'
        Assert-EvidenceChild $root $candidate.putIntentPath 'put-intent.json'
        Assert-EvidenceChild $root $candidate.protectionReceiptPath 'protection-receipt.json'
        foreach ($path in @($candidate.intentPath,$candidate.putIntentPath,$candidate.protectionReceiptPath,
                ([IO.Path]::Combine($root,'selection-receipt.json')))) {
            Assert-ArtifactOperationFile $root $path
        }
        $intent = Read-EvidenceChecked $candidate.intentPath $candidate.intentSha256
        $putIntent = Read-EvidenceChecked $candidate.putIntentPath $candidate.putIntentSha256
        $receipt = Read-EvidenceChecked $candidate.protectionReceiptPath $candidate.protectionReceiptSha256
        $selection = Read-EvidenceChecked ([IO.Path]::Combine($root,'selection-receipt.json')) $candidate.selectionReceiptSha256
        $configPath = $intent.configPath
        # The intent contains the exact config bytes and location; no current profile lookup may replace them.
        if ($configPath -isnot [string]) { throw 'Evidence config provenance missing.' }
        $config = Read-EvidenceChecked $configPath $candidate.configSha256
        $configCheck = Read-EvidenceConfig $configPath
        if ($configCheck.Hash -cne $candidate.configSha256 -or
            $configCheck.SettingsHash -cne $candidate.settingsBeforeSha256) {
            throw 'Evidence config changed.'
        }
        $settingsBefore = Read-EvidenceChecked $config.settingsEvidencePath $candidate.settingsBeforeSha256
        $settingsAfter = Read-EvidenceChecked $SettingsAfterPath $SettingsAfterHash
        $afterConfig = [pscustomobject]@{ endpoint=$config.endpoint; bucket=$config.bucket; observedAt=$settingsAfter.observedAt
            prefix=$config.prefix }
        Assert-EvidenceSettings $settingsAfter $afterConfig
        $afterAt=Assert-Utc $settingsAfter.observedAt
        $completedAt=Assert-Utc $receipt.completedAt
        $now=[DateTimeOffset]::UtcNow
        if ($afterAt -le $completedAt -or $afterAt -gt $now -or $afterAt -lt $now.AddHours(-24)) {
            throw 'Evidence post-publish observation unavailable.'
        }
        if (-not (Test-EvidenceSettingsEquivalent $settingsBefore $settingsAfter)) { throw 'Evidence settings changed.' }
        if ($intent.operationId -cne $result.operationId -or $intent.runId -cne $candidate.runId -or
            $intent.bundleKey -cne $candidate.key -or $intent.packageBytes -ne $candidate.packageBytes -or
            $intent.packageSha256 -cne $candidate.packageSha256 -or $intent.manifestSha256 -cne $candidate.manifestSha256 -or
            $intent.generation -cne $receipt.generation -or $intent.configSha256 -cne $candidate.configSha256 -or
            $intent.evidenceBase -cne $candidate.evidenceBase -or $intent.evidenceHead -cne $candidate.evidenceHead -or
            $intent.producerBase -cne $candidate.producerBase -or $intent.producerHead -cne $candidate.producerHead -or
            $intent.sourceManifestSha256 -cne $candidate.sourceManifestSha256 -or
            $intent.selectionReceiptSha256 -cne $candidate.selectionReceiptSha256 -or
            $putIntent.bundleKey -cne $candidate.key -or $putIntent.intentSha256 -cne $candidate.intentSha256 -or
            $putIntent.serverLockLowerBound -cne $candidate.serverLockLowerBound -or
            $putIntent.operationId -cne $result.operationId -or
            $putIntent.packageBytes -ne $candidate.packageBytes -or
            $putIntent.packageSha256 -cne $candidate.packageSha256 -or
            $putIntent.manifestSha256 -cne $candidate.manifestSha256 -or
            $putIntent.state -cne 'may-have-been-sent' -or
            $receipt.operationId -cne $result.operationId -or $receipt.runId -cne $candidate.runId -or
            $receipt.intentSha256 -cne $candidate.intentSha256 -or
            $receipt.putIntentSha256 -cne $candidate.putIntentSha256 -or
            $receipt.bundleKey -cne $candidate.key -or
            $receipt.witnessKey -cne $intent.witnessKey -or $receipt.controlKey -cne $intent.controlKey -or
            $receipt.generation -cne $intent.generation -or $receipt.configSha256 -cne $candidate.configSha256 -or
            $receipt.packageBytes -ne $candidate.packageBytes -or
            $receipt.packageSha256 -cne $candidate.packageSha256 -or
            $receipt.manifestSha256 -cne $candidate.manifestSha256 -or
            $receipt.serverLockLowerBound -cne $candidate.serverLockLowerBound) {
            throw 'Evidence provenance mismatch.'
        }
        $prefix="evidence/first-use/$($config.repositoryId)/$($candidate.evidenceHead)/$($candidate.runId)/"
        if ($intent.witnessKey -cne ($prefix+'protection-witness.txt') -or
            $intent.controlKey -cne ('probe/unlocked/'+$result.operationId+'/control.txt') -or
            $intent.configPath -cne $configPath -or
            $intent.settingsBeforeSha256 -cne $candidate.settingsBeforeSha256) {
            throw 'Evidence intent identity mismatch.'
        }
        $expectedLower = (Assert-Utc $putIntent.putIntentAt).AddSeconds(2592000).ToString('o')
        if ($expectedLower -cne $candidate.serverLockLowerBound -or
            (Assert-Utc $putIntent.putIntentAt) -lt (Assert-Utc $intent.createdAt) -or
            (Assert-Utc $putIntent.putIntentAt) -gt $completedAt) { throw 'Evidence retention mismatch.' }
        if ($selection.evidenceBase -cne $candidate.evidenceBase -or $selection.evidenceHead -cne $candidate.evidenceHead -or
            $selection.producerHead -cne $candidate.producerHead -or
            $selection.entries -isnot [array] -or $selection.entries.Count -lt 4) { throw 'Evidence selection mismatch.' }
        $expectedEntries = @($selection.entries | ForEach-Object { [ordered]@{path=$_.path;bytes=$_.expectedBytes;sha256=$_.expectedSha256} })
        foreach ($entry in $selection.entries) {
            if ($entry.expectedBytes -ne $entry.actualBytes -or $entry.expectedSha256 -cne $entry.actualSha256) {
                throw 'Evidence selection bytes mismatch.'
            }
        }
        if ($selection.kind -ceq 'E1') {
            $sourceManifestPath=[IO.Path]::Combine($root,'snapshot','source','manifest.json')
            if ((Get-FileHash -LiteralPath $sourceManifestPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $candidate.sourceManifestSha256) {
                throw 'Source manifest mismatch.'
            }
            $expectedEntries += [ordered]@{path='source/manifest.json';bytes=([IO.FileInfo]::new($sourceManifestPath)).Length;sha256=$candidate.sourceManifestSha256}
            $receiptPath=[IO.Path]::Combine($root,'snapshot','selection-receipt.json')
            $expectedEntries += [ordered]@{path='selection-receipt.json';bytes=([IO.FileInfo]::new($receiptPath)).Length;sha256=$candidate.selectionReceiptSha256}
        }
        if ($candidate.entries.Count -ne $expectedEntries.Count) { throw 'Evidence entry set mismatch.' }
        foreach ($expected in $expectedEntries) {
            $actual=@($candidate.entries | Where-Object { $_.path -ceq $expected.path })
            if ($actual.Count -ne 1 -or $actual[0].bytes -ne $expected.bytes -or
                $actual[0].sha256 -cne $expected.sha256) { throw 'Evidence entry changed.' }
        }
        if ($receipt.observations -isnot [array] -or $receipt.observations.Count -ne 13) {
            throw 'Evidence observation set mismatch.'
        }
        $obs = $receipt.observations
        $witnessBytes=[Text.Encoding]::UTF8.GetBytes('evidence-witness-'+$candidate.runId)
        $witnessHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($witnessBytes)).ToLowerInvariant()
        $controlBytes=[Text.Encoding]::UTF8.GetBytes('evidence-control-'+$candidate.runId)
        $controlHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($controlBytes)).ToLowerInvariant()
        Assert-EvidenceNetworkObservation $obs[0] 'witness-put' 'put' $intent.witnessKey $intent.generation 'success'
        Assert-EvidenceReadbackObservation $obs[1] 'witness-first-readback' $intent.witnessKey $intent.generation $witnessBytes.Length $witnessHash
        Assert-EvidenceNetworkObservation $obs[2] 'witness-overwrite' 'put' $intent.witnessKey $intent.generation 'lock'
        Assert-EvidenceNetworkObservation $obs[3] 'witness-delete' 'delete' $intent.witnessKey $intent.generation 'lock'
        Assert-EvidenceReadbackObservation $obs[4] 'witness-final-readback' $intent.witnessKey $intent.generation $witnessBytes.Length $witnessHash
        Assert-EvidenceNetworkObservation $obs[5] 'control-put' 'put' $intent.controlKey $intent.generation 'success'
        Assert-EvidenceNetworkObservation $obs[6] 'control-get' 'get' $intent.controlKey $intent.generation 'success' $controlBytes.Length $controlHash
        Assert-EvidenceNetworkObservation $obs[7] 'control-delete' 'delete' $intent.controlKey $intent.generation 'success'
        Assert-EvidenceNetworkObservation $obs[8] 'control-absence' 'get' $intent.controlKey $intent.generation 'missing'
        Assert-EvidenceNetworkObservation $obs[9] 'bundle-absence' 'get' $candidate.key $intent.generation 'missing'
        Assert-EvidenceNetworkObservation $obs[10] 'bundle-put' 'put' $candidate.key $intent.generation 'success'
        Assert-EvidenceReadbackObservation $obs[11] 'bundle-readback' $candidate.key $intent.generation $candidate.packageBytes $candidate.packageSha256
        Assert-EvidenceReadbackObservation $obs[12] 'witness-after-bundle' $intent.witnessKey $intent.generation $witnessBytes.Length $witnessHash
        $reference = Read-ArtifactReference $candidate.reference $configCheck
        if ($reference.key -cne $candidate.key -or $reference.manifestSha256 -cne $candidate.manifestSha256 -or
            $reference.packageBytes -ne $candidate.packageBytes -or $reference.base -cne $candidate.evidenceBase -or
            $reference.head -cne $candidate.evidenceHead -or $reference.retainUntil -cne $candidate.serverLockLowerBound) {
            throw 'Evidence reference mismatch.'
        }
        $archive = [IO.Path]::Combine($root,'bundle.zip')
        if (([IO.FileInfo]::new($archive)).Length -ne $candidate.packageBytes -or
            (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant() -cne $candidate.packageSha256) {
            throw 'Evidence package changed.'
        }
        $ledgerId = [Guid]::NewGuid().ToString('N')
        $ledger = [ordered]@{ schemaVersion=1; ledgerId=$ledgerId; candidatePath=$CandidatePath
            candidateSha256=$CandidateHash; operationId=$result.operationId; runId=$candidate.runId
            reference=$candidate.reference; key=$candidate.key; packageBytes=$candidate.packageBytes
            packageSha256=$candidate.packageSha256; manifestSha256=$candidate.manifestSha256
            sourceManifestSha256=$candidate.sourceManifestSha256; evidenceBase=$candidate.evidenceBase
            evidenceHead=$candidate.evidenceHead; producerBase=$candidate.producerBase; producerHead=$candidate.producerHead
            kind=$selection.kind; entries=@($candidate.entries); configPath=$configPath; configSha256=$candidate.configSha256
            settingsBeforePath=$config.settingsEvidencePath; settingsBeforeSha256=$candidate.settingsBeforeSha256
            settingsAfterPath=$SettingsAfterPath; settingsAfterSha256=$SettingsAfterHash
            intentPath=$candidate.intentPath; intentSha256=$candidate.intentSha256
            putIntentPath=$candidate.putIntentPath; putIntentSha256=$candidate.putIntentSha256
            selectionReceiptPath=[IO.Path]::Combine($root,'selection-receipt.json'); selectionReceiptSha256=$candidate.selectionReceiptSha256
            protectionReceiptPath=$candidate.protectionReceiptPath; protectionReceiptSha256=$candidate.protectionReceiptSha256
            serverLockLowerBound=$candidate.serverLockLowerBound
            retentionRule='owner D close + 30 days minimum and while referenced'
            owner='OSM maintainer'; closedAt=$null; retentionObligationUntil=$null
            committedAt=[DateTimeOffset]::UtcNow.ToString('o') }
        $fixed = Write-EvidenceImmutable 'evidence-ledgers' $ledgerId 'ledger.json' $ledger
        return [pscustomobject]@{ schemaVersion=1; status='passed'; ledgerId=$ledgerId; ledgerPath=$fixed.Path; ledgerSha256=$fixed.Sha256 }
    } catch { return [pscustomobject]@{ schemaVersion=1; status='failed'; ledgerId=$null; ledgerPath=$null; ledgerSha256=$null } }
}

function Write-EvidenceReaderInput([string] $E1Ledger,[string] $E1Hash,[string] $E2Ledger,[string] $E2Hash,
    [string] $FreshConfig,[string] $FreshConfigHash) {
    $e1 = Read-EvidenceChecked $E1Ledger $E1Hash
    $e2 = Read-EvidenceChecked $E2Ledger $E2Hash
    Assert-EvidenceLedgerPath $E1Ledger $e1
    Assert-EvidenceLedgerPath $E2Ledger $e2
    $config = Read-EvidenceChecked $FreshConfig $FreshConfigHash
    $verifiedConfig = Read-EvidenceConfig $FreshConfig
    if ($verifiedConfig.Hash -cne $FreshConfigHash -or $e1.kind -cne 'E1' -or $e2.kind -cne 'E2' -or
        $e1.producerHead -cne $e2.producerHead -or $e2.evidenceHead -cne $e2.producerHead -or
        $e1.evidenceBase -cne 'fd7ebf932d230a522293dee72572cbdeeac1c8fa' -or
        $e1.evidenceHead -cne 'caed8bae55c033673b75532dc650f0a3a3cedc48' -or
        $e2.evidenceBase -cne '6d804ca637cf42fb876e602c8ccc0656cfbd255d' -or
        $config.repositoryId -cne '4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b') {
        throw 'Reader handoff identity unavailable.'
    }
    foreach ($value in @($e1,$e2)) {
        $reference=Read-ArtifactReference $value.reference $verifiedConfig
        if ($reference.key -cne $value.key -or $reference.manifestSha256 -cne $value.manifestSha256 -or
            $reference.packageBytes -ne $value.packageBytes -or $reference.base -cne $value.evidenceBase -or
            $reference.head -cne $value.evidenceHead) { throw 'Reader handoff reference mismatch.' }
    }
    $items = foreach ($item in @(@{value=$e1;path=$E1Ledger;hash=$E1Hash},@{value=$e2;path=$E2Ledger;hash=$E2Hash})) {
        $value=$item.value
        [ordered]@{ kind=$value.kind; ledgerPath=$item.path; ledgerSha256=$item.hash
            reference=$value.reference; key=$value.key; packageBytes=$value.packageBytes
            packageSha256=$value.packageSha256; manifestSha256=$value.manifestSha256
            sourceManifestSha256=$value.sourceManifestSha256; evidenceBase=$value.evidenceBase
            evidenceHead=$value.evidenceHead; producerHead=$value.producerHead; entrySet=@($value.entries)
            retentionRule=$value.retentionRule; owner=$value.owner
            openPaths=if($value.kind -ceq 'E1'){@('source/final-offline/c-raw/ArtifactTransfer.stdout.log','source/continuation/current-run/publish-operation/operation-observations.json')}
                else {@('settings-overview.jpg','settings-rules.jpg')} }
    }
    $id=[Guid]::NewGuid().ToString('N')
    $input=[ordered]@{ schemaVersion=1; handoffId=$id; configPath=$FreshConfig; configSha256=$FreshConfigHash
        items=@($items); createdAt=[DateTimeOffset]::UtcNow.ToString('o') }
    return Write-EvidenceImmutable 'evidence-handoffs' $id 'reader-input.json' $input
}

function Read-EvidenceReaderInput([string] $Path,[string] $ExpectedHash) {
    $input=Read-EvidenceChecked $Path $ExpectedHash
    if ($input.schemaVersion -ne 1 -or $input.items.Count -ne 2) { throw 'Reader input unavailable.' }
    $config=Read-EvidenceChecked $input.configPath $input.configSha256
    $verified=Read-EvidenceConfig $input.configPath
    if ($verified.Hash -cne $input.configSha256) { throw 'Reader config mismatch.' }
    foreach ($item in $input.items) {
        $ledger=Read-EvidenceChecked $item.ledgerPath $item.ledgerSha256
        Assert-EvidenceLedgerPath $item.ledgerPath $ledger
        foreach ($name in @('kind','reference','key','packageBytes','packageSha256','manifestSha256',
                'sourceManifestSha256','evidenceBase','evidenceHead','producerHead','retentionRule','owner')) {
            if ($ledger.$name -cne $item.$name) { throw 'Reader ledger mismatch.' }
        }
        $reference=Read-ArtifactReference $item.reference $verified
        if ($reference.key -cne $item.key -or $reference.packageBytes -ne $item.packageBytes -or
            $reference.manifestSha256 -cne $item.manifestSha256 -or
            $reference.base -cne $item.evidenceBase -or $reference.head -cne $item.evidenceHead) {
            throw 'Reader reference mismatch.'
        }
        if ((ConvertTo-Json -InputObject $ledger.entries -Compress -Depth 12) -cne
            (ConvertTo-Json -InputObject $item.entrySet -Compress -Depth 12)) { throw 'Reader entry mismatch.' }
        $expectedOpen=if($item.kind -ceq 'E1'){@('source/final-offline/c-raw/ArtifactTransfer.stdout.log',
                'source/continuation/current-run/publish-operation/operation-observations.json')}
            elseif($item.kind -ceq 'E2'){@('settings-overview.jpg','settings-rules.jpg')}
            else{throw 'Reader kind unavailable.'}
        if (($item.openPaths -join '|') -cne ($expectedOpen -join '|')) { throw 'Reader open path mismatch.' }
    }
    if ($input.items[0].kind -cne 'E1' -or $input.items[1].kind -cne 'E2' -or
        $input.items[0].producerHead -cne $input.items[1].producerHead -or
        $input.items[0].evidenceBase -cne 'fd7ebf932d230a522293dee72572cbdeeac1c8fa' -or
        $input.items[0].evidenceHead -cne 'caed8bae55c033673b75532dc650f0a3a3cedc48' -or
        $input.items[1].evidenceBase -cne '6d804ca637cf42fb876e602c8ccc0656cfbd255d' -or
        $input.items[1].evidenceHead -cne $input.items[1].producerHead) { throw 'Reader pair mismatch.' }
    return $input
}

function Write-EvidenceCloseRecord([string] $E1Ledger,[string] $E1Hash,[string] $E2Ledger,[string] $E2Hash,
    [string] $CloseEventPath,[string] $CloseEventHash) {
    $e1=Read-EvidenceChecked $E1Ledger $E1Hash; $e2=Read-EvidenceChecked $E2Ledger $E2Hash
    Assert-EvidenceLedgerPath $E1Ledger $e1; Assert-EvidenceLedgerPath $E2Ledger $e2
    if ($e1.kind -cne 'E1' -or $e2.kind -cne 'E2') { throw 'Close ledger pair unavailable.' }
    $event=Read-EvidenceChecked $CloseEventPath $CloseEventHash
    Assert-Fields $event @('schemaVersion','owner','ledgerIds','ledgerSha256','closedAt','consumerReferenceState','ownerInstructionReference')
    if ($event.schemaVersion -ne 1 -or $event.owner -cne 'OSM maintainer' -or
        $event.ledgerIds.Count -ne 2 -or $event.ledgerIds[0] -cne $e1.ledgerId -or
        $event.ledgerIds[1] -cne $e2.ledgerId -or
        $event.ledgerSha256[0] -cne $E1Hash -or $event.ledgerSha256[1] -cne $E2Hash -or
        $event.consumerReferenceState -isnot [string] -or [string]::IsNullOrWhiteSpace($event.consumerReferenceState) -or
        $event.ownerInstructionReference -isnot [string] -or [string]::IsNullOrWhiteSpace($event.ownerInstructionReference)) {
        throw 'Close event unavailable.'
    }
    $closed=Assert-Utc $event.closedAt
    if ($closed -gt [DateTimeOffset]::UtcNow) { throw 'Close event time unavailable.' }
    $closeIdentity=[Text.Encoding]::UTF8.GetBytes($E1Hash + $E2Hash)
    $id=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($closeIdentity)).ToLowerInvariant().Substring(0,32)
    $record=[ordered]@{ schemaVersion=1; recordId=$id; owner=$event.owner; ledgerIds=@($event.ledgerIds)
        ledgerSha256=@($event.ledgerSha256); closedAt=$event.closedAt
        retentionObligationUntil=$closed.AddDays(30).ToString('o')
        consumerReferenceState=$event.consumerReferenceState; ownerInstructionReference=$event.ownerInstructionReference
        eventPath=$CloseEventPath; eventSha256=$CloseEventHash }
    return Write-EvidenceImmutable 'evidence-retention' $id 'retention.json' $record
}

Export-ModuleMember -Function Invoke-EvidenceCommit, Write-EvidenceReaderInput, Read-EvidenceReaderInput, Write-EvidenceCloseRecord
