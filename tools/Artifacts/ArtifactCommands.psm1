Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactPaths.psm1') -Force

function Assert-JsonDuplicates([Text.Json.JsonElement] $Node) {
    if ($Node.ValueKind -eq [Text.Json.JsonValueKind]::Object) {
        $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($field in $Node.EnumerateObject()) {
            if (-not $names.Add($field.Name)) { throw 'Artifact JSON unavailable.' }
            Assert-JsonDuplicates $field.Value
        }
    } elseif ($Node.ValueKind -eq [Text.Json.JsonValueKind]::Array) {
        foreach ($value in $Node.EnumerateArray()) { Assert-JsonDuplicates $value }
    }
}

function Read-ArtifactJson([string] $Path) {
    if (-not [IO.Path]::IsPathFullyQualified($Path) -or -not [IO.File]::Exists($Path) -or
        (Get-Item -LiteralPath $Path).Length -lt 1 -or (Get-Item -LiteralPath $Path).Length -gt 1MB) {
        throw 'Artifact JSON unavailable.'
    }
    Assert-NoArtifactReparse $Path
    $bytes = [IO.File]::ReadAllBytes($Path)
    try {
        $raw = [Text.UTF8Encoding]::new($false, $true).GetString($bytes)
        $doc = [Text.Json.JsonDocument]::Parse($raw)
        try { Assert-JsonDuplicates $doc.RootElement }
        finally { $doc.Dispose() }
        return @{ Data = ($raw | ConvertFrom-Json -Depth 32 -DateKind String); Sha256 = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant() }
    } catch { throw 'Artifact JSON unavailable.' }
}

function Assert-Fields($Object, [string[]] $Names) {
    if ($null -eq $Object -or $Object.GetType() -ne [System.Management.Automation.PSCustomObject]) { throw 'Artifact schema unavailable.' }
    $actual = @($Object.PSObject.Properties.Name | Sort-Object -CaseSensitive)
    $required = @($Names | Sort-Object -CaseSensitive)
    if (($actual -join ',') -cne ($required -join ',')) { throw 'Artifact schema unavailable.' }
}

function Assert-Hex([string] $Value, [int] $Length) {
    if ($null -eq $Value -or $Value -cnotmatch ("\A[0-9a-f]{$Length}\z")) { throw 'Artifact identity unavailable.' }
}

function Assert-Utc([string] $Value) {
    [DateTimeOffset]$parsed = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParseExact($Value, 'o', [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::None, [ref]$parsed) -or $parsed.Offset -ne [TimeSpan]::Zero) {
        throw 'Artifact time unavailable.'
    }
    return $parsed
}

function Read-ArtifactConfig([string] $Path) {
    $item = Read-ArtifactJson $Path
    $data = $item.Data
    Assert-Fields $data @('schemaVersion','profile','endpoint','bucket','repositoryId','prefix','retentionSeconds',
        'observedAt','validUntil','settingsEvidencePath','settingsEvidenceSha256')
    if ($data.schemaVersion -isnot [long] -or $data.schemaVersion -ne 1 -or
        $data.profile -isnot [string] -or $data.profile -cne 'osm' -or
        $data.endpoint -isnot [string] -or $data.endpoint -cnotmatch '\Ahttps://[0-9a-f]{32}\.r2\.cloudflarestorage\.com\z' -or
        $data.bucket -isnot [string] -or $data.bucket -cne 'osm-artifacts' -or
        $data.repositoryId -isnot [string] -or
        $data.prefix -isnot [string] -or $data.prefix -cne 'probe/locked/' -or
        $data.retentionSeconds -isnot [long] -or $data.retentionSeconds -ne 86400 -or
        $data.observedAt -isnot [string] -or $data.validUntil -isnot [string] -or
        $data.settingsEvidencePath -isnot [string] -or $data.settingsEvidenceSha256 -isnot [string]) {
        throw 'Artifact config unavailable.'
    }
    Assert-Hex $data.repositoryId 64
    Assert-Hex $data.settingsEvidenceSha256 64
    $observed = Assert-Utc $data.observedAt
    $valid = Assert-Utc $data.validUntil
    $now = [DateTimeOffset]::UtcNow
    if ($valid -lt $now -or $observed -gt $now -or $valid -le $observed -or
        $valid -gt $observed.AddHours(24)) { throw 'Artifact config expired.' }
    $settings = Read-ArtifactJson $data.settingsEvidencePath
    if ($settings.Sha256 -cne $data.settingsEvidenceSha256) { throw 'Artifact settings hash mismatch.' }
    $evidence = $settings.Data
    Assert-Fields $evidence @('schemaVersion','endpoint','bucket','prefix','retentionSeconds','publicDevelopmentUrlEnabled',
        'customDomainCount','writerCanConfigure','bucketScopedWriter','lockEnabled','ageRuleCount',
        'dateRuleCount','indefiniteRuleCount','lifecycleCompatible','observedAt','observer')
    if ($evidence.schemaVersion -isnot [long] -or $evidence.schemaVersion -ne 1 -or
        $evidence.endpoint -isnot [string] -or $evidence.endpoint -cne $data.endpoint -or
        $evidence.bucket -isnot [string] -or $evidence.bucket -cne $data.bucket -or
        $evidence.prefix -isnot [string] -or $evidence.prefix -cne $data.prefix -or
        $evidence.retentionSeconds -isnot [long] -or $evidence.retentionSeconds -ne 86400 -or
        $evidence.publicDevelopmentUrlEnabled -isnot [bool] -or $evidence.publicDevelopmentUrlEnabled -or
        $evidence.customDomainCount -isnot [long] -or $evidence.customDomainCount -ne 0 -or
        $evidence.writerCanConfigure -isnot [bool] -or $evidence.writerCanConfigure -or
        $evidence.bucketScopedWriter -isnot [bool] -or -not $evidence.bucketScopedWriter -or
        $evidence.lockEnabled -isnot [bool] -or -not $evidence.lockEnabled -or
        $evidence.ageRuleCount -isnot [long] -or $evidence.ageRuleCount -ne 1 -or
        $evidence.dateRuleCount -isnot [long] -or $evidence.dateRuleCount -ne 0 -or
        $evidence.indefiniteRuleCount -isnot [long] -or $evidence.indefiniteRuleCount -ne 0 -or
        $evidence.lifecycleCompatible -isnot [bool] -or -not $evidence.lifecycleCompatible -or
        $evidence.observedAt -isnot [string] -or $evidence.observedAt -cne $data.observedAt -or
        $evidence.observer -isnot [string] -or [string]::IsNullOrWhiteSpace($evidence.observer) -or
        $evidence.observer.Length -gt 128) { throw 'Artifact settings unavailable.' }
    $null = Assert-Utc $evidence.observedAt
    $identity = ConvertTo-Json -InputObject ([object[]]@($data.endpoint,$data.bucket,$data.repositoryId,$data.prefix,[long]$data.retentionSeconds)) -Compress
    $profileId = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($identity))).ToLowerInvariant()
    return @{ Data = $data; Hash = $item.Sha256; ProfileId = $profileId }
}

