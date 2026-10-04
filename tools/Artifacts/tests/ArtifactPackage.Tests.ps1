param([string] $ResultPath = '')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$dll = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Packaging/artifacts/package/ArtifactPackaging.dll'))
if (-not [IO.File]::Exists($dll)) { throw 'Build ArtifactPackaging first.' }
$beforeHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash.ToLowerInvariant()
$assembly = [Reflection.Assembly]::LoadFrom($dll)
if (-not $assembly.Location.Equals($dll, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected package DLL.' }
$root = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'osm-package-test-' + [Guid]::NewGuid().ToString('N'))
$base = 'a' * 40; $head = 'b' * 40; $repository = 'c' * 64; $run = 'd' * 32
$cases = @('snapshot-roundtrip','repetitive-roundtrip','archive-stream-boundary','duplicate-case','traversal-reparse','known-secret-name','outer-hash','extra-entry','payload-hash')
$executed = [Collections.Generic.List[string]]::new()
$failed = [Collections.Generic.List[string]]::new()
function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Expect-Failure([scriptblock] $Action) {
    $rejected = $false
    try { & $Action | Out-Null } catch { $rejected = $true }
    Assert $rejected 'unsafe package was accepted'
}
function New-Fixture([string] $Name) {
    $path = [IO.Path]::Combine($root, $Name)
    [IO.Directory]::CreateDirectory($path) | Out-Null
    return $path
}
function Write-Input([string] $Path, [string] $Source, [string[]] $Files) {
    $value = [ordered]@{ schemaVersion = 1; purpose = 'synthetic'; root = $Source; files = $Files }
    [IO.File]::WriteAllText($Path, (ConvertTo-Json -InputObject $value -Compress), [Text.UTF8Encoding]::new($false))
}
try {
    foreach ($case in $cases) {
        try {
            $workspace = New-Fixture $case
            $source = New-Fixture "$case/source"
            $operation = New-Fixture "$case/operation"
            $input = [IO.Path]::Combine($workspace, 'input.json')
            switch ($case) {
                'snapshot-roundtrip' {
                    [IO.Directory]::CreateDirectory([IO.Path]::Combine($source,'dir')) | Out-Null
                    [IO.File]::WriteAllText([IO.Path]::Combine($source,'dir','α.txt'), 'synthetic-payload')
                    [IO.File]::WriteAllBytes([IO.Path]::Combine($source,'empty.bin'), [byte[]]::new(0))
                    Write-Input $input $source @('dir/α.txt','empty.bin')
                    $package = [OneStarMaker.Artifacts.Packaging.PackageIO]::Create($input,$operation,$base,$head,$repository,$run,60000)
                    [IO.File]::WriteAllText([IO.Path]::Combine($source,'dir','α.txt'), 'changed-after-snapshot')
                    $extract = New-Fixture "$case/extract"
                    $ready = [OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($package.Path,$extract,$package.Sha256,
                        $package.ManifestSha256,$base,$head,$repository,$run,60000)
                    Assert ($ready.Files.Count -eq 2 -and $ready.Path.EndsWith('ready')) 'roundtrip entries'
                    Assert ([IO.File]::ReadAllText([IO.Path]::Combine($ready.Path,'dir','α.txt')) -ceq 'synthetic-payload') 'snapshot was not isolated'
                    Assert ((Get-Item ([IO.Path]::Combine($ready.Path,'empty.bin'))).Length -eq 0) 'zero byte file'
                }
                'duplicate-case' {
                    [IO.File]::WriteAllText([IO.Path]::Combine($source,'a.txt'), 'a')
                    Write-Input $input $source @('a.txt','A.txt')
                    Expect-Failure { [OneStarMaker.Artifacts.Packaging.PackageIO]::Create($input,$operation,$base,$head,$repository,$run,60000) }
                }
                'repetitive-roundtrip' {
                    [IO.File]::WriteAllBytes([IO.Path]::Combine($source,'zeros.bin'), [byte[]]::new(4MB))
                    Write-Input $input $source @('zeros.bin')
                    $package = [OneStarMaker.Artifacts.Packaging.PackageIO]::Create($input,$operation,$base,$head,$repository,$run,60000)
                    $extract = New-Fixture "$case/extract"
                    $ready = [OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($package.Path,$extract,$package.Sha256,
                        $package.ManifestSha256,$base,$head,$repository,$run,60000)
                    Assert ((Get-Item ([IO.Path]::Combine($ready.Path,'zeros.bin'))).Length -eq 4MB) 'legitimate repetitive archive rejected'
                }
                'archive-stream-boundary' {
                    [IO.File]::WriteAllBytes([IO.Path]::Combine($source,'data.bin'), [byte[]]::new(64KB))
                    Write-Input $input $source @('data.bin')
                    $method=[OneStarMaker.Artifacts.Packaging.PackageIO].GetMethod('CreateBounded',[Reflection.BindingFlags]'NonPublic,Static')
                    Assert ($null -ne $method) 'bounded writer unavailable'
                    $normal=[OneStarMaker.Artifacts.Packaging.PackageIO]::Create($input,$operation,$base,$head,$repository,$run,60000)
                    foreach($cap in @([long]4096, [long]($normal.Bytes-1))) {
                        $bounded=New-Fixture "$case/bounded-$cap"
                        $failure=$null
                        try { $method.Invoke($null,[object[]]@($input,$bounded,$base,$head,$repository,$run,60000,$cap)) | Out-Null }
                        catch { $failure=if($_.Exception.InnerException){$_.Exception.InnerException.Message}else{$_.Exception.Message} }
                        Assert ($null -ne $failure) 'bounded package succeeded'
                        $partial=[IO.Path]::Combine($bounded,'bundle.zip')
                        $exists=[IO.File]::Exists($partial)
                        $actual=if($exists){(Get-Item -LiteralPath $partial).Length}else{-1}
                        Assert ($exists -and $actual -le $cap) "ZIP stream crossed injected limit: cap=$cap actual=$actual failure=$failure"
                    }
                }
                'traversal-reparse' {
                    Write-Input $input $source @('../outside.txt')
                    Expect-Failure { [OneStarMaker.Artifacts.Packaging.PackageIO]::Create($input,$operation,$base,$head,$repository,$run,60000) }
                    $link = [IO.Path]::Combine($source,'linked')
                    [IO.Directory]::CreateSymbolicLink($link, $root) | Out-Null
                    Write-Input $input $source @('linked/file.txt')
                    Expect-Failure { [OneStarMaker.Artifacts.Packaging.PackageIO]::Create($input,$operation,$base,$head,$repository,$run,60000) }
                }
                'known-secret-name' {
                    [IO.File]::WriteAllText([IO.Path]::Combine($source,'id_ed25519'), 'dummy-private-material')
                    Write-Input $input $source @('id_ed25519')
                    Expect-Failure { [OneStarMaker.Artifacts.Packaging.PackageIO]::Create($input,$operation,$base,$head,$repository,$run,60000) }
                }
                default {
                    [IO.File]::WriteAllText([IO.Path]::Combine($source,'data.txt'), 'synthetic-data')
                    Write-Input $input $source @('data.txt')
                    $package = [OneStarMaker.Artifacts.Packaging.PackageIO]::Create($input,$operation,$base,$head,$repository,$run,60000)
                    $extract = New-Fixture "$case/extract"
                    if ($case -eq 'outer-hash') {
                        Expect-Failure { [OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($package.Path,$extract,('0' * 64),$package.ManifestSha256,$base,$head,$repository,$run,60000) }
                        Assert (-not [IO.Directory]::Exists([IO.Path]::Combine($extract,'ready'))) 'outer mismatch produced ready'
                    } elseif ($case -eq 'extra-entry') {
                        $copy = [IO.Path]::Combine($operation,'extra.zip')
                        [IO.File]::Copy($package.Path,$copy)
                        $stream = [IO.FileStream]::new($copy,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite)
                        $zip = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Update)
                        try { $entry=$zip.CreateEntry('unexpected.txt'); $writer=[IO.StreamWriter]::new($entry.Open()); try { $writer.Write('extra') } finally { $writer.Dispose() } }
                        finally { $zip.Dispose(); $stream.Dispose() }
                        $hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $copy).Hash.ToLowerInvariant()
                        Expect-Failure { [OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($copy,$extract,$hash,$package.ManifestSha256,$base,$head,$repository,$run,60000) }
                    } else {
                        $copy = [IO.Path]::Combine($operation,'changed.zip')
                        [IO.File]::Copy($package.Path,$copy)
                        $stream = [IO.FileStream]::new($copy,[IO.FileMode]::Open,[IO.FileAccess]::ReadWrite)
                        $zip = [IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Update)
                        try {
                            $zip.GetEntry('payload/data.txt').Delete()
                            $entry=$zip.CreateEntry('payload/data.txt'); $writer=[IO.StreamWriter]::new($entry.Open())
                            try { $writer.Write('changed-by-attacker') } finally { $writer.Dispose() }
                        } finally { $zip.Dispose(); $stream.Dispose() }
                        $hash=(Get-FileHash -Algorithm SHA256 -LiteralPath $copy).Hash.ToLowerInvariant()
                        Expect-Failure { [OneStarMaker.Artifacts.Packaging.PackageIO]::Extract($copy,$extract,$hash,$package.ManifestSha256,$base,$head,$repository,$run,60000) }
                    }
                    Assert (-not [IO.Directory]::Exists([IO.Path]::Combine($extract,'ready'))) 'failed extract produced ready'
                }
            }
            $executed.Add($case)
        } catch { $failed.Add($case + ': ' + $_.Exception.GetType().Name + ': ' + $_.Exception.Message) }
    }
    $afterHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash.ToLowerInvariant()
    if ($beforeHash -cne $afterHash) { throw 'Package DLL changed during tests.' }
    if ($ResultPath) {
        $result = [ordered]@{ registered = $cases; selected = $cases; executed = @($executed); failed = @($failed)
            loadedPath = $dll; loadedHashBefore = $beforeHash; loadedHashAfter = $afterHash
            moduleVersionId = $assembly.ManifestModule.ModuleVersionId.ToString() }
        [IO.File]::WriteAllText($ResultPath,(ConvertTo-Json -InputObject $result -Depth 6),[Text.UTF8Encoding]::new($false))
    }
    foreach ($item in $executed) { "PASS $item" }
    foreach ($item in $failed) { "FAIL $item" }
    "Cases: $($cases.Count); passed: $($executed.Count); failed: $($failed.Count)"
    if ($failed.Count -gt 0 -or $executed.Count -ne $cases.Count) { exit 1 }
} finally { if ([IO.Directory]::Exists($root)) { [IO.Directory]::Delete($root,$true) } }
