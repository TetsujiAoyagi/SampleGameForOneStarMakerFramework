Set-StrictMode -Version Latest

# Offline tests alone may replace the root inside this module. Public CLI exposes no such input.
$script:TestRoot = $null

Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1') -Force
Import-Module (Join-Path $PSScriptRoot '../Workflow/WindowsStorePaths.psm1')

function Get-ArtifactRoot {
    if (-not [OperatingSystem]::IsWindows()) { throw 'Artifact path unavailable.' }
    $binding=[AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1')
    if($binding){
        $context=$binding|ConvertFrom-Json -AsHashtable -Depth 10
        if($context.schemaVersion -ne 1 -or $context.ownerSid -cne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value){throw 'Artifact path unavailable.'}
        $role=$context.roles.transfers
        if(-not $role){throw 'Artifact path unavailable.'}
        $null=Assert-WindowsStorePathIdentity $role.physicalPath $role
        $root=$role.physicalPath;Assert-ArtifactAcl $root $true
    }else{
        $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
        if ([string]::IsNullOrEmpty($local)) { throw 'Artifact path unavailable.' }
        $root = if ($script:TestRoot) { [IO.Path]::GetFullPath($script:TestRoot) }
            else { [IO.Path]::Combine($local, 'OneStarMaker', 'Artifacts', 'transfers') }
        Assert-NoArtifactReparse $root
        if([IO.Directory]::Exists($root)){$root=(Get-WindowsStorePathIdentity $root).physicalPath}
    }
    $repo = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..'))
    if ((Test-ArtifactWithin $root $repo) -or (Test-ArtifactWithin $root 'D:\OneDrive\OSM-Artifacts') -or $root.StartsWith('\\')) { throw 'Artifact path unavailable.' }
    Assert-NoArtifactReparse $root
    return $root
}

# Only stored references under this role's exact logical or physical directory
# boundary may resolve. Never canonicalize an outside/source path into ownership.
function Resolve-ArtifactStoredPath([string]$Path,[switch]$AllowMissing){
    $root=Get-ArtifactRoot
    $binding=[AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1')
    $logical=if($binding){($binding|ConvertFrom-Json -AsHashtable).roles.transfers.logicalPath}elseif($script:TestRoot){[IO.Path]::GetFullPath($script:TestRoot)}else{[IO.Path]::Combine([Environment]::GetFolderPath('LocalApplicationData'),'OneStarMaker','Artifacts','transfers')}
    if(-not [IO.Path]::IsPathFullyQualified($Path) -or $Path.Contains('/') -or [IO.Path]::GetFullPath($Path) -cne $Path -or $Path -match '[\\:]\z'){throw 'Artifact path unavailable.'}
    $prefix=if($Path.StartsWith($root+'\',[StringComparison]::OrdinalIgnoreCase)){$root}elseif($Path.StartsWith($logical+'\',[StringComparison]::OrdinalIgnoreCase)){$logical}else{throw 'Artifact path unavailable.'}
    $suffix=$Path.Substring($prefix.Length+1)
    foreach($part in $suffix.Split('\')){if(-not $part -or $part -in @('.','..') -or $part.TrimEnd(' ','.') -cne $part -or $part.Contains(':')){throw 'Artifact path unavailable.'}}
    Assert-ArtifactAcl $root $true
    $resolved=[IO.Path]::Combine($root,$suffix);Assert-NoArtifactReparse $resolved
    $isDirectory=[IO.Directory]::Exists($resolved);$exists=$isDirectory -or [IO.File]::Exists($resolved)
    if(-not $exists){if(-not $AllowMissing){throw 'Artifact path unavailable.'};$parent=[IO.Path]::GetDirectoryName($resolved);while($parent -ine $root){if([IO.Directory]::Exists($parent)){Assert-ArtifactAcl $parent $true};$parent=[IO.Path]::GetDirectoryName($parent)};return $resolved}
    $identity=Get-WindowsStorePathIdentity $resolved
    if($identity.physicalPath -ine $resolved){throw 'Artifact path unavailable.'}
    if($Path -ine $resolved -and ([IO.File]::Exists($Path) -or [IO.Directory]::Exists($Path))){$null=Assert-WindowsStorePathIdentity $Path $identity}
    $current=$root
    foreach($part in $suffix.Split('\')){$current=[IO.Path]::Combine($current,$part);Assert-ArtifactAcl $current ([IO.Directory]::Exists($current))}
    return $resolved
}
function New-ArtifactOperation {
    $root = Get-ArtifactRoot
    if(-not [AppDomain]::CurrentDomain.GetData('OneStarMaker.Evidence.StorageBinding.v1')){
        $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
        $shared = if ($script:TestRoot) { [IO.Path]::GetDirectoryName($root) }
            else { [IO.Path]::Combine($local, 'OneStarMaker') }
        if (-not [IO.Directory]::Exists($shared)) {
            [IO.Directory]::CreateDirectory($shared) | Out-Null
            Set-ArtifactAcl $shared $true
        }
        if([IO.Directory]::Exists($root)){$shared=(Get-WindowsStorePathIdentity $shared).physicalPath}
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
    }
    $root=Get-ArtifactRoot;Assert-ArtifactAcl $root $true
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

Export-ModuleMember -Function Get-ArtifactRoot, Resolve-ArtifactStoredPath, New-ArtifactOperation, Assert-ArtifactOperationFile, Protect-ArtifactTree, Test-ArtifactWithin, Assert-NoArtifactReparse
