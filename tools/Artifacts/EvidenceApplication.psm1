Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactProtectionPolicy.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1') -Force

$script:ProducerBase = '6d804ca637cf42fb876e602c8ccc0656cfbd255d'
$script:E1Root = 'C:\Users\void\AppData\Local\OneStarMaker\Artifacts\review-evidence\artifact-cli-final-r3-cec0e01b2bb6433e9cab782ddcc84df4'
$script:E1Base = 'fd7ebf932d230a522293dee72572cbdeeac1c8fa'
$script:E1Head = 'caed8bae55c033673b75532dc650f0a3a3cedc48'
$script:E1ManifestHash = 'b6e3dbf63c1bf925c36c966e64022bb6d1b14897f04aeb1af7af4082a2693ee4'
$script:E1Fixed = @(
    @('final-offline/snapshots/A3.md',29410,'31f78fdbeb01f0b82a24e3c6d4dda432f64ad83e3fd1890388f74009903faceb'),
    @('final-offline/snapshots/B_RESULT.md',2115,'a5a5f60e455c0852c12795d4338db4148368164b8c4297e4823a2fb8d6215d3c'),
    @('final-offline/snapshots/implementation.diff',206304,'e73495ccc6527639999aef6badaa1d72afd1714dfa1bb91ecc4ad84543cbe8cb'),
    @('final-offline/snapshots/implementation.name-status',897,'06e367fa4bbc7109842394b83e0849af6fbad021aaa4eac25009ca2575e27c13'),
    @('final-offline/snapshots/implementation.stat',1335,'1daa6b2d0bdd3b46bcbcbcdc905dc7f8928bea5737759ffa74b6a1f1fa8c93ca'),
    @('final-offline/c-raw/ArtifactTransfer.stdout.log',331,'1e34e44e63c18d707650856f8e2a5daf6ac9e745cdcbba4b80c5c9d9925538cc'),
    @('final-offline/c-raw/ArtifactPackage.stdout.log',237,'d9857c384c5b3ef45c4092eab5d0c91ee6ad591b2d0c74dcd2a16ec0037691ef'),
    @('continuation/current-run/publish-operation/operation-observations.json',6366,'b8b54e6331e933ba19d1227302b107056012c2749e022a7519b161e008649d51'),
    @('continuation/current-run/publish-operation/protection-receipt.json',6371,'1b1cf9d643c8f43c7cf9913ce19e203636bba70e5241da1918994c9f9d20e2c3'),
    @('continuation/raw/fetch-reader/verification.json',2761,'6fd72d986c5259aa4e799da5b9201288f88cba4c1942c6ba4a6949dbb89ed4dd'),
    @('continuation/settings-provenance.json',3195,'971c93075eb097755c60f3a5db4480ae15f9ade2f90601fefd74d1a33bdd950a'),
    @('continuation/observations/bucket-before.json',1174,'cd6a4fd59f533cee313460c67de895c409143e56bae0dcee7ff15061770ae200'),
    @('continuation/observations/bucket-after.json',1131,'03e5a12f96bd7383b46ac03dd4f480b0d997a796d307c65924bccf04f229c99a')
)

