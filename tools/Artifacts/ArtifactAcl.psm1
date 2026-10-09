Set-StrictMode -Version Latest

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

Export-ModuleMember -Function Assert-NoArtifactReparse, Test-ArtifactWithin, Assert-ArtifactAcl, Set-ArtifactAcl
