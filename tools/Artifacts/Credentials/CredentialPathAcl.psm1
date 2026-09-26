Set-StrictMode -Version Latest
# テストだけが注入する書き込み台帳。CLI や環境変数からは設定できない。
$script:WriteLedger = $null

function Assert-CredentialWrite([string] $Root, [string] $Path) {
    $full = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Within $full $Root)) { throw 'Credential operation unavailable.' }
    if ($null -ne $script:WriteLedger) { $script:WriteLedger.Add($full) }
}

function Assert-Windows {
    if (-not [OperatingSystem]::IsWindows()) { throw 'Credential operation unavailable.' }
}

function Assert-Profile([string] $Profile) {
    if ($Profile -cne 'osm' -or $Profile -cnotmatch '^[a-z][a-z0-9_-]{0,31}$') {
        throw 'Credential operation unavailable.'
    }
}

function Test-Within([string] $Path, [string] $Parent) {
    $p = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    $q = [IO.Path]::GetFullPath($Parent).TrimEnd('\')
    return $p.Equals($q, [StringComparison]::OrdinalIgnoreCase) -or
        $p.StartsWith($q + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparse([string] $Path) {
    # 最終ディレクトリだけでなく、ドライブ直下からの全祖先を確認する。
    # 見落とした junction 経由で秘密ファイルが別の保存先へ逸脱するのを防ぐ。
    $full = [IO.Path]::GetFullPath($Path)
    $item = [IO.Path]::GetPathRoot($full)
    if (-not $item) { throw 'Credential operation unavailable.' }
    foreach ($part in $full.Substring($item.Length).Split('\', [StringSplitOptions]::RemoveEmptyEntries)) {
        $item = [IO.Path]::Combine($item, $part)
        try {
            $attributes = [IO.File]::GetAttributes($item)
            if (($attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw 'Credential operation unavailable.'
            }
        } catch [IO.FileNotFoundException] {
        } catch [IO.DirectoryNotFoundException] {
        }
    }
}

function New-OwnerAcl([bool] $Directory) {
    # 新規の資格情報専用領域は継承を切り、現在ユーザー・SYSTEM・管理者に限定する。
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $acl = if ($Directory) { [Security.AccessControl.DirectorySecurity]::new() } else { [Security.AccessControl.FileSecurity]::new() }
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($identity in @($sid, [Security.Principal.SecurityIdentifier]::new('S-1-5-18'), [Security.Principal.SecurityIdentifier]::new('S-1-5-32-544'))) {
        $rights = [Security.AccessControl.FileSystemRights]::FullControl
        $inheritance = if ($Directory) { [Security.AccessControl.InheritanceFlags]::ContainerInherit -bor [Security.AccessControl.InheritanceFlags]::ObjectInherit } else { [Security.AccessControl.InheritanceFlags]::None }
        $rule = [Security.AccessControl.FileSystemAccessRule]::new($identity, $rights, $inheritance, [Security.AccessControl.PropagationFlags]::None, [Security.AccessControl.AccessControlType]::Allow)
        $acl.AddAccessRule($rule)
    }
    return $acl
}

function Assert-OwnerAcl([string] $Path, [bool] $Directory) {
    # 既存の資格情報専用領域を勝手に「修復」しない。意図外の ACE は拒否する。
    Assert-NoReparse $Path
    $acl = if ($Directory) { [IO.FileSystemAclExtensions]::GetAccessControl([IO.DirectoryInfo]::new($Path)) }
        else { [IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($Path)) }
    if (-not $acl.AreAccessRulesProtected) { throw 'Credential operation unavailable.' }
    $allowed = @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18', 'S-1-5-32-544')
    $seen = @{}
    foreach ($rule in $acl.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier])) {
        if ($rule.IsInherited -or $rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow -or
            $rule.IdentityReference.Value -notin $allowed -or
            (($rule.FileSystemRights -band [Security.AccessControl.FileSystemRights]::FullControl) -ne [Security.AccessControl.FileSystemRights]::FullControl)) {
            throw 'Credential operation unavailable.'
        }
        $seen[$rule.IdentityReference.Value] = $true
    }
    foreach ($identity in $allowed) { if (-not $seen.ContainsKey($identity)) { throw 'Credential operation unavailable.' } }
    $owner = $acl.GetOwner([Security.Principal.SecurityIdentifier]).Value
    if ($owner -ne [Security.Principal.WindowsIdentity]::GetCurrent().User.Value) { throw 'Credential operation unavailable.' }
}

function Assert-SharedParent([string] $Path) {
    # OneStarMaker は既存の別機能と共有される。ここでは reparse のみ確認し、
    # 既存 ACL や兄弟ディレクトリを変更しない。専用 ACL 境界は Artifacts 以下。
    Assert-NoReparse $Path
    if (-not [IO.Directory]::Exists($Path)) { throw 'Credential operation unavailable.' }
}

function Assert-CredentialDirectoryInitialization([string] $Path, [string[]] $AllowedDirectories) {
    # 初回だけ最終 root の親も作るため、固定の三段に限って書き込みを許可する。
    # ファイルの書き込み境界を広げるための例外ではない。
    $full = [IO.Path]::GetFullPath($Path)
    if (-not @($AllowedDirectories | Where-Object { $full.Equals($_, [StringComparison]::OrdinalIgnoreCase) }).Count) {
        throw 'Credential operation unavailable.'
    }
    if ($null -ne $script:WriteLedger) { $script:WriteLedger.Add($full) }
}

function Set-OwnerAcl([string] $Path, [bool] $Directory, [string] $Root, [string[]] $AllowedDirectories) {
    $acl = New-OwnerAcl $Directory
    if ($Directory) { Assert-CredentialDirectoryInitialization $Path $AllowedDirectories }
    else { Assert-CredentialWrite $Root $Path }
    if ($Directory) { [IO.FileSystemAclExtensions]::SetAccessControl([IO.DirectoryInfo]::new($Path), $acl) }
    else { [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($Path), $acl) }
    Assert-OwnerAcl $Path $Directory
}

function Get-CredentialRoot([string] $TestRoot) {
    Assert-Windows
    if ($TestRoot) {
        $root = [IO.Path]::GetFullPath($TestRoot)
    } else {
        # 環境変数 LOCALAPPDATA の置換値を信用せず Known Folder から導く。
        $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
        if (-not $local) { throw 'Credential operation unavailable.' }
        $root = [IO.Path]::Combine($local, 'OneStarMaker', 'Artifacts', 'credentials')
    }
    $root = [IO.Path]::GetFullPath($root)
    $repo = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..', '..'))
    if ((Test-Within $root $repo) -or (Test-Within $root 'D:\OneDrive\OSM-Artifacts')) { throw 'Credential operation unavailable.' }
    Assert-NoReparse $root
    return $root
}

function Initialize-CredentialRoot([string] $Root, [bool] $IsTestRoot, [string] $TestBase) {
    Assert-NoReparse $Root
    $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $base = if ($IsTestRoot) {
        if ($TestBase) { [IO.Path]::GetFullPath($TestBase) } else { [IO.Path]::GetDirectoryName($Root) }
    } else { $local }
    [string[]]$allowed = if ($IsTestRoot -and -not $TestBase) { @($Root) }
        else {
            @(
                [IO.Path]::Combine($base, 'OneStarMaker'),
                [IO.Path]::Combine($base, 'OneStarMaker', 'Artifacts'),
                [IO.Path]::Combine($base, 'OneStarMaker', 'Artifacts', 'credentials')
            )
        }
    if (-not $Root.Equals($allowed[-1], [StringComparison]::OrdinalIgnoreCase)) { throw 'Credential operation unavailable.' }
    $current = $base
    $relative = [IO.Path]::GetRelativePath($base, $Root)
    foreach ($part in $relative.Split('\', [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = [IO.Path]::Combine($current, $part)
        Assert-NoReparse $current
        if (-not [IO.Directory]::Exists($current)) {
            Assert-CredentialDirectoryInitialization $current $allowed
            [IO.Directory]::CreateDirectory($current) | Out-Null
            Set-OwnerAcl $current $true $Root $allowed
        } elseif (-not $IsTestRoot -and $current.Equals($allowed[0], [StringComparison]::OrdinalIgnoreCase)) {
            Assert-SharedParent $current
        } elseif ($TestBase -and $current.Equals($allowed[0], [StringComparison]::OrdinalIgnoreCase)) {
            Assert-SharedParent $current
        } else { Assert-OwnerAcl $current $true }
    }
}

function Assert-CredentialFile([string] $Path) {
    if ([IO.File]::Exists($Path)) { Assert-OwnerAcl $Path $false }
    elseif ([IO.Directory]::Exists($Path)) { throw 'Credential operation unavailable.' }
}

function Set-CredentialFileAcl([string] $Path, [string] $Root) { Set-OwnerAcl $Path $false $Root @() }

Export-ModuleMember -Function Assert-Profile, Get-CredentialRoot, Initialize-CredentialRoot, Assert-CredentialFile, Set-CredentialFileAcl, Assert-CredentialWrite
