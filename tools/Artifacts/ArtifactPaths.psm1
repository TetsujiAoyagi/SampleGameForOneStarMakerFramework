Set-StrictMode -Version Latest

# Offline tests alone may replace the root inside this module. Public CLI exposes no such input.
$script:TestRoot = $null

Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1') -Force

function Get-ArtifactRoot {
    if (-not [OperatingSystem]::IsWindows()) { throw 'Artifact path unavailable.' }
    $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    if ([string]::IsNullOrEmpty($local)) { throw 'Artifact path unavailable.' }
    $root = if ($script:TestRoot) { [IO.Path]::GetFullPath($script:TestRoot) }
        else { [IO.Path]::Combine($local, 'OneStarMaker', 'Artifacts', 'transfers') }
    $repo = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..'))
    if ((Test-ArtifactWithin $root $repo) -or (Test-ArtifactWithin $root 'D:\OneDrive\OSM-Artifacts')) {
        throw 'Artifact path unavailable.'
    }
    Assert-NoArtifactReparse $root
    return $root
}

function New-ArtifactOperation {
    $root = Get-ArtifactRoot
    $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $shared = if ($script:TestRoot) { [IO.Path]::GetDirectoryName($root) }
        else { [IO.Path]::Combine($local, 'OneStarMaker') }
    if (-not [IO.Directory]::Exists($shared)) {
        [IO.Directory]::CreateDirectory($shared) | Out-Null
        Set-ArtifactAcl $shared $true
    }
    Assert-NoArtifactReparse $shared
    # The shared OneStarMaker parent may serve other features; its existing ACL is left untouched.
    $current = $shared
    foreach ($part in [IO.Path]::GetRelativePath($shared, $root).Split('\', [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = [IO.Path]::Combine($current, $part)
        Assert-NoArtifactReparse $current
        if (-not [IO.Directory]::Exists($current)) {
            [IO.Directory]::CreateDirectory($current) | Out-Null
            Set-ArtifactAcl $current $true
        } else { Assert-ArtifactAcl $current $true }
    }
    $operation = [IO.Path]::Combine($root, [Guid]::NewGuid().ToString('N'))
    if ([IO.Directory]::Exists($operation) -or [IO.File]::Exists($operation)) { throw 'Artifact operation unavailable.' }
    [IO.Directory]::CreateDirectory($operation) | Out-Null
    Set-ArtifactAcl $operation $true
    # A CreateNew marker makes accidental reuse of an operation directory fail before content is written.
    $marker = [IO.Path]::Combine($operation, 'operation.lock')
    $stream = [IO.FileStream]::new($marker, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Flush($true) } finally { $stream.Dispose() }
    Set-ArtifactAcl $marker $false
    return $operation
}

function Assert-ArtifactOperationFile([string] $OperationRoot, [string] $Path) {
    $root = Get-ArtifactRoot
    if (-not (Test-ArtifactWithin $OperationRoot $root) -or -not (Test-ArtifactWithin $Path $OperationRoot) -or
        $OperationRoot -cnotmatch '[\\/][0-9a-f]{32}$') { throw 'Artifact path unavailable.' }
    Assert-ArtifactAcl $root $true
    Assert-ArtifactAcl $OperationRoot $true
    Assert-NoArtifactReparse $Path
    if ([IO.File]::Exists($Path)) { Assert-ArtifactAcl $Path $false }
}

function Protect-ArtifactTree([string] $OperationRoot) {
    Assert-ArtifactAcl $OperationRoot $true
    foreach ($dir in [IO.Directory]::EnumerateDirectories($OperationRoot, '*', [IO.SearchOption]::AllDirectories)) {
        Assert-NoArtifactReparse $dir
        Set-ArtifactAcl $dir $true
    }
    foreach ($file in [IO.Directory]::EnumerateFiles($OperationRoot, '*', [IO.SearchOption]::AllDirectories)) {
        Assert-NoArtifactReparse $file
        Set-ArtifactAcl $file $false
    }
}

Export-ModuleMember -Function Get-ArtifactRoot, New-ArtifactOperation, Assert-ArtifactOperationFile, Protect-ArtifactTree, Test-ArtifactWithin, Assert-NoArtifactReparse
