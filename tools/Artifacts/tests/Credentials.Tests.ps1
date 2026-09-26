param([string] $Case = '*')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$storePath = Join-Path $PSScriptRoot '../Credentials/CredentialStore.psm1'
$store = Import-Module $storePath -Force -PassThru
$root = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'osm-credential-test-' + [Guid]::NewGuid().ToString('N'))
$script:passed = [Collections.Generic.List[string]]::new()
$script:failed = [Collections.Generic.List[string]]::new()
$script:writes = [Collections.Generic.List[string]]::new()
$sentinelId = 'DUMMY_ID_' + [Guid]::NewGuid().ToString('N')
$sentinelSecret = 'DUMMY_SECRET_' + [Guid]::NewGuid().ToString('N')

function Invoke-StorePrivate([scriptblock] $Code, [object[]] $Values = @()) {
    return & $store $Code @Values
}
function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Reset-Fixture {
    Invoke-StorePrivate { param($value) $script:TestRoot = $value; $script:Fault = $null; $script:Clock = $null; $script:FakeValidator = $null } @($root)
    if ([IO.Directory]::Exists($root)) { [IO.Directory]::Delete($root, $true) }
}
function Record-Writes {
    if ([IO.Directory]::Exists($root)) {
        foreach ($file in [IO.Directory]::EnumerateFiles($root)) { $script:writes.Add($file) }
    }
}
function Run([string] $Name, [scriptblock] $Body) {
    if ($Name -notlike $Case) { return }
    try {
        Reset-Fixture
        & $Body
        Record-Writes
        $script:passed.Add($Name)
    } catch {
        $script:failed.Add($Name)
        # Never print exception details: platform errors may include sensitive context.
    }
}

