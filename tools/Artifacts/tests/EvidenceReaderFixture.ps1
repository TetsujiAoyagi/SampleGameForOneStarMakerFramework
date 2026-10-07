# This fixture builds reader/ledger input only. It never invokes the production E1 selector.
function New-EvidenceReaderFixture([string] $SourceRoot,[string] $OperationRoot,$Config,[string] $ProducerHead) {
    $evidenceBase='fd7ebf932d230a522293dee72572cbdeeac1c8fa'
    $evidenceHead='caed8bae55c033673b75532dc650f0a3a3cedc48'
    $producerBase='6d804ca637cf42fb876e602c8ccc0656cfbd255d'
    $names=@(
        'final-offline/snapshots/A3.md',
        'final-offline/snapshots/B_RESULT.md',
        'final-offline/snapshots/implementation.diff',
        'final-offline/snapshots/implementation.name-status',
        'final-offline/snapshots/implementation.stat',
        'final-offline/c-raw/ArtifactTransfer.stdout.log',
        'final-offline/c-raw/ArtifactPackage.stdout.log',
        'continuation/current-run/publish-operation/operation-observations.json',
        'continuation/current-run/publish-operation/protection-receipt.json',
        'continuation/raw/fetch-reader/verification.json',
        'continuation/settings-provenance.json',
        'continuation/observations/bucket-before.json',
        'continuation/observations/bucket-after.json'
    )
    [IO.Directory]::CreateDirectory($SourceRoot)|Out-Null
    $snapshot=[IO.Path]::Combine($OperationRoot,'snapshot')
    [IO.Directory]::CreateDirectory($snapshot)|Out-Null
    $files=[Collections.Generic.List[object]]::new()
    $receiptEntries=[Collections.Generic.List[object]]::new()
    $packageFiles=[Collections.Generic.List[OneStarMaker.Artifacts.Packaging.PackageFile]]::new()
    foreach($name in $names){
        $source=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($SourceRoot,$name)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($source))|Out-Null
        # Text is deterministic per entry and carries no owner evidence or credential bytes.
        [IO.File]::WriteAllText($source,"offline-reader-dummy: $name`n",[Text.UTF8Encoding]::new($false))
        $hash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        $bytes=([IO.FileInfo]::new($source)).Length
        $relative='source/'+$name
        $target=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($snapshot,$relative)
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
        [IO.File]::Copy($source,$target)
        $files.Add([ordered]@{source=$name;path=$relative;bytes=$bytes;sha256=$hash})
        $receiptEntries.Add([ordered]@{source=$name;path=$relative;expectedBytes=$bytes;actualBytes=$bytes
            expectedSha256=$hash;actualSha256=$hash})
        $entry=[OneStarMaker.Artifacts.Packaging.PackageFile]::new()
        $entry.Path=$relative;$entry.Bytes=$bytes;$entry.Sha256=$hash;$packageFiles.Add($entry)
    }
    $manifestPath=[IO.Path]::Combine($SourceRoot,'manifest.json')
    $manifest=[ordered]@{schemaVersion=1;kind='E1';files=@($files.ToArray())}
    [IO.File]::WriteAllText($manifestPath,(ConvertTo-Json -InputObject $manifest -Compress -Depth 12),[Text.UTF8Encoding]::new($false))
    $manifestHash=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestTarget=[IO.Path]::Combine($snapshot,'source','manifest.json')
    [IO.File]::Copy($manifestPath,$manifestTarget)
    $entry=[OneStarMaker.Artifacts.Packaging.PackageFile]::new()
    $entry.Path='source/manifest.json';$entry.Bytes=([IO.FileInfo]::new($manifestPath)).Length
    $entry.Sha256=$manifestHash;$packageFiles.Add($entry)
    $receipt=[ordered]@{schemaVersion=1;kind='E1';sourceManifestSha256=$manifestHash
        evidenceBase=$evidenceBase;evidenceHead=$evidenceHead;producerBase=$producerBase;producerHead=$ProducerHead
        capturedAt=[DateTimeOffset]::UtcNow.ToString('o');entries=@($receiptEntries.ToArray())}
    $receiptPath=[IO.Path]::Combine($OperationRoot,'selection-receipt.json')
    [IO.File]::WriteAllText($receiptPath,(ConvertTo-Json -InputObject $receipt -Compress -Depth 12),[Text.UTF8Encoding]::new($false))
    $receiptHash=(Get-FileHash -LiteralPath $receiptPath -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::Copy($receiptPath,[IO.Path]::Combine($snapshot,'selection-receipt.json'))
    $entry=[OneStarMaker.Artifacts.Packaging.PackageFile]::new()
    $entry.Path='selection-receipt.json';$entry.Bytes=([IO.FileInfo]::new($receiptPath)).Length
    $entry.Sha256=$receiptHash;$packageFiles.Add($entry)
    $runId=[Guid]::NewGuid().ToString('N')
    $package=[OneStarMaker.Artifacts.Packaging.PackageIO]::CreateVerified($OperationRoot,$snapshot,
        $evidenceBase,$evidenceHead,$Config.Data.repositoryId,$runId,$packageFiles.ToArray(),600000)
    return [pscustomobject]@{Package=$package;RunId=$runId;EvidenceBase=$evidenceBase;EvidenceHead=$evidenceHead
        ProducerBase=$producerBase;ProducerHead=$ProducerHead;SourceManifestHash=$manifestHash
        SelectionReceiptHash=$receiptHash;ExpectedFiles=$packageFiles.ToArray();SourceFiles=$files.ToArray()
        SourceManifestPath=$manifestPath;SelectionReceiptPath=$receiptPath;Snapshot=$snapshot}
}
