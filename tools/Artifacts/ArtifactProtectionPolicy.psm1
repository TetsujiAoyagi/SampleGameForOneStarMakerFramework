Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactCommands.psm1') -Force

function Read-EvidenceConfig([string] $Path) {
    $item = Read-ArtifactJson $Path
    $data = $item.Data
    Assert-Fields $data @('schemaVersion','profile','purpose','endpoint','bucket','repositoryId','prefix',
        'retentionSeconds','observedAt','validUntil','settingsEvidencePath','settingsEvidenceSha256')
    if ($data.schemaVersion -isnot [long] -or $data.schemaVersion -ne 1 -or
        $data.profile -cne 'osm' -or $data.purpose -cne 'evidence' -or
        $data.endpoint -isnot [string] -or $data.endpoint -cnotmatch '\Ahttps://[0-9a-f]{32}\.r2\.cloudflarestorage\.com\z' -or
        $data.bucket -cne 'osm-artifacts' -or
        $data.repositoryId -cne '4d2d1728f1db77f50605a10da0bbf10fd31443c381c4f312658134fffb850b3b' -or
        $data.prefix -cne 'evidence/first-use/' -or $data.retentionSeconds -isnot [long] -or
        $data.retentionSeconds -ne 2592000 -or $data.settingsEvidenceSha256 -isnot [string]) {
        throw 'Evidence config unavailable.'
    }
    Assert-Hex $data.settingsEvidenceSha256 64
    $observed = Assert-Utc $data.observedAt
    $valid = Assert-Utc $data.validUntil
    $now = [DateTimeOffset]::UtcNow
    if ($observed -gt $now -or $valid -lt $now -or $valid -le $observed -or $valid -gt $observed.AddHours(24)) {
        throw 'Evidence config expired.'
    }
    $settings = Read-ArtifactJson $data.settingsEvidencePath
    if ($settings.Sha256 -cne $data.settingsEvidenceSha256) { throw 'Evidence settings hash mismatch.' }
    Assert-EvidenceSettings $settings.Data $data
    $identity = ConvertTo-Json -InputObject ([object[]]@($data.endpoint,$data.bucket,$data.repositoryId,$data.prefix,[long]$data.retentionSeconds)) -Compress
    $profileId = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($identity))).ToLowerInvariant()
    return @{ Data = $data; Hash = $item.Sha256; ProfileId = $profileId; SettingsHash = $settings.Sha256; Path = $Path }
}

function Assert-EvidenceSettings($Settings, $Config) {
    Assert-Fields $Settings @('schemaVersion','endpoint','bucket','publicDevelopmentUrlEnabled','customDomainCount',
        'writerCanConfigure','bucketScopedWriter','writerPermission','lockRules','lifecycleRules','observedAt','observer')
    if ($Settings.schemaVersion -isnot [long] -or $Settings.schemaVersion -ne 1 -or
        $Settings.endpoint -cne $Config.endpoint -or $Settings.bucket -cne $Config.bucket -or
        $Settings.publicDevelopmentUrlEnabled -isnot [bool] -or $Settings.publicDevelopmentUrlEnabled -or
        $Settings.customDomainCount -isnot [long] -or $Settings.customDomainCount -ne 0 -or
        $Settings.writerCanConfigure -isnot [bool] -or $Settings.writerCanConfigure -or
        $Settings.bucketScopedWriter -isnot [bool] -or -not $Settings.bucketScopedWriter -or
        $Settings.writerPermission -cne 'Object Read & Write' -or
        $Settings.observedAt -cne $Config.observedAt -or
        $Settings.observer -isnot [string] -or [string]::IsNullOrWhiteSpace($Settings.observer) -or
        $Settings.observer.Length -gt 128) { throw 'Evidence settings unavailable.' }
    $null = Assert-Utc $Settings.observedAt
    if ($Settings.lockRules -isnot [array] -or $Settings.lifecycleRules -isnot [array]) {
        throw 'Evidence rules unavailable.'
    }
    $matching = 0
    $evidenceExact = 0
    $probeExact = 0
    foreach ($rule in $Settings.lockRules) {
        Assert-Fields $rule @('prefix','enabled','mode','retentionSeconds')
        if ($rule.prefix -isnot [string] -or $rule.enabled -isnot [bool] -or
            $rule.mode -cnotin @('age','date','indefinite') -or
            ($null -ne $rule.retentionSeconds -and $rule.retentionSeconds -isnot [long])) {
            throw 'Evidence lock rule unavailable.'
        }
        # A rule on an empty/parent/exact prefix applies to the bundle; disabled rules do not.
        if ($rule.enabled -and $Config.prefix.StartsWith($rule.prefix, [StringComparison]::Ordinal)) {
            $matching++
            if ($rule.prefix -cne $Config.prefix -or $rule.mode -cne 'age' -or
                $rule.retentionSeconds -ne 2592000) { throw 'Evidence lock overlap.' }
        }
        if ($rule.prefix -ceq $Config.prefix) { $evidenceExact++ }
        if ($rule.prefix -ceq 'probe/locked/') { $probeExact++ }
        if ($rule.enabled -and $rule.prefix.StartsWith($Config.prefix, [StringComparison]::Ordinal) -and
            $rule.prefix -cne $Config.prefix) { throw 'Evidence exact-key lock overlap.' }
        if ($rule.prefix -ceq 'probe/locked/' -and
            (-not $rule.enabled -or $rule.mode -cne 'age' -or $rule.retentionSeconds -ne 86400)) {
            throw 'Existing probe lock changed.'
        }
    }
    if ($matching -ne 1 -or $evidenceExact -ne 1 -or $probeExact -ne 1) { throw 'Evidence lock unavailable.' }
    if ($Settings.lifecycleRules.Count -ne 1) { throw 'Evidence lifecycle unavailable.' }
    $lifecycle = $Settings.lifecycleRules[0]
    Assert-Fields $lifecycle @('type','prefix','enabled','days')
    if ($lifecycle.type -cne 'abort-incomplete-multipart' -or $lifecycle.prefix -cne '' -or
        $lifecycle.enabled -isnot [bool] -or -not $lifecycle.enabled -or
        $lifecycle.days -isnot [long] -or $lifecycle.days -ne 7) {
        throw 'Evidence lifecycle unavailable.'
    }
}

function Test-EvidenceSettingsEquivalent($Before, $After) {
    foreach ($name in @('endpoint','bucket','publicDevelopmentUrlEnabled','customDomainCount',
            'writerCanConfigure','bucketScopedWriter','writerPermission')) {
        if ($Before.$name -cne $After.$name) { return $false }
    }
    $lockA = @($Before.lockRules | ForEach-Object { "$($_.prefix)|$($_.enabled)|$($_.mode)|$($_.retentionSeconds)" } | Sort-Object) -join ';'
    $lockB = @($After.lockRules | ForEach-Object { "$($_.prefix)|$($_.enabled)|$($_.mode)|$($_.retentionSeconds)" } | Sort-Object) -join ';'
    $lifeA = @($Before.lifecycleRules | ForEach-Object { "$($_.type)|$($_.prefix)|$($_.enabled)|$($_.days)" } | Sort-Object) -join ';'
    $lifeB = @($After.lifecycleRules | ForEach-Object { "$($_.type)|$($_.prefix)|$($_.enabled)|$($_.days)" } | Sort-Object) -join ';'
    return $lockA -ceq $lockB -and $lifeA -ceq $lifeB
}

Export-ModuleMember -Function Read-EvidenceConfig, Assert-EvidenceSettings, Test-EvidenceSettingsEquivalent