try {
    Run 'encrypted round trip and safe status' {
        $result = Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false
        Assert ($result.Outcome -eq 'success') 'initial commit'
        $status = Get-CredentialStatus 'osm'
        Assert ($status.Bucket -eq 'osm-artifacts' -and $null -eq $status.Endpoint) 'metadata'
        Assert (-not ($status | ConvertTo-Json).Contains($sentinelId)) 'status ID'
        Assert (-not ($status | ConvertTo-Json).Contains($sentinelSecret)) 'status secret'
        $cipher = [IO.File]::ReadAllBytes([IO.Path]::Combine($root, 'osm.active'))
        Assert (-not ([Text.Encoding]::UTF8.GetString($cipher).Contains($sentinelId))) 'cipher ID'
        Assert (-not ([Text.Encoding]::UTF8.GetString($cipher).Contains($sentinelSecret))) 'cipher secret'
    }
    Run 'duplicate set and successful replacement' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $before = (Get-CredentialStatus 'osm').Generation
        try { Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null; throw 'accepted duplicate' } catch {
            if ($_.Exception.Message -eq 'accepted duplicate') { throw }
        }
        Write-CredentialRecord 'osm' ($sentinelId + 'R') ($sentinelSecret + 'R') $true | Out-Null
        Assert ((Get-CredentialStatus 'osm').Generation -ne $before) 'replacement generation'
    }
    Run 'fake validation refuses before encryption' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $before = (Get-CredentialStatus 'osm').Generation
        $filesBefore = @([IO.Directory]::EnumerateFiles($root)).Count
        Invoke-StorePrivate { $script:FakeValidator = { throw 'fake refusal' } }
        try { Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $true | Out-Null; throw 'accepted fake refusal' } catch {
            if ($_.Exception.Message -eq 'accepted fake refusal') { throw }
        }
        Assert ((Get-CredentialStatus 'osm').Generation -eq $before) 'old active lost'
        Assert (@([IO.Directory]::EnumerateFiles($root)).Count -eq $filesBefore) 'extra file'
    }
    Run 'precommit interruption preserves active' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $before = (Get-CredentialStatus 'osm').Generation
        Invoke-StorePrivate { $script:Fault = { param($point) if ($point -eq 'before-commit') { throw 'injected' } } }
        try { Write-CredentialRecord 'osm' ($sentinelId + 'R') ($sentinelSecret + 'R') $true | Out-Null; throw 'accepted fault' } catch {
            if ($_.Exception.Message -eq 'accepted fault') { throw }
        }
        Assert ((Get-CredentialStatus 'osm').Generation -eq $before) 'old active lost'
        Assert (@([IO.Directory]::EnumerateFiles($root, 'osm.candidate.*')).Count -eq 0) 'candidate retained'
    }
    Run 'postcommit cleanup pending is distinct' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $before = (Get-CredentialStatus 'osm').Generation
        Invoke-StorePrivate { $script:Fault = { param($point) if ($point -eq 'after-commit') { throw 'injected' } } }
        $result = Write-CredentialRecord 'osm' ($sentinelId + 'R') ($sentinelSecret + 'R') $true
        Assert ($result.Outcome -eq 'committed with cleanup pending') 'cleanup outcome'
        Assert ((Get-CredentialStatus 'osm').Generation -ne $before) 'commit missing'
    }
    Run 'missing active never promotes backup' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $active = [IO.Path]::Combine($root, 'osm.active')
        [IO.File]::Move($active, [IO.Path]::Combine($root, 'osm.backup.' + [Guid]::NewGuid().ToString('N')))
        try { Get-CredentialStatus 'osm' | Out-Null; throw 'promoted backup' } catch {
            if ($_.Exception.Message -eq 'promoted backup') { throw }
        }
        try { Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null; throw 'ignored backup' } catch {
            if ($_.Exception.Message -eq 'ignored backup') { throw }
        }
    }
    Run 'tamper is refused' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        [IO.File]::WriteAllBytes([IO.Path]::Combine($root, 'osm.active'), [byte[]]@(1,2,3))
        try { Get-CredentialStatus 'osm' | Out-Null; throw 'accepted tamper' } catch {
            if ($_.Exception.Message -eq 'accepted tamper') { throw }
        }
    }
    Run 'remove is idempotent and leaves unrelated files' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $unrelated = [IO.Path]::Combine($root, 'other.txt')
        [IO.File]::WriteAllText($unrelated, 'unrelated')
        Assert ((Remove-CredentialRecord 'osm') -eq 'removed') 'remove'
        Assert ((Remove-CredentialRecord 'osm') -eq 'already absent') 'second remove'
        Assert ([IO.File]::Exists($unrelated)) 'unrelated file deleted'
    }
    Run 'CLI rejects redirected input' {
        $cli = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..', 'artifacts.ps1'))
        $output = & pwsh -NoProfile -File $cli credentials set --profile osm 2>&1 | Out-String
        Assert ($LASTEXITCODE -ne 0) 'redirected CLI accepted'
        Assert (-not $output.Contains($sentinelId) -and -not $output.Contains($sentinelSecret)) 'output secret'
    }
    Run 'unsafe ACL and reparse refusal' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $active = [IO.Path]::Combine($root, 'osm.active')
        $acl = [IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($active))
        $acl.SetAccessRuleProtection($false, $true)
        [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($active), $acl)
        try { Get-CredentialStatus 'osm' | Out-Null; throw 'accepted ACL' } catch {
            if ($_.Exception.Message -eq 'accepted ACL') { throw }
        }
    }
    Run 'exclusive lock times out without active mutation' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $generation = (Get-CredentialStatus 'osm').Generation
        $lockPath = [IO.Path]::Combine($root, 'osm.lock')
        $held = [IO.FileStream]::new($lockPath, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try {
            $timer = [Diagnostics.Stopwatch]::StartNew()
            try { Write-CredentialRecord 'osm' ($sentinelId + 'R') ($sentinelSecret + 'R') $true | Out-Null; throw 'ignored lock' } catch {
                if ($_.Exception.Message -eq 'ignored lock') { throw }
            }
            Assert ($timer.Elapsed -le [TimeSpan]::FromSeconds(6)) 'lock exceeded bound'
        } finally { $held.Dispose() }
        Assert ((Get-CredentialStatus 'osm').Generation -eq $generation) 'active changed on timeout'
    }
    Run 'ancestor reparse is refused' {
        $parent = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'osm-link-' + [Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateSymbolicLink($parent, [IO.Path]::GetTempPath()) | Out-Null
        try {
            $linkedRoot = [IO.Path]::Combine($parent, 'osm-test-' + [Guid]::NewGuid().ToString('N'))
            Invoke-StorePrivate { param($value) $script:TestRoot = $value } @($linkedRoot)
            try { Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null; throw 'accepted reparse' } catch {
                if ($_.Exception.Message -eq 'accepted reparse') { throw }
            }
        } finally { [IO.Directory]::Delete($parent) }
    }
    # Cross-user execution and PTY entry are recorded separately by Phase C when available.
    foreach ($write in $script:writes) {
        Assert ($write.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) 'write escaped fixture'
    }
    if ([IO.Directory]::Exists($root)) {
        foreach ($file in [IO.Directory]::EnumerateFiles($root)) {
            $contents = [IO.File]::ReadAllBytes($file)
            $asText = [Text.Encoding]::UTF8.GetString($contents)
            Assert (-not $asText.Contains($sentinelId) -and -not $asText.Contains($sentinelSecret)) 'persisted sentinel'
        }
    }
    $joined = (@($script:passed) + @($script:failed)) -join ','
    Assert (-not $joined.Contains($sentinelId) -and -not $joined.Contains($sentinelSecret)) 'case report secret'
    foreach ($name in $script:passed) { Write-Output "PASS $name" }
    foreach ($name in $script:failed) { Write-Output "FAIL $name" }
    Write-Output "Cases: $($script:passed.Count + $script:failed.Count); passed: $($script:passed.Count); failed: $($script:failed.Count)"
    if ($script:failed.Count -gt 0 -or $script:passed.Count -eq 0) { exit 1 }
} finally {
    Invoke-StorePrivate { $script:TestRoot = $null; $script:Fault = $null; $script:Clock = $null; $script:FakeValidator = $null }
    if ([IO.Directory]::Exists($root)) { [IO.Directory]::Delete($root, $true) }
}
