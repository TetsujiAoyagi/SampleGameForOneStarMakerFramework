Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'ArtifactAcl.psm1') -Force

# TestRoot is set only by offline module-scope tests. Public commands have no root override.
$script:TestRoot = $null
$script:WriteHook = $null # Offline fault injection; public path API does not expose this.

function Invoke-EvidenceWriteHook([string] $Stage,[string] $Pending,[string] $Final) {
    if ($script:WriteHook) { $null = & $script:WriteHook $Stage $Pending $Final }
}

function Get-EvidenceRoot([string] $Kind) {
    if ($Kind -cnotin @('evidence-ledgers','evidence-handoffs','evidence-retention')) { throw 'Evidence path unavailable.' }
    if (-not [OperatingSystem]::IsWindows()) { throw 'Evidence path unavailable.' }
    $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $base = if ($script:TestRoot) { [IO.Path]::GetFullPath($script:TestRoot) }
        else { [IO.Path]::Combine($local,'OneStarMaker','Artifacts') }
    $repo = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot,'..','..'))
    if ((Test-ArtifactWithin $base $repo) -or (Test-ArtifactWithin $base 'D:\OneDrive\OSM-Artifacts')) {
        throw 'Evidence path unavailable.'
    }
    Assert-NoArtifactReparse $base
    return [IO.Path]::Combine($base,$Kind)
}

function New-EvidenceDirectory([string] $Kind, [string] $Id) {
    if ($Id -cnotmatch '\A[0-9a-f]{32}\z') { throw 'Evidence identity unavailable.' }
    $root = Get-EvidenceRoot $Kind
    $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $shared = if ($script:TestRoot) { $script:TestRoot } else { [IO.Path]::Combine($local,'OneStarMaker') }
    if (-not [IO.Directory]::Exists($shared)) {
        [IO.Directory]::CreateDirectory($shared) | Out-Null
        Set-ArtifactAcl $shared $true
    }
    Assert-NoArtifactReparse $shared
    $current = $shared
    foreach ($part in [IO.Path]::GetRelativePath($shared,$root).Split('\',[StringSplitOptions]::RemoveEmptyEntries)) {
        $current = [IO.Path]::Combine($current,$part)
        Assert-NoArtifactReparse $current
        if (-not [IO.Directory]::Exists($current)) {
            [IO.Directory]::CreateDirectory($current) | Out-Null
            Set-ArtifactAcl $current $true
        } else { Assert-ArtifactAcl $current $true }
    }
    $directory = [IO.Path]::Combine($root,$Id)
    if ([IO.Directory]::Exists($directory) -or [IO.File]::Exists($directory)) { throw 'Evidence record already exists.' }
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    Set-ArtifactAcl $directory $true
    return $directory
}

function Write-EvidenceImmutable([string] $Kind, [string] $Id, [string] $Name, $Value) {
    $expectedName = switch ($Kind) {
        'evidence-ledgers' { 'ledger.json' }
        'evidence-handoffs' { 'reader-input.json' }
        'evidence-retention' { 'retention.json' }
        default { throw 'Evidence path unavailable.' }
    }
    if ($Name -cne $expectedName) { throw 'Evidence filename unavailable.' }
    $dir = New-EvidenceDirectory $Kind $Id
    $pending = [IO.Path]::Combine($dir,$Name + '.pending')
    $final = [IO.Path]::Combine($dir,$Name)
    $json = ConvertTo-Json -InputObject $Value -Compress -Depth 24
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
    if ($bytes.Length -lt 1 -or $bytes.Length -gt 1MB) { throw 'Evidence record size unavailable.' }
    try {
        Invoke-EvidenceWriteHook 'before-pending' $pending $final
        $stream = [IO.FileStream]::new($pending,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
        try { $stream.Write($bytes,0,$bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
        Set-ArtifactAcl $pending $false
        Invoke-EvidenceWriteHook 'after-pending' $pending $final
        Invoke-EvidenceWriteHook 'before-rename' $pending $final
        [IO.File]::Move($pending,$final)
        Assert-ArtifactAcl $final $false
        return [pscustomobject]@{ Path=$final; Sha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant() }
    } catch { throw 'Evidence record pending or unavailable.' }
}

Export-ModuleMember -Function Get-EvidenceRoot, Write-EvidenceImmutable
