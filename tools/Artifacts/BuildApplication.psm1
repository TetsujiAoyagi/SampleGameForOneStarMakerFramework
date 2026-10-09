Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1')
Import-Module (Join-Path $PSScriptRoot 'ArtifactApplication.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildContract.psm1')
Import-Module (Join-Path $PSScriptRoot 'BuildStore.psm1')
Import-Module (Join-Path $PSScriptRoot 'Credentials/CredentialStore.psm1')
$script:Clock={ [DateTimeOffset]::UtcNow }
$script:FaultHook=$null
$script:NetworkHook=$null
$script:ReadbackHook=$null
$script:FailureHook=$null
$script:SnapshotLimit=240MB
$script:PackageCreatedHook=$null

function Get-BuildInventory([string]$Root){
    if([OneStarMaker.Artifacts.Packaging.PackagePolicy]::HasReparse($Root)){throw 'snapshot-conflict'}
    $names=[Collections.Generic.List[string]]::new();$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $pending=[Collections.Generic.Stack[string]]::new();$pending.Push($Root)
    while($pending.Count){
        $dir=$pending.Pop()
        foreach($item in [IO.Directory]::EnumerateFileSystemEntries($dir)){
            $attributes=[IO.File]::GetAttributes($item)
            if(($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'snapshot-conflict'}
            if(($attributes -band [IO.FileAttributes]::Directory) -ne 0){$pending.Push($item);continue}
            $relative=[IO.Path]::GetRelativePath($Root,$item).Replace([IO.Path]::DirectorySeparatorChar,'/')
            $null=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Relative($relative)
            if(-not $seen.Add($relative)){throw 'snapshot-conflict'}
            $names.Add($relative)
        }
    }
    return @($names.ToArray()|Sort-Object -CaseSensitive)
}
function Assert-BuildInventory($Selection){
    $actual=@(Get-BuildInventory $Selection.root);$chosen=@($Selection.files|Sort-Object -CaseSensitive)
    if($actual.Count -ne $chosen.Count -or (($actual -join "`n") -cne ($chosen -join "`n"))){throw 'snapshot-conflict'}
}
function Copy-BuildSnapshot($Selection,[string]$Operation,[Diagnostics.Stopwatch]$Timer){
    Assert-BuildInventory $Selection
    $handles=[Collections.Generic.List[IO.FileStream]]::new()
    $snapshot=[IO.Path]::Combine($Operation,'snapshot');[IO.Directory]::CreateDirectory($snapshot)|Out-Null
    $files=[Collections.Generic.List[OneStarMaker.Artifacts.Packaging.PackageFile]]::new()
    try{
        # 先に全handleをFileShare.Readで確保し、途中の書換えと集合変更を拒否する。
        foreach($name in $Selection.files){
            $source=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($Selection.root,$name)
            $handle=[IO.FileStream]::new($source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
            $handles.Add($handle)
        }
        Assert-BuildInventory $Selection
        $total=[long]0
        for($i=0;$i -lt $Selection.files.Count;$i++){
            $null=Get-Remaining $Timer 600000
            $name=$Selection.files[$i];$reader=$handles[$i]
            if($reader.Length -gt 256MB -or $total+$reader.Length -gt $script:SnapshotLimit){throw 'size-limit'}
            $target=[OneStarMaker.Artifacts.Packaging.PackagePolicy]::Under($snapshot,$name)
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
            $hash=[Security.Cryptography.IncrementalHash]::CreateHash([Security.Cryptography.HashAlgorithmName]::SHA256)
            try{
                $writer=[IO.FileStream]::new($target,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
                try{
                    $bytes=[long]0;$buffer=[byte[]]::new(65536);$n=0
                    while(($n=$reader.Read($buffer,0,$buffer.Length)) -gt 0){
                        $null=Get-Remaining $Timer 600000;$bytes+=$n;$total+=$n
                        if($bytes -gt 256MB -or $total -gt $script:SnapshotLimit){throw 'size-limit'}
                        $writer.Write($buffer,0,$n);$hash.AppendData($buffer,0,$n)
                    }
                    if($bytes -ne $reader.Length){throw 'snapshot-conflict'}
                    $writer.Flush($true)
                }finally{$writer.Dispose()}
                $entry=[OneStarMaker.Artifacts.Packaging.PackageFile]::new()
                $entry.Path=$name;$entry.Bytes=$bytes;$entry.Sha256=[Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
                $files.Add($entry)
            }finally{$hash.Dispose()}
        }
        Assert-BuildInventory $Selection
        return @{Files=$files.ToArray();Handles=$handles}
    }catch{foreach($handle in $handles){$handle.Dispose()};throw}
}
function Invoke-BuildNetwork([string]$Generation,[hashtable]$Request){
    if($script:NetworkHook){return & $script:NetworkHook $Generation $Request}
    return Invoke-ArtifactNetwork $Generation $Request
}
function Invoke-BuildReadback([string]$Operation,$Config,[string]$Generation,[string]$Key,[long]$Bytes,[string]$Hash,[Diagnostics.Stopwatch]$Timer){
    if($script:ReadbackHook){return & $script:ReadbackHook $Operation $Config $Generation $Key $Bytes $Hash}
    return Invoke-ArtifactReadback $Operation $Config $Generation $Key $Bytes $Hash $Timer
}
function Invoke-BuildPublish([string]$ConfigPath,[string]$SelectionPath){
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';reference=$null;packageSha256=$null;buildId=$null;receiptPath=$null;residue=[ordered]@{localPath=$null;remote='pre-put'}}
    $locks=$null;$packageLock=$null;$timer=[Diagnostics.Stopwatch]::StartNew()
    try{
        Import-ArtifactPackage
        $config=Read-BuildConfig $ConfigPath
        if([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($SelectionPath)) -ine [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ConfigPath))){throw 'invalid-selection'}
        $selection=Read-BuildSelection $SelectionPath
        $r.buildId=$selection.buildId
        $null=Get-BuildSeries $config.Data.repositoryId $selection.project $selection.target $selection.configuration
        Assert-BuildInventory $selection
        $dir=Get-BuildDirectory $config.Data.repositoryId $selection.buildId $true
        $operation=New-ArtifactOperation;$r.residue.localPath=$operation
        $snapshot=Copy-BuildSnapshot $selection $operation $timer;$locks=$snapshot.Handles
        $codec=[OneStarMaker.Artifacts.Packaging.BuildPackage]::new($config.Data.repositoryId,$selection.project,$selection.target,$selection.configuration,$selection.buildId)
        $package=$codec.CreateVerified($operation,[IO.Path]::Combine($operation,'snapshot'),$snapshot.Files,(Get-Remaining $timer 600000))
        if($script:PackageCreatedHook){& $script:PackageCreatedHook $package.Path}
        # hash確定直後に読取lockを取得し、同じlock下の最終検証からPUT完了まで維持する。
        # lock取得前の書換えは次のExtractの外hash検証で拒否する。
        $packageLock=[IO.FileStream]::new($package.Path,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
        $verified=$codec.Extract($package.Path,$operation,$package.Sha256,$package.ManifestSha256,(Get-Remaining $timer 600000))
        if($verified.Files.Count -ne $snapshot.Files.Count){throw 'package-invalid'}
        Protect-ArtifactTree $operation
        $key=Get-BuildKey $config $selection.project $selection.target $selection.configuration $selection.buildId
        $entries=@($snapshot.Files|Sort-Object Path -CaseSensitive|ForEach-Object{[pscustomobject]@{path=$_.Path;bytes=$_.Bytes;sha256=$_.Sha256}})
        $intent=[ordered]@{schemaVersion=1;purpose='build';buildId=$selection.buildId;repositoryId=$config.Data.repositoryId;project=$selection.project;target=$selection.target;configuration=$selection.configuration;seriesId=$codec.SeriesId;key=$key;configSha256=$config.Hash;evidenceConfigSha256=$config.EvidenceConfigSha256;packageBytes=$package.Bytes;packageSha256=$package.Sha256;manifestSha256=$package.ManifestSha256;entries=$entries;operationPath=$operation}
        $written=Write-BuildImmutable $dir 'intent.json' $intent
        if($script:FaultHook){& $script:FaultHook 'before-put' $intent}
        $generation=(Get-CredentialStatus 'osm').Generation;$r.residue.remote='may-have-been-sent'
        $put=Invoke-BuildNetwork $generation (New-ArtifactRequest $config 'put' $key $package.Path $null $timer)
        if(-not (Test-ArtifactSuccess $put 'put' $generation)){throw 'put-unconfirmed'}
        $read=Invoke-BuildReadback $operation $config $generation $key $package.Bytes $package.Sha256 $timer
        if(-not (Test-ArtifactReadback $read $generation $key $package.Bytes $package.Sha256)){throw 'readback-unconfirmed'}
        $published=(& $script:Clock).ToUniversalTime().ToString('o')
        $receipt=[ordered]@{schemaVersion=1;purpose='build';buildId=$selection.buildId;repositoryId=$config.Data.repositoryId;project=$selection.project;target=$selection.target;configuration=$selection.configuration;seriesId=$codec.SeriesId;key=$key;configSha256=$config.Hash;evidenceConfigSha256=$config.EvidenceConfigSha256;packageBytes=$package.Bytes;packageSha256=$package.Sha256;manifestSha256=$package.ManifestSha256;entries=$entries;intentSha256=$written.Sha256;readback=$read;publishState='succeeded';publishedAt=$published}
        if($script:FaultHook){& $script:FaultHook 'before-receipt' $receipt}
        $null=Write-BuildImmutable $dir 'receipt.json' $receipt
        if($script:FaultHook){& $script:FaultHook 'after-receipt' $receipt}
        $committed=Read-BuildReceipt $config $selection.buildId
        $r.reference=New-BuildReference $config $committed.Data;$r.packageSha256=$committed.Data.packageSha256
        $r.receiptPath=$committed.Path;$r.residue.remote='verified';$r.status='passed';$r.reasonCode='complete'
    }catch{
        if($script:FailureHook){& $script:FailureHook $_}
        $code=$_.Exception.Message
        if($code -cin @('already-exists','invalid-selection','invalid-source','snapshot-conflict','size-limit','put-unconfirmed','readback-unconfirmed','receipt-missing','receipt-invalid','invalid-build-config')){$r.reasonCode=$code}
        if($code -cin @('after-rename-unconfirmed')){$r.reasonCode='commit-unconfirmed'}
    }finally{if($packageLock){$packageLock.Dispose()};if($locks){foreach($handle in $locks){$handle.Dispose()}}}
    return [pscustomobject]$r
}
function Invoke-BuildInspect([string]$ConfigPath,[string]$BuildId){
    try{
        Import-ArtifactPackage;$config=Read-BuildConfig $ConfigPath;Assert-BuildId $BuildId
        $receipt=Read-BuildReceipt $config $BuildId
        return [pscustomobject]@{status='passed';reasonCode='complete';buildId=$BuildId;reference=(New-BuildReference $config $receipt.Data);packageSha256=$receipt.Data.packageSha256;receiptPath=$receipt.Path;publishedAt=$receipt.Data.publishedAt}
    }catch{return [pscustomobject]@{status='failed';reasonCode='receipt-unavailable'}}
}
function Invoke-BuildFetch([string]$ConfigPath,[string]$Reference,[string]$ExpectedHash){
    $r=[ordered]@{status='failed';reasonCode='unconfirmed';outputPath=$null;residue=[ordered]@{localPath=$null}};$timer=[Diagnostics.Stopwatch]::StartNew()
    try{
        Assert-Hex $ExpectedHash 64;Import-ArtifactPackage;$config=Read-BuildConfig $ConfigPath
        $ref=Read-BuildReference $Reference $config
        $op=New-ArtifactOperation;$r.residue.localPath=$op
        $generation=(Get-CredentialStatus 'osm').Generation;$archive=[IO.Path]::Combine($op,'download.zip')
        $download=Invoke-BuildNetwork $generation (New-ArtifactRequest $config 'get' $ref.key $null $archive $timer)
        if(-not (Test-ArtifactSuccess $download 'get' $generation) -or $download.Bytes -ne $ref.packageBytes -or $download.Sha256 -cne $ExpectedHash){throw 'download-unconfirmed'}
        $codec=[OneStarMaker.Artifacts.Packaging.BuildPackage]::new($ref.repositoryId,$ref.project,$ref.target,$ref.configuration,$ref.buildId)
        $package=$codec.Extract($archive,$op,$ExpectedHash,$ref.manifestSha256,(Get-Remaining $timer 600000))
        Protect-ArtifactTree $op
        $r.outputPath=$package.Path;$r.status='passed';$r.reasonCode='complete'
    }catch{if($script:FailureHook){& $script:FailureHook $_};if($_.Exception.Message -cin @('download-unconfirmed','invalid-reference')){$r.reasonCode=$_.Exception.Message}}
    return [pscustomobject]$r
}
Export-ModuleMember -Function Invoke-BuildPublish,Invoke-BuildFetch,Invoke-BuildInspect
