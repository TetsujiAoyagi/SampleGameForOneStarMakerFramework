Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'CredentialPathAcl.psm1') -Force

# These values are set only by a test script running inside this module's private scope.
$script:TestRoot = $null
$script:Fault = $null
$script:Clock = $null
$script:FakeValidator = $null

function Get-Paths([string] $Profile, [bool] $Create) {
    Assert-Profile $Profile
    $root = Get-CredentialRoot $script:TestRoot
    if ($Create) { Initialize-CredentialRoot $root ([bool]$script:TestRoot) }
    elseif ([IO.Directory]::Exists($root)) { Initialize-CredentialRoot $root ([bool]$script:TestRoot) }
    return @{ Root = $root; Active = [IO.Path]::Combine($root, "$Profile.active"); Lock = [IO.Path]::Combine($root, "$Profile.lock") }
}

function Invoke-Fault([string] $Point) {
    if ($script:Fault) { & $script:Fault $Point }
}

function Get-Now {
    if ($script:Clock) { return & $script:Clock }
    return [DateTimeOffset]::UtcNow
}

function Enter-StoreLock([hashtable] $Paths) {
    $deadline = [Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        try {
            Assert-CredentialFile $Paths.Lock
            $stream = [IO.FileStream]::new($Paths.Lock, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            Set-CredentialFileAcl $Paths.Lock
            return $stream
        } catch [IO.IOException] {
            if ($deadline.Elapsed -ge [TimeSpan]::FromSeconds(5)) { throw 'Credential operation unavailable.' }
            [Threading.Thread]::Yield() | Out-Null
        }
    }
}

function Assert-Record($Record, [string] $Profile) {
    if ($null -eq $Record -or $Record.Version -cne 1 -or $Record.Profile -cne $Profile -or
        $Record.Bucket -cne 'osm-artifacts' -or $null -ne $Record.Endpoint -or
        $Record.Generation -cnotmatch '^[0-9a-f]{32}$' -or
        $Record.CreatedUtc -cnotmatch '^\d{4}-\d\d-\d\dT' -or
        $Record.UpdatedUtc -cnotmatch '^\d{4}-\d\d-\d\dT' -or
        $null -ne $Record.TokenReference -or
        $Record.AccessKeyId -cnotmatch '^[\x21-\x7e]{1,256}$' -or
        $Record.SecretAccessKey -cnotmatch '^[\x21-\x7e]{1,256}$') { throw 'Credential operation unavailable.' }
    $names = @($Record.PSObject.Properties.Name | Sort-Object)
    $expected = @('AccessKeyId','Bucket','CreatedUtc','Endpoint','Generation','Profile','SecretAccessKey','TokenReference','UpdatedUtc','Version') | Sort-Object
    if (($names -join ',') -cne ($expected -join ',')) { throw 'Credential operation unavailable.' }
}

function Protect-Record($Record) {
    $plain = $null
    try {
        $plain = [Text.Encoding]::UTF8.GetBytes(($Record | ConvertTo-Json -Compress -Depth 3))
        return [Security.Cryptography.ProtectedData]::Protect($plain, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    } finally { if ($plain) { [Array]::Clear($plain, 0, $plain.Length) } }
}

function Unprotect-Record([byte[]] $Cipher, [string] $Profile) {
    $plain = $null
    try {
        $plain = [Security.Cryptography.ProtectedData]::Unprotect($Cipher, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $record = [Text.Encoding]::UTF8.GetString($plain) | ConvertFrom-Json -Depth 3 -DateKind String
        Assert-Record $record $Profile
        return $record
    } catch { throw 'Credential operation unavailable.' }
    finally { if ($plain) { [Array]::Clear($plain, 0, $plain.Length) } }
}

function Read-Active([hashtable] $Paths, [string] $Profile) {
    Assert-CredentialFile $Paths.Active
    if (-not [IO.File]::Exists($Paths.Active)) { throw 'Credential operation unavailable.' }
    $cipher = [IO.File]::ReadAllBytes($Paths.Active)
    try { return Unprotect-Record $cipher $Profile }
    finally { [Array]::Clear($cipher, 0, $cipher.Length) }
}

function Get-OwnedFiles([hashtable] $Paths, [string] $Profile) {
    if (-not [IO.Directory]::Exists($Paths.Root)) { return @() }
    $pattern = '^' + [regex]::Escape($Profile) + '\.(candidate|backup)\.[0-9a-f]{32}$'
    return @([IO.Directory]::EnumerateFiles($Paths.Root) | Where-Object { [IO.Path]::GetFileName($_) -cmatch $pattern })
}

function Remove-OwnedFiles([hashtable] $Paths, [string] $Profile) {
    foreach ($file in Get-OwnedFiles $Paths $Profile) {
        Assert-CredentialFile $file
        [IO.File]::Delete($file)
    }
}

function Write-CredentialRecord([string] $Profile, [string] $AccessKeyId, [string] $SecretAccessKey,
    [bool] $Replace) {
    $paths = Get-Paths $Profile $true
    $lock = Enter-StoreLock $paths
    $candidate = $null
    $committed = $false
    try {
        Assert-CredentialFile $paths.Active
        $exists = [IO.File]::Exists($paths.Active)
        if (-not $exists -and @(Get-OwnedFiles $paths $Profile | Where-Object { [IO.Path]::GetFileName($_) -cmatch '\.backup\.' }).Count -gt 0) {
            throw 'Credential operation unavailable.'
        }
        if ($exists -and -not $Replace) { throw 'Credential operation unavailable.' }
        if ($Replace -and -not $exists) { throw 'Credential operation unavailable.' }
        $old = if ($exists) { Read-Active $paths $Profile } else { $null }
        if ($AccessKeyId -cnotmatch '^[\x21-\x7e]{1,256}$' -or $SecretAccessKey -cnotmatch '^[\x21-\x7e]{1,256}$') {
            throw 'Credential operation unavailable.'
        }
        if ($script:FakeValidator) { & $script:FakeValidator $AccessKeyId $SecretAccessKey }
        Invoke-Fault 'before-encrypt'
        $now = (Get-Now).ToUniversalTime().ToString('o')
        $record = [pscustomobject]@{
            Version = 1; Profile = $Profile; Bucket = 'osm-artifacts'; Endpoint = $null
            Generation = [Guid]::NewGuid().ToString('N'); CreatedUtc = if ($old) { $old.CreatedUtc } else { $now }
            UpdatedUtc = $now; TokenReference = $null; AccessKeyId = $AccessKeyId; SecretAccessKey = $SecretAccessKey
        }
        Assert-Record $record $Profile
        $cipher = Protect-Record $record
        $candidate = [IO.Path]::Combine($paths.Root, "$Profile.candidate.$([Guid]::NewGuid().ToString('N'))")
        try {
            $stream = [IO.FileStream]::new($candidate, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try { $stream.Write($cipher, 0, $cipher.Length); $stream.Flush($true) } finally { $stream.Dispose() }
            Set-CredentialFileAcl $candidate
            $verified = Unprotect-Record ([IO.File]::ReadAllBytes($candidate)) $Profile
            if ($verified.Generation -cne $record.Generation) { throw 'Credential operation unavailable.' }
            Invoke-Fault 'before-commit'
            if ($exists) {
                $backup = [IO.Path]::Combine($paths.Root, "$Profile.backup.$([Guid]::NewGuid().ToString('N'))")
                [IO.File]::Replace($candidate, $paths.Active, $backup)
            } else {
                # A missing active file is never recovered from a leftover backup.
                [IO.File]::Move($candidate, $paths.Active, $false)
            }
            $committed = $true
            try {
                Invoke-Fault 'after-commit'
                Remove-OwnedFiles $paths $Profile
                return @{ Outcome = 'success' }
            } catch { return @{ Outcome = 'committed with cleanup pending' } }
        } finally { [Array]::Clear($cipher, 0, $cipher.Length) }
    } finally {
        if (-not $committed -and $candidate -and [IO.File]::Exists($candidate)) {
            try { Assert-CredentialFile $candidate; [IO.File]::Delete($candidate) } catch { }
        }
        $lock.Dispose()
    }
}

function Get-CredentialStatus([string] $Profile) {
    $paths = Get-Paths $Profile $false
    if (-not [IO.Directory]::Exists($paths.Root)) { throw 'Credential operation unavailable.' }
    $record = Read-Active $paths $Profile
    return [pscustomobject]@{
        Profile = $record.Profile; Bucket = $record.Bucket; Endpoint = $record.Endpoint
        Generation = $record.Generation; CreatedUtc = $record.CreatedUtc; UpdatedUtc = $record.UpdatedUtc
        TokenReference = $record.TokenReference
    }
}

function Remove-CredentialRecord([string] $Profile) {
    $paths = Get-Paths $Profile $false
    if (-not [IO.Directory]::Exists($paths.Root)) { return 'already absent' }
    $lock = Enter-StoreLock $paths
    try {
        Assert-CredentialFile $paths.Active
        $present = [IO.File]::Exists($paths.Active)
        if ($present) { [IO.File]::Delete($paths.Active) }
        Remove-OwnedFiles $paths $Profile
        return $(if ($present) { 'removed' } else { 'already absent' })
    } finally { $lock.Dispose() }
}

Export-ModuleMember -Function Write-CredentialRecord, Get-CredentialStatus, Remove-CredentialRecord
