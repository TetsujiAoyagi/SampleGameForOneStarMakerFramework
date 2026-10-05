Set-StrictMode -Version Latest

# Offline tests alone may replace the root inside this module. Public CLI exposes no such input.
$script:TestRoot = $null

function Assert-NoArtifactReparse([string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    $current = [IO.Path]::GetPathRoot($full)
    foreach ($part in $full.Substring($current.Length).Split('\', [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = [IO.Path]::Combine($current, $part)
        if ([IO.File]::Exists($current) -or [IO.Directory]::Exists($current)) {
            if (([IO.File]::GetAttributes($current) -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Artifact path unavailable.'
            }
        }
    }
}

function Test-ArtifactWithin([string] $Path, [string] $Root) {
    $pathFull = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\')
    return $pathFull.Equals($rootFull, [StringComparison]::OrdinalIgnoreCase) -or
        $pathFull.StartsWith($rootFull + '\', [StringComparison]::OrdinalIgnoreCase)
}

function New-ArtifactAcl([bool] $IsDirectory) {
    $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = if ($IsDirectory) { [Security.AccessControl.DirectorySecurity]::new() } else { [Security.AccessControl.FileSecurity]::new() }
    $acl.SetOwner($owner)
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($identity in @($owner, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
        $inherit = if ($IsDirectory) { [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit } else { [Security.AccessControl.InheritanceFlags]::None }
        $acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($identity,
                [Security.AccessControl.FileSystemRights]::FullControl, $inherit,
                [Security.AccessControl.PropagationFlags]::None,
                [Security.AccessControl.AccessControlType]::Allow))
    }
    return $acl
}

function Assert-ArtifactAcl([string] $Path, [bool] $IsDirectory) {
    Assert-NoArtifactReparse $Path
    $acl = if ($IsDirectory) { [IO.FileSystemAclExtensions]::GetAccessControl([IO.DirectoryInfo]::new($Path)) }
        else { [IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($Path)) }
    if (-not $acl.AreAccessRulesProtected) { throw 'Artifact path unavailable.' }
    $owner = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    if ($acl.GetOwner([Security.Principal.SecurityIdentifier]).Value -cne $owner) { throw 'Artifact path unavailable.' }
    $allowed = @($owner, 'S-1-5-18', 'S-1-5-32-544')
    $seen = @{}
    foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        if ($rule.IsInherited -or $rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or
            $rule.IdentityReference.Value -notin $allowed -or
            (($rule.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) -ne [Security.AccessControl.FileSystemRights]::FullControl)) {
            throw 'Artifact path unavailable.'
        }
        $seen[$rule.IdentityReference.Value] = $true
    }
    foreach ($identity in $allowed) { if (-not $seen.ContainsKey($identity)) { throw 'Artifact path unavailable.' } }
}

function Set-ArtifactAcl([string] $Path, [bool] $IsDirectory) {
    $acl = New-ArtifactAcl $IsDirectory
    if ($IsDirectory) { [IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($Path), $acl) }
    else { [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($Path), $acl) }
    Assert-ArtifactAcl $Path $IsDirectory
}

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
