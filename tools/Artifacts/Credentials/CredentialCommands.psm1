Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'CredentialStore.psm1') -Force

function Test-InteractiveInput {
    # 秘密を含む入力が pipe やリダイレクト経由になる実行形態を先に拒否する。
    return [OperatingSystem]::IsWindows() -and -not [Console]::IsInputRedirected -and
        -not [Console]::IsOutputRedirected -and -not [Console]::IsErrorRedirected
}

function Read-MaskedValue([string] $Label) {
    # ReadKey(true) でキーの echo を抑える。可変 char list は最後に消すが、
    # 返却した .NET string の完全消去は保証できないため処理寿命を短くする。
    $chars = [Collections.Generic.List[char]]::new()
    try {
        [Console]::Write("${Label}: ")
        while ($true) {
            $key = [Console]::ReadKey($true)
            if ($key.Key -eq [ConsoleKey]::Enter) { [Console]::WriteLine(); break }
            if ($key.Key -eq [ConsoleKey]::Escape -or $key.Key -eq [ConsoleKey]::C) {
                if ($key.Key -eq [ConsoleKey]::Escape -or ($key.Modifiers -band [ConsoleModifiers]::Control)) {
                    throw 'Credential operation unavailable.'
                }
            }
            if ($key.Key -eq [ConsoleKey]::Backspace) {
                if ($chars.Count -gt 0) { $chars.RemoveAt($chars.Count - 1) }
                continue
            }
            if ($key.KeyChar -ge [char]33 -and $key.KeyChar -le [char]126 -and $chars.Count -lt 256) {
                $chars.Add($key.KeyChar)
            } else { throw 'Credential operation unavailable.' }
        }
        if ($chars.Count -eq 0) { throw 'Credential operation unavailable.' }
        return [string]::new($chars.ToArray())
    } finally {
        for ($i = 0; $i -lt $chars.Count; $i++) { $chars[$i] = [char]0 }
        $chars.Clear()
    }
}

function Invoke-CredentialSet([string] $Profile, [bool] $Replace) {
    # 最初の prompt とファイル操作より前に非対話入力を拒否する。
    if (-not (Test-InteractiveInput)) { throw 'Credential operation unavailable.' }
    $id = $null
    $secret = $null
    try {
        $id = Read-MaskedValue 'Access Key ID'
        $secret = Read-MaskedValue 'Secret Access Key'
        return Write-CredentialRecord $Profile $id $secret $Replace
    } finally { $id = $null; $secret = $null }
}

function Invoke-CredentialStatus([string] $Profile) {
    # Store から受け取るのは安全な metadata のみ。接続時刻や R2 成功を捏造しない。
    $metadata = Get-CredentialStatus $Profile
    return @(
        "profile: $($metadata.Profile)", "bucket: $($metadata.Bucket)",
        "endpoint: $(if ($null -eq $metadata.Endpoint) { 'unconfigured' } else { $metadata.Endpoint })",
        "generation: $($metadata.Generation)", "created UTC: $($metadata.CreatedUtc)",
        "updated UTC: $($metadata.UpdatedUtc)",
        'token reference: uncollected', 'local-only; R2 connectivity unverified'
    )
}

function Invoke-CredentialRemove([string] $Profile) {
    $outcome = Remove-CredentialRecord $Profile
    return "$outcome; Cloudflare token revocation was not performed."
}

Export-ModuleMember -Function Invoke-CredentialSet, Invoke-CredentialStatus, Invoke-CredentialRemove