function Read-ArtifactReference([string] $Text, $Config) {
    if (-not $Text.StartsWith('osm-artifact-v1:', [StringComparison]::Ordinal) -or $Text.Length -gt 4096) {
        throw 'Artifact reference unavailable.'
    }
    try {
        $encoded = $Text.Substring(16)
        if ($encoded -cnotmatch '\A[A-Za-z0-9_-]+\z') { throw 'Artifact reference unavailable.' }
        $base64 = $encoded.Replace('-', '+').Replace('_', '/')
        $base64 = $base64.PadRight($base64.Length + ((4 - $base64.Length % 4) % 4), '=')
        $decoded = [Convert]::FromBase64String($base64)
        if ([Convert]::ToBase64String($decoded).TrimEnd('=').Replace('+','-').Replace('/','_') -cne $encoded) {
            throw 'Artifact reference unavailable.'
        }
        $raw = [Text.UTF8Encoding]::new($false, $true).GetString($decoded)
        $document = [Text.Json.JsonDocument]::Parse($raw)
        try { Assert-JsonDuplicates $document.RootElement } finally { $document.Dispose() }
        $value = $raw | ConvertFrom-Json -Depth 8 -DateKind String
        Assert-Fields $value @('schemaVersion','profileId','repositoryId','key','base','head','runId','packageBytes','manifestSha256','retainUntil')
        if ($value.schemaVersion -isnot [long] -or $value.schemaVersion -ne 1 -or
            $value.profileId -isnot [string] -or $value.profileId -cne $Config.ProfileId -or
            $value.repositoryId -isnot [string] -or $value.repositoryId -cne $Config.Data.repositoryId -or
            $value.key -isnot [string] -or $value.base -isnot [string] -or $value.head -isnot [string] -or
            $value.runId -isnot [string] -or $value.manifestSha256 -isnot [string] -or
            $value.retainUntil -isnot [string] -or
            ($value.packageBytes -isnot [int] -and $value.packageBytes -isnot [long])) { throw 'Artifact reference unavailable.' }
        Assert-Hex $value.base 40; Assert-Hex $value.head 40; Assert-Hex $value.runId 32
        Assert-Hex $value.manifestSha256 64
        if ($value.key -cne "probe/locked/$($value.repositoryId)/$($value.head)/$($value.runId)/bundle.zip" -or
            $value.packageBytes -lt 1 -or $value.packageBytes -gt 256MB) { throw 'Artifact reference unavailable.' }
        $null = Assert-Utc $value.retainUntil
        return $value
    } catch { throw 'Artifact reference unavailable.' }
}

function New-ArtifactReference($Config, [string] $Key, [string] $Base, [string] $Head,
    [string] $RunId, [long] $Bytes, [string] $ManifestHash, [string] $RetainUntil) {
    $value = [ordered]@{
        schemaVersion = 1; profileId = $Config.ProfileId; repositoryId = $Config.Data.repositoryId
        key = $Key; base = $Base; head = $Head; runId = $RunId
        packageBytes = $Bytes; manifestSha256 = $ManifestHash; retainUntil = $RetainUntil
    }
    $json = ConvertTo-Json -InputObject $value -Compress -Depth 4
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json)).TrimEnd('=').Replace('+','-').Replace('/','_')
    return 'osm-artifact-v1:' + $encoded
}

Export-ModuleMember -Function Read-ArtifactJson, Assert-JsonDuplicates, Assert-Fields, Assert-Hex, Assert-Utc,
    Read-ArtifactConfig, Read-ArtifactReference, New-ArtifactReference
