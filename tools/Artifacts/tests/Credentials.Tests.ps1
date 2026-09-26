param([string] $Case = '*')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$storePath = Join-Path $PSScriptRoot '../Credentials/CredentialStore.psm1'
$store = Import-Module $storePath -Force -PassThru
$pathModule = $store.NestedModules | Where-Object Name -eq 'CredentialPathAcl' | Select-Object -First 1
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
function Assert-NoSentinel([string] $Text, [string] $Where) {
    Assert (-not $Text.Contains($sentinelId) -and -not $Text.Contains($sentinelSecret)) "sentinel in $Where"
}
function Assert-SafeError($ErrorRecord) { Assert-NoSentinel ($ErrorRecord | Out-String) 'caught exception' }
function Test-BytesContain([byte[]] $Haystack, [byte[]] $Needle) {
    for ($i = 0; $i -le $Haystack.Length - $Needle.Length; $i++) {
        $found = $true
        for ($j = 0; $j -lt $Needle.Length; $j++) {
            if ($Haystack[$i + $j] -ne $Needle[$j]) { $found = $false; break }
        }
        if ($found) { return $true }
    }
    return $false
}
function Assert-FixtureCleanOfSentinels {
    if (-not [IO.Directory]::Exists($root)) { return }
    foreach ($file in [IO.Directory]::EnumerateFiles($root, '*', [IO.SearchOption]::AllDirectories)) {
        Assert-NoSentinel $file 'file path'
        $bytes = [IO.File]::ReadAllBytes($file)
        foreach ($value in @($sentinelId, $sentinelSecret)) {
            foreach ($encoding in @([Text.Encoding]::UTF8, [Text.Encoding]::Unicode)) {
                Assert (-not (Test-BytesContain $bytes ($encoding.GetBytes($value)))) 'persisted sentinel'
            }
        }
    }
}
function Reset-Fixture {
    Invoke-StorePrivate { param($value) $script:TestRoot = $value; $script:Fault = $null; $script:Clock = $null; $script:FakeValidator = $null } @($root)
    if ([IO.Directory]::Exists($root)) { [IO.Directory]::Delete($root, $true) }
    $script:writes.Clear()
    & $pathModule { param($ledger) $script:WriteLedger = $ledger } $script:writes
}
function Run([string] $Name, [scriptblock] $Body) {
    if ($Name -notlike $Case) { return }
    Reset-Fixture
    $captured = @()
    $exceptionText = ''
    $caseFailed = $false
    try {
        $captured = @(& $Body *>&1)
    } catch {
        $caseFailed = $true
        $exceptionText = $_.ToString() + [Environment]::NewLine + $_.ScriptStackTrace
    } finally {
        try {
            Assert-NoSentinel ($captured | Out-String) 'captured streams'
            Assert-NoSentinel $exceptionText 'exception'
            Assert-FixtureCleanOfSentinels
            foreach ($write in $script:writes) {
                Assert ($write.Equals($root, [StringComparison]::OrdinalIgnoreCase) -or
                    $write.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) 'write escaped fixture'
            }
            if ($Name -notin @('CLI rejects redirected input', 'CLI verbose and error output is safe', 'ancestor reparse is refused')) {
                Assert ($script:writes.Count -gt 0) 'write ledger was empty'
            }
        } catch { $caseFailed = $true }
    }
    if ($caseFailed) { $script:failed.Add($Name) } else { $script:passed.Add($Name) }
}

