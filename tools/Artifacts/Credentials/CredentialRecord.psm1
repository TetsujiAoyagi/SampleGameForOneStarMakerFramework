Set-StrictMode -Version Latest

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

function Assert-Envelope($Envelope, [string] $Profile) {
    Assert-Record $Envelope.Active $Profile
    if ($Envelope.Version -eq 1) { return }
    if ($Envelope.Version -ne 2) { throw 'Credential operation unavailable.' }
    if ($null -eq $Envelope.Retired -and $null -eq $Envelope.TransitionId) { return }
    if ($null -eq $Envelope.Retired -or $Envelope.TransitionId -isnot [string] -or
        $Envelope.TransitionId -cnotmatch '\A[0-9a-f]{32}\z') { throw 'Credential operation unavailable.' }
    Assert-Record $Envelope.Retired $Profile
    if ($Envelope.Active.Generation -ceq $Envelope.Retired.Generation) { throw 'Credential operation unavailable.' }
}

function Unprotect-Envelope([byte[]] $Cipher, [string] $Profile) {
    $plain = $null
    try {
        $plain = [Security.Cryptography.ProtectedData]::Unprotect($Cipher, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $json = [Text.Encoding]::UTF8.GetString($plain)
        $doc = [Text.Json.JsonDocument]::Parse($json)
        try {
            $root = $doc.RootElement
            if ($root.ValueKind -ne [Text.Json.JsonValueKind]::Object) { throw 'Credential operation unavailable.' }
            $versionFields = @($root.EnumerateObject() | Where-Object Name -eq 'Version')
            if ($versionFields.Count -ne 1 -or $versionFields[0].Value.ValueKind -ne [Text.Json.JsonValueKind]::Number) {
                throw 'Credential operation unavailable.'
            }
            $version = $versionFields[0].Value.GetInt32()
            if ($version -eq 1) {
                Assert-JsonSchema $json
                $record = $json | ConvertFrom-Json -Depth 5 -DateKind String
                Assert-Record $record $Profile
                return [pscustomobject]@{ Version = 1; Active = $record; Retired = $null; TransitionId = $null }
            }
            if ($version -ne 2) { throw 'Credential operation unavailable.' }
            $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
            foreach ($property in $root.EnumerateObject()) {
                if (-not $names.Add($property.Name) -or $property.Name -cnotin @('Version','Active','Retired','TransitionId')) {
                    throw 'Credential operation unavailable.'
                }
                if ($property.Name -ceq 'Active' -or ($property.Name -ceq 'Retired' -and $property.Value.ValueKind -ne [Text.Json.JsonValueKind]::Null)) {
                    Assert-JsonSchema $property.Value.GetRawText()
                }
            }
            if ($names.Count -ne 4) { throw 'Credential operation unavailable.' }
            $retiredElement = $root.GetProperty('Retired')
            $transitionElement = $root.GetProperty('TransitionId')
            if ($retiredElement.ValueKind -ne [Text.Json.JsonValueKind]::Null -and $retiredElement.ValueKind -ne [Text.Json.JsonValueKind]::Object) {
                throw 'Credential operation unavailable.'
            }
            if ($transitionElement.ValueKind -ne [Text.Json.JsonValueKind]::Null -and $transitionElement.ValueKind -ne [Text.Json.JsonValueKind]::String) {
                throw 'Credential operation unavailable.'
            }
            $envelope = $json | ConvertFrom-Json -Depth 6 -DateKind String
            if ($envelope.Version -isnot [long] -or $envelope.Version -ne 2) { throw 'Credential operation unavailable.' }
            Assert-Envelope $envelope $Profile
            return $envelope
        } finally { $doc.Dispose() }
    } catch { throw 'Credential operation unavailable.' }
    finally { if ($plain) { [Array]::Clear($plain, 0, $plain.Length) } }
}

Export-ModuleMember -Function Assert-Record, Assert-JsonSchema, Protect-Record, Unprotect-Record, Assert-Envelope, Unprotect-Envelope
