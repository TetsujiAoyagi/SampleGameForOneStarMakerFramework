Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'CredentialPathAcl.psm1') -Force

# テストだけが module 内部へ注入する経路。export せず CLI・環境変数から到達させない。
$script:TestRoot = $null
$script:TestBase = $null
$script:Fault = $null
$script:Clock = $null
$script:FakeValidator = $null

function Get-Paths([string] $Profile, [bool] $Create) {
    Assert-Profile $Profile
    $root = Get-CredentialRoot $script:TestRoot
    if ($Create) { Initialize-CredentialRoot $root ([bool]$script:TestRoot) $script:TestBase }
    elseif ([IO.Directory]::Exists($root)) { Initialize-CredentialRoot $root ([bool]$script:TestRoot) $script:TestBase }
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
    # ローカルの排他ロックで set/remove を直列化する。5 秒以内に取れなければ
    # active の変更前に失敗し、待機を無期限にしない。
    $deadline = [Diagnostics.Stopwatch]::StartNew()
    while ($true) {
        try {
            Assert-CredentialFile $Paths.Lock
            Assert-CredentialWrite $Paths.Root $Paths.Lock
            $stream = [IO.FileStream]::new($Paths.Lock, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            Set-CredentialFileAcl $Paths.Lock $Paths.Root
            return $stream
        } catch [IO.IOException] {
            if ($deadline.Elapsed -ge [TimeSpan]::FromSeconds(5)) { throw 'Credential operation unavailable.' }
            [Threading.Thread]::Yield() | Out-Null
        }
    }
}

function Assert-Record($Record, [string] $Profile) {
    # PowerShell の -match は配列に対して一致要素の配列を返す。
    # そのまま否定比較すると不正な配列を通し得るため、値より先に型を検証する。
    if ($null -eq $Record -or $Record.GetType() -ne [System.Management.Automation.PSCustomObject]) { throw 'Credential operation unavailable.' }
    $names = @($Record.PSObject.Properties.Name | Sort-Object)
    $expected = @('AccessKeyId','Bucket','CreatedUtc','Endpoint','Generation','Profile','SecretAccessKey','TokenReference','UpdatedUtc','Version') | Sort-Object
    if (($names -join ',') -cne ($expected -join ',')) { throw 'Credential operation unavailable.' }
    if (($Record.Version -isnot [int] -and $Record.Version -isnot [long]) -or $Record.Version -ne 1 -or
        $Record.Profile -isnot [string] -or $Record.Profile -cne $Profile -or
        $Record.Bucket -isnot [string] -or $Record.Bucket -cne 'osm-artifacts' -or
        $null -ne $Record.Endpoint -or $null -ne $Record.TokenReference -or
        $Record.Generation -isnot [string] -or $Record.Generation -cnotmatch '^[0-9a-f]{32}$' -or
        $Record.CreatedUtc -isnot [string] -or $Record.UpdatedUtc -isnot [string] -or
        $Record.AccessKeyId -isnot [string] -or $Record.AccessKeyId -cnotmatch '^[\x21-\x7e]{1,256}$' -or
        $Record.SecretAccessKey -isnot [string] -or $Record.SecretAccessKey -cnotmatch '^[\x21-\x7e]{1,256}$') {
        throw 'Credential operation unavailable.'
    }
    # 接頭辞だけでは「2026-99-99TINVALID」も通る。完全な round-trip 書式と
    # 実在する UTC 日時であることを確認し、更新時刻の逆行も拒否する。
    [DateTimeOffset]$created = [DateTimeOffset]::MinValue
    [DateTimeOffset]$updated = [DateTimeOffset]::MinValue
    $culture = [Globalization.CultureInfo]::InvariantCulture
    $style = [Globalization.DateTimeStyles]::None
    if (-not [DateTimeOffset]::TryParseExact($Record.CreatedUtc, 'o', $culture, $style, [ref]$created) -or
        -not [DateTimeOffset]::TryParseExact($Record.UpdatedUtc, 'o', $culture, $style, [ref]$updated) -or
        $created.Offset -ne [TimeSpan]::Zero -or $updated.Offset -ne [TimeSpan]::Zero -or $updated -lt $created) {
        throw 'Credential operation unavailable.'
    }
}

function Assert-JsonSchema([string] $Json) {
    # ConvertFrom-Json は重複キーを上書きするため、変換前の JSON で
    # キーの重複・余分なキー・配列などの型違いを拒否する。
    $document = [Text.Json.JsonDocument]::Parse($Json)
    try {
        if ($document.RootElement.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw 'Credential operation unavailable.' }
        $strings = @('Profile','Bucket','Generation','CreatedUtc','UpdatedUtc','AccessKeyId','SecretAccessKey')
        $nulls = @('Endpoint','TokenReference')
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($property in $document.RootElement.EnumerateObject()) {
            if (-not $seen.Add($property.Name)) { throw 'Credential operation unavailable.' }
            if ($property.Name -cin $strings) {
                if ($property.Value.ValueKind -ne [Text.Json.JsonValueKind]::String) { throw 'Credential operation unavailable.' }
            } elseif ($property.Name -cin $nulls) {
                if ($property.Value.ValueKind -ne [Text.Json.JsonValueKind]::Null) { throw 'Credential operation unavailable.' }
            } elseif ($property.Name -ceq 'Version') {
                if ($property.Value.ValueKind -ne [Text.Json.JsonValueKind]::Number -or $property.Value.GetInt32() -ne 1) {
                    throw 'Credential operation unavailable.'
                }
            } else { throw 'Credential operation unavailable.' }
        }
        if ($seen.Count -ne 10) { throw 'Credential operation unavailable.' }
    } finally { $document.Dispose() }
}

function Protect-Record($Record) {
    # 平文 byte 配列はこの呼び出し内で消去する。PowerShell の文字列を
    # 完全消去できるとは主張しない。DPAPI は CurrentUser のみを使う。
    $plain = $null
    try {
        $plain = [Text.Encoding]::UTF8.GetBytes(($Record | ConvertTo-Json -Compress -Depth 3))
        return [Security.Cryptography.ProtectedData]::Protect($plain, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    } finally { if ($plain) { [Array]::Clear($plain, 0, $plain.Length) } }
}

function Unprotect-Record([byte[]] $Cipher, [string] $Profile) {
    # 復号できること自体は健全性の証明ではない。全フィールドを検証し、
    # 呼び出し元には秘密を含む例外詳細を返さない。
    $plain = $null
    try {
        $plain = [Security.Cryptography.ProtectedData]::Unprotect($Cipher, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $json = [Text.Encoding]::UTF8.GetString($plain)
        Assert-JsonSchema $json
        $record = $json | ConvertFrom-Json -Depth 3 -DateKind String
        Assert-Record $record $Profile
        return $record
    } catch { throw 'Credential operation unavailable.' }
    finally { if ($plain) { [Array]::Clear($plain, 0, $plain.Length) } }
}

function Read-Active([hashtable] $Paths, [string] $Profile) {
    # backup/candidate は障害復旧用の残骸であり、active に昇格させない。
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
        Assert-CredentialWrite $Paths.Root $file
        [IO.File]::Delete($file)
    }
}

function Write-CredentialRecord([string] $Profile, [string] $AccessKeyId, [string] $SecretAccessKey,
    [bool] $Replace) {
    # 候補は同一ディレクトリに暗号化してから再復号・検証する。
    # File.Replace/Move が commit 点で、それ以前の失敗は旧 active を保つ。
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
            Assert-CredentialWrite $paths.Root $candidate
            $stream = [IO.FileStream]::new($candidate, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try { $stream.Write($cipher, 0, $cipher.Length); $stream.Flush($true) } finally { $stream.Dispose() }
            Set-CredentialFileAcl $candidate $paths.Root
            $verified = Unprotect-Record ([IO.File]::ReadAllBytes($candidate)) $Profile
            if ($verified.Generation -cne $record.Generation) { throw 'Credential operation unavailable.' }
            Invoke-Fault 'before-commit'
            if ($exists) {
                $backup = [IO.Path]::Combine($paths.Root, "$Profile.backup.$([Guid]::NewGuid().ToString('N'))")
                foreach ($changed in @($candidate, $paths.Active, $backup)) { Assert-CredentialWrite $paths.Root $changed }
                [IO.File]::Replace($candidate, $paths.Active, $backup)
            } else {
                # active が無い場合も残存 backup を復元元として扱わない。
                foreach ($changed in @($candidate, $paths.Active)) { Assert-CredentialWrite $paths.Root $changed }
                [IO.File]::Move($candidate, $paths.Active, $false)
            }
            $committed = $true
            try {
                # commit 後の掃除失敗は「未反映」と報告しない。active は新世代が正本。
                Invoke-Fault 'after-commit'
                Remove-OwnedFiles $paths $Profile
                return @{ Outcome = 'success' }
            } catch { return @{ Outcome = 'committed with cleanup pending' } }
        } finally { [Array]::Clear($cipher, 0, $cipher.Length) }
    } finally {
        if (-not $committed -and $candidate -and [IO.File]::Exists($candidate)) {
            try { Assert-CredentialFile $candidate; Assert-CredentialWrite $paths.Root $candidate; [IO.File]::Delete($candidate) } catch { }
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
    # 対象 profile の厳密な命名規則に合う一時物だけ消す。
    # ローカル削除は Cloudflare 上の token 失効を意味しない。
    $paths = Get-Paths $Profile $false
    if (-not [IO.Directory]::Exists($paths.Root)) { return 'already absent' }
    $lock = Enter-StoreLock $paths
    try {
        Assert-CredentialFile $paths.Active
        $present = [IO.File]::Exists($paths.Active)
        if ($present) { Assert-CredentialWrite $paths.Root $paths.Active; [IO.File]::Delete($paths.Active) }
        Remove-OwnedFiles $paths $Profile
        return $(if ($present) { 'removed' } else { 'already absent' })
    } finally { $lock.Dispose() }
}

Export-ModuleMember -Function Write-CredentialRecord, Get-CredentialStatus, Remove-CredentialRecord