try {
    Run 'encrypted round trip and safe status' {
        $result = Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false
        Assert ($result.Outcome -eq 'success') 'initial commit'
        $status = Get-CredentialStatus 'osm'
        Assert ($status.Bucket -eq 'osm-artifacts' -and $null -eq $status.Endpoint) 'metadata'
        Assert (-not ($status | ConvertTo-Json).Contains($sentinelId)) 'status ID'
        Assert (-not ($status | ConvertTo-Json).Contains($sentinelSecret)) 'status secret'
        Write-Output ($status | ConvertTo-Json -Compress)
        $cipher = [IO.File]::ReadAllBytes([IO.Path]::Combine($root, 'osm.active'))
        Assert (-not ([Text.Encoding]::UTF8.GetString($cipher).Contains($sentinelId))) 'cipher ID'
        Assert (-not ([Text.Encoding]::UTF8.GetString($cipher).Contains($sentinelSecret))) 'cipher secret'
    }
    Run 'duplicate set and successful replacement' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $before = (Get-CredentialStatus 'osm').Generation
        try { Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null; throw 'accepted duplicate' } catch {
            Assert-SafeError $_
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
            Assert-SafeError $_
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
            Assert-SafeError $_
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
            Assert-SafeError $_
            if ($_.Exception.Message -eq 'promoted backup') { throw }
        }
        try { Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null; throw 'ignored backup' } catch {
            Assert-SafeError $_
            if ($_.Exception.Message -eq 'ignored backup') { throw }
        }
    }
    Run 'tamper is refused' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        [IO.File]::WriteAllBytes([IO.Path]::Combine($root, 'osm.active'), [byte[]]@(1,2,3))
        try { Get-CredentialStatus 'osm' | Out-Null; throw 'accepted tamper' } catch {
            Assert-SafeError $_
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
        $cliArgs = @('-NoProfile', '-File', $cli, 'credentials', 'set', '--profile', 'osm')
        Assert-NoSentinel ($cliArgs -join ' ') 'child process arguments'
        $output = & pwsh @cliArgs 2>&1 | Out-String
        Assert ($LASTEXITCODE -ne 0) 'redirected CLI accepted'
        Assert-NoSentinel $output 'redirected CLI output'
    }
    Run 'CLI verbose and error output is safe' {
        $cli = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..', 'artifacts.ps1'))
        foreach ($arguments in @(
            @('-NoProfile', '-File', $cli, 'credentials', 'status', '--profile', 'wrong'),
            @('-NoProfile', '-File', $cli, 'credentials', 'set', '--profile', 'osm', '-Verbose')
        )) {
            Assert-NoSentinel ($arguments -join ' ') 'child process arguments'
            $output = & pwsh @arguments 2>&1 | Out-String
            Assert ($LASTEXITCODE -ne 0) 'invalid CLI accepted'
            Assert-NoSentinel $output 'CLI error and verbose output'
        }
    }
    Run 'unsafe ACL and reparse refusal' {
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $active = [IO.Path]::Combine($root, 'osm.active')
        $acl = [IO.FileSystemAclExtensions]::GetAccessControl([IO.FileInfo]::new($active))
        $acl.SetAccessRuleProtection($false, $true)
        [IO.FileSystemAclExtensions]::SetAccessControl([IO.FileInfo]::new($active), $acl)
        try { Get-CredentialStatus 'osm' | Out-Null; throw 'accepted ACL' } catch {
            Assert-SafeError $_
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
                Assert-SafeError $_
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
                Assert-SafeError $_
                if ($_.Exception.Message -eq 'accepted reparse') { throw }
            }
        } finally { [IO.Directory]::Delete($parent) }
    }
    # Cross-user execution and PTY entry are recorded separately by Phase C when available.
    Assert-NoSentinel ([Environment]::CommandLine) 'process command line'
    Assert-NoSentinel (([Environment]::GetCommandLineArgs()) -join ' ') 'process arguments'
    $repo = [IO.Path]::GetFullPath([IO.Path]::Combine($PSScriptRoot, '..', '..', '..'))
    $diff = & git -C $repo diff --no-ext-diff --no-color HEAD 2>&1 | Out-String
    Assert ($LASTEXITCODE -eq 0) 'repository diff command'
    Assert-NoSentinel $diff 'repository diff'
    $joined = (@($script:passed) + @($script:failed)) -join ','
    Assert (-not $joined.Contains($sentinelId) -and -not $joined.Contains($sentinelSecret)) 'case report secret'
    foreach ($name in $script:passed) { Write-Output "PASS $name" }
    foreach ($name in $script:failed) { Write-Output "FAIL $name" }
    Write-Output "Cases: $($script:passed.Count + $script:failed.Count); passed: $($script:passed.Count); failed: $($script:failed.Count)"
    if ($script:failed.Count -gt 0 -or $script:passed.Count -eq 0) { exit 1 }
} finally {
    Invoke-StorePrivate { $script:TestRoot = $null; $script:Fault = $null; $script:Clock = $null; $script:FakeValidator = $null }
    & $pathModule { $script:WriteLedger = $null }
    if ([IO.Directory]::Exists($root)) { [IO.Directory]::Delete($root, $true) }
}