function Assert-EvidenceSelection($Selection,[string] $Base,[string] $Head,[string] $ProducerHead,$Config) {
    Assert-Fields $Selection @('schemaVersion','kind','root','producerBase','producerHead','sourceManifestSha256','files')
    if ($Selection.schemaVersion -isnot [long] -or $Selection.schemaVersion -ne 1 -or
        $Selection.kind -cnotin @('E1','E2') -or $Selection.root -isnot [string] -or
        -not [IO.Path]::IsPathFullyQualified($Selection.root) -or
        $Selection.producerBase -cne $script:ProducerBase -or $Selection.producerHead -cne $ProducerHead -or
        $Selection.files -isnot [array]) { throw 'Evidence selection unavailable.' }
    $root = [IO.Path]::GetFullPath($Selection.root)
    Assert-NoArtifactReparse $root
    if (-not [IO.Directory]::Exists($root) -or (Test-ArtifactWithin $root (Get-ArtifactRoot)) -or
        (Test-ArtifactWithin $root ([IO.Path]::Combine([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData),'OneStarMaker','Artifacts','credentials')))) {
        throw 'Evidence source unavailable.'
    }
    if ($Selection.kind -ceq 'E1') {
        if ($Base -cne $script:E1Base -or $Head -cne $script:E1Head -or
            $root -cne $script:E1Root -or $Selection.sourceManifestSha256 -cne $script:E1ManifestHash -or
            $Selection.files.Count -ne 13) { throw 'E1 selection changed.' }
    } else {
        if ($Base -cne $script:ProducerBase -or $Head -cne $ProducerHead -or
            $Selection.sourceManifestSha256 -ne $null -or $Selection.files.Count -ne 4 -or
            -not ([IO.Path]::GetFullPath($Config.Data.settingsEvidencePath).Equals([IO.Path]::Combine($root,'settings-before.json'),[StringComparison]::OrdinalIgnoreCase))) {
            throw 'E2 selection changed.'
        }
    }
    $expectedE2 = @('settings-before.json','settings-overview.jpg','settings-rules.jpg','capture.json')
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($file in $Selection.files) {
        Assert-Fields $file @('source','path','bytes','sha256')
        if ($file.source -isnot [string] -or $file.path -isnot [string] -or
            $file.bytes -isnot [long] -or $file.bytes -lt 0 -or $file.bytes -gt 256MB -or
            $file.sha256 -isnot [string] -or $file.sha256 -cnotmatch '\A[0-9a-f]{64}\z' -or
            -not $seen.Add($file.path)) { throw 'Evidence entry unavailable.' }
        $null = [OneStarMaker.Artifacts.Packaging.PackagePolicy]::Relative($file.path)
        $null = [OneStarMaker.Artifacts.Packaging.PackagePolicy]::Relative($file.source)
        if ($Selection.kind -ceq 'E1') {
            $fixed = @($script:E1Fixed | Where-Object { $_[0] -ceq $file.source })
            if ($fixed.Count -ne 1 -or $file.path -cne ('source/' + $file.source) -or
                $file.bytes -ne $fixed[0][1] -or $file.sha256 -cne $fixed[0][2]) { throw 'E1 entry changed.' }
        } elseif ($file.source -cne $file.path -or $file.path -cnotin $expectedE2) {
            throw 'E2 entry changed.'
        }
    }
    if ($Selection.kind -ceq 'E2' -and @($expectedE2 | Where-Object { -not $seen.Contains($_) }).Count -ne 0) {
        throw 'E2 entry missing.'
    }
    if ($Selection.kind -ceq 'E2') {
        $capture = (Read-ArtifactJson ([IO.Path]::Combine($root,'capture.json'))).Data
        Assert-Fields $capture @('schemaVersion','implementationHead','bucket','page','capturedAt','observer','tool',
            'overviewBytes','overviewSha256','rulesBytes','rulesSha256','observationScope')
        if ($capture.schemaVersion -ne 1 -or $capture.implementationHead -cne $ProducerHead -or
            $capture.bucket -cne 'osm-artifacts' -or $capture.page -cne 'settings' -or
            [string]::IsNullOrWhiteSpace($capture.observer) -or [string]::IsNullOrWhiteSpace($capture.tool)) {
            throw 'Evidence capture unavailable.'
        }
        $null = Assert-Utc $capture.capturedAt
        Assert-Fields $capture.observationScope @('overview','rules')
        foreach ($name in @('overview','rules')) {
            if ($capture.observationScope.$name -isnot [string] -or
                [string]::IsNullOrWhiteSpace($capture.observationScope.$name) -or
                $capture.observationScope.$name.Length -gt 512 -or
                $capture.observationScope.$name -cmatch '[\x00-\x1f]') { throw 'Evidence observation scope unavailable.' }
        }
        foreach ($pair in @(@('settings-overview.jpg','overviewSha256','overviewBytes'),
                          @('settings-rules.jpg','rulesSha256','rulesBytes'))) {
            $entry = @($Selection.files | Where-Object path -CEQ $pair[0])[0]
            if ($capture.($pair[1]) -cne $entry.sha256 -or $capture.($pair[2]) -isnot [long] -or
                $capture.($pair[2]) -ne $entry.bytes) { throw 'Evidence capture hash or size mismatch.' }
            $imagePath = [OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($root,$pair[0])
            $image = [IO.FileStream]::new($imagePath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
            try {
                if ($image.Length -ne $entry.bytes -or $image.Length -lt 4 -or $image.Length -gt 256MB) {
                    throw 'Evidence image size unavailable.'
                }
                $first=[byte[]]::new(2); $last=[byte[]]::new(2)
                if ($image.Read($first,0,2) -ne 2) { throw 'Evidence image header unavailable.' }
                $null=$image.Seek(-2,[IO.SeekOrigin]::End)
                if ($image.Read($last,0,2) -ne 2 -or $first[0] -ne 255 -or $first[1] -ne 216 -or
                    $last[0] -ne 255 -or $last[1] -ne 217) {
                    throw 'Evidence image format unavailable.'
                }
            } finally { $image.Dispose() }
        }
    }
}

function Copy-EvidenceChecked([string] $Root,[string] $Source,[string] $Target,[long] $Bytes,[string] $Hash,
    [Diagnostics.Stopwatch] $Timer) {
    $sourcePath = [OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Root,$Source)
    if (-not [IO.File]::Exists($sourcePath) -or [IO.Directory]::Exists($sourcePath)) { throw 'Evidence source missing.' }
    if ($Bytes -lt 0 -or $Bytes -gt 256MB -or $Timer.ElapsedMilliseconds -ge 600000) { throw 'Evidence source limit.' }
    $destination = [IO.Path]::GetFullPath($Target)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    $reader = [IO.FileStream]::new($sourcePath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
    try {
        if ($reader.Length -ne $Bytes -or $reader.Length -gt 256MB) { throw 'Evidence source size changed.' }
        $writer = [IO.FileStream]::new($destination,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        $digest=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
        try {
            $buffer=[byte[]]::new(65536); $copied=[long]0
            while (($read=$reader.Read($buffer,0,$buffer.Length)) -gt 0) {
                if ($Timer.ElapsedMilliseconds -ge 600000 -or $read -gt $Bytes-$copied -or
                    $read -gt 256MB-$copied) { throw 'Evidence source limit.' }
                $writer.Write($buffer,0,$read); $digest.AppendData($buffer,0,$read); $copied+=$read
            }
            $writer.Flush($true)
            $actualHash=[Convert]::ToHexString($digest.GetHashAndReset()).ToLowerInvariant()
        } finally { $digest.Dispose(); $writer.Dispose() }
    } finally { $reader.Dispose() }
    if ($copied -ne $Bytes -or $actualHash -cne $Hash) {
        throw 'Evidence source changed.'
    }
    return $actualHash
}

function Invoke-EvidencePublish([string] $ConfigPath,[string] $SelectionPath,[string] $Base,[string] $Head) {
    $operationRoot = $null; $id = [Guid]::NewGuid().ToString('N'); $timer=[Diagnostics.Stopwatch]::StartNew()
    $result = New-ArtifactResult $id 'evidence-publish'
    try {
        Assert-Hex $Base 40; Assert-Hex $Head 40
        $config = Read-EvidenceConfig $ConfigPath
        $selection = (Read-ArtifactJson $SelectionPath).Data
        $repo = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..','..'))
        $producerHead = (& git -C $repo rev-parse HEAD 2>$null).Trim()
        Assert-Hex $producerHead 40
        $assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'Packaging/artifacts/package/ArtifactPackaging.dll'))
        if ($null -eq $assembly.GetType('OneStarMaker.Artifacts.Packaging.PackageIO',$true)) { throw 'Packaging unavailable.' }
        Assert-EvidenceSelection $selection $Base $Head $producerHead $config
        $operationRoot = New-ArtifactOperation; $result.residue.localPath=$operationRoot
        $snapshot = [IO.Path]::Combine($operationRoot,'snapshot')
        [IO.Directory]::CreateDirectory($snapshot) | Out-Null
        $receiptEntries = [Collections.Generic.List[object]]::new()
        $packageFiles = [Collections.Generic.List[OneStarMaker.Artifacts.Packaging.PackageFile]]::new()
        foreach ($file in $selection.files) {
            $target = [OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($snapshot,$file.path)
            $actualHash=Copy-EvidenceChecked $selection.root $file.source $target $file.bytes $file.sha256 $timer
            $receiptEntries.Add([ordered]@{ source=$file.source; path=$file.path; expectedBytes=$file.bytes
                actualBytes=([IO.FileInfo]::new($target)).Length; expectedSha256=$file.sha256
                actualSha256=$actualHash })
            $entry = [OneStarMaker.Artifacts.Packaging.PackageFile]::new()
            $entry.Path=$file.path; $entry.Bytes=$file.bytes; $entry.Sha256=$file.sha256
            $packageFiles.Add($entry)
        }
        $sourceManifestHash = if ($selection.kind -ceq 'E1') { $script:E1ManifestHash } else { '0'*64 }
        if ($selection.kind -ceq 'E1') {
            $null=Copy-EvidenceChecked $selection.root 'manifest.json' ([IO.Path]::Combine($snapshot,'source','manifest.json')) `
                ([IO.FileInfo]::new([IO.Path]::Combine($selection.root,'manifest.json'))).Length $script:E1ManifestHash $timer
            $entry = [OneStarMaker.Artifacts.Packaging.PackageFile]::new()
            $entry.Path='source/manifest.json'; $entry.Bytes=([IO.FileInfo]::new([IO.Path]::Combine($snapshot,'source','manifest.json'))).Length
            $entry.Sha256=$script:E1ManifestHash; $packageFiles.Add($entry)
        }
        $selectionReceipt = [ordered]@{ schemaVersion=1; kind=$selection.kind; sourceManifestSha256=$selection.sourceManifestSha256
            evidenceBase=$Base; evidenceHead=$Head; producerBase=$script:ProducerBase; producerHead=$producerHead
            capturedAt=[DateTimeOffset]::UtcNow.ToString('o'); entries=@($receiptEntries.ToArray()) }
        $receiptPath = [IO.Path]::Combine($operationRoot,'selection-receipt.json')
        $receiptHash = Write-ArtifactJson $receiptPath $selectionReceipt
        if ($selection.kind -ceq 'E1') {
            [IO.File]::Copy($receiptPath,[IO.Path]::Combine($snapshot,'selection-receipt.json'))
            $entry = [OneStarMaker.Artifacts.Packaging.PackageFile]::new()
            $entry.Path='selection-receipt.json'; $entry.Bytes=([IO.FileInfo]::new($receiptPath)).Length
            $entry.Sha256=$receiptHash; $packageFiles.Add($entry)
        }
        Protect-ArtifactTree $operationRoot
        $runId = [Guid]::NewGuid().ToString('N')
        $package = [OneStarMaker.Artifacts.Packaging.PackageIO]::CreateVerified($operationRoot,$snapshot,$Base,$Head,
            $config.Data.repositoryId,$runId,$packageFiles.ToArray(),(Get-Remaining $timer 600000))
        Protect-ArtifactTree $operationRoot
        $result = Invoke-ArtifactPublishPrepared $config $package $operationRoot $id $runId $Base $Head `
            $script:ProducerBase $producerHead $sourceManifestHash $receiptHash $packageFiles.ToArray() $timer
    } catch {
        $result.status='failed'; $result.reasonCode='unconfirmed'; $result.ledger=$null; $result.outputPath=$null
        if ($operationRoot) {
            $result.residue.localPath=$operationRoot; $result.residue.cleanup='local-retained'
            $result.residue.remote=if ([IO.File]::Exists([IO.Path]::Combine($operationRoot,'put-intent.json'))) { 'may-have-been-sent' } else { 'pre-put-intent' }
            try { $null=Write-ArtifactJson ([IO.Path]::Combine($operationRoot,'result.json')) $result; Protect-ArtifactTree $operationRoot } catch {}
        }
    }
    return [pscustomobject]$result
}

Export-ModuleMember -Function Invoke-EvidencePublish, Assert-EvidenceSelection
