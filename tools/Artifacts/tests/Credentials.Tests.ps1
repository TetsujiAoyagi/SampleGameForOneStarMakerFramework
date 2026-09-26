param([string[]] $Case = @('*'))
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$commands = Import-Module (Join-Path $PSScriptRoot '../Credentials/CredentialCommands.psm1') -Force -PassThru
$storePath = Join-Path $PSScriptRoot '../Credentials/CredentialStore.psm1'
# Commands が読み込んだ Store の同じインスタンスを公開し、private fixture と公開関数を一致させる。
$store = Import-Module $storePath -PassThru
$pathModule = $store.NestedModules | Where-Object Name -eq 'CredentialPathAcl' | Select-Object -First 1
$root = [IO.Path]::Combine([IO.Path]::GetTempPath(), 'osm-credential-test-' + [Guid]::NewGuid().ToString('N'))
$script:passed = [Collections.Generic.List[string]]::new()
$script:failed = [Collections.Generic.List[string]]::new()
$script:writes = [Collections.Generic.List[string]]::new()
$sentinelId = 'DUMMY_ID_' + [Guid]::NewGuid().ToString('N')
$sentinelSecret = 'DUMMY_SECRET_' + [Guid]::NewGuid().ToString('N')

# 実鍵を要求せず毎回別のダミー値を生成する。失敗時も値や例外本文を出力しない。

function Invoke-StorePrivate([scriptblock] $Code, [object[]] $Values = @()) {
    return & $store $Code @Values
}
function Invoke-CommandsPrivate([scriptblock] $Code, [object[]] $Values = @()) {
    return & $commands $Code @Values
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
    # 各 case の fixture を消す前に、UTF-8/UTF-16 の平文痕跡を全生成物で走査する。
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
    Invoke-StorePrivate { param($value) $script:TestRoot = $value; $script:TestBase = $null; $script:Fault = $null; $script:Clock = $null; $script:FakeValidator = $null } @($root)
    if ([IO.Directory]::Exists($root)) { [IO.Directory]::Delete($root, $true) }
    $script:writes.Clear()
    & $pathModule { param($ledger) $script:WriteLedger = $ledger } $script:writes
}
function Run([string] $Name, [scriptblock] $Body) {
    # 書き込み台帳は Store/PathAcl の private hook。case ごとに初期化し、
    # 実ファイルと stream/例外の双方を消去前に検査する。
    $selected = $false
    foreach ($pattern in $Case) {
        if ($Name -like $pattern) { $selected = $true; break }
    }
    if (-not $selected) { return }
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
            if ($Name -notin @('CLI rejects redirected input', 'CLI verbose and error output is safe', 'noninteractive invocation is refused', 'ancestor reparse is refused', 'unsafe dedicated directory is refused')) {
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
    Run 'fixed three-level root initializes from missing directories' {
        [IO.Directory]::CreateDirectory($root) | Out-Null
        $first = [IO.Path]::Combine($root, 'OneStarMaker')
        $second = [IO.Path]::Combine($first, 'Artifacts')
        $final = [IO.Path]::Combine($second, 'credentials')
        Assert (-not [IO.Directory]::Exists($first)) 'fixture already initialized'
        Invoke-StorePrivate { param($base, $target) $script:TestBase = $base; $script:TestRoot = $target } @($root, $final)
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        Assert ([IO.File]::Exists([IO.Path]::Combine($final, 'osm.active'))) 'active outside fixed descendant'
        Assert ((Get-CredentialStatus 'osm').Bucket -eq 'osm-artifacts') 'round trip under fixed root'
        $allowed = @($first, $second, $final)
        foreach ($path in $allowed) {
            Assert ([IO.Directory]::Exists($path)) 'missing support directory'
            Assert ($script:writes.Contains($path)) 'directory initialization absent from ledger'
        }
        foreach ($write in $script:writes) {
            Assert (($write -in $allowed) -or $write.StartsWith($final + '\', [StringComparison]::OrdinalIgnoreCase)) 'write outside fixed initialization hierarchy'
        }
    }
    Run 'existing shared parent and sibling remain unchanged' {
        [IO.Directory]::CreateDirectory($root) | Out-Null
        $first = [IO.Path]::Combine($root, 'OneStarMaker')
        $sibling = [IO.Path]::Combine($first, 'RevisionLocks')
        [IO.Directory]::CreateDirectory($sibling) | Out-Null
        $parentAclBefore = [IO.FileSystemAclExtensions]::GetAccessControl([IO.DirectoryInfo]::new($first)).GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::All)
        $siblingAclBefore = [IO.FileSystemAclExtensions]::GetAccessControl([IO.DirectoryInfo]::new($sibling)).GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::All)
        $final = [IO.Path]::Combine($first, 'Artifacts', 'credentials')
        Invoke-StorePrivate { param($base, $target) $script:TestBase = $base; $script:TestRoot = $target } @($root, $final)
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        Assert ([IO.File]::Exists([IO.Path]::Combine($final, 'osm.active'))) 'active absent'
        $parentAclAfter = [IO.FileSystemAclExtensions]::GetAccessControl([IO.DirectoryInfo]::new($first)).GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::All)
        $siblingAclAfter = [IO.FileSystemAclExtensions]::GetAccessControl([IO.DirectoryInfo]::new($sibling)).GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::All)
        Assert ($parentAclAfter -ceq $parentAclBefore) 'shared parent ACL changed'
        Assert ($siblingAclAfter -ceq $siblingAclBefore) 'sibling ACL changed'
        Assert ([IO.Directory]::Exists($sibling)) 'sibling deleted'
        Assert (-not $script:writes.Contains($first) -and -not $script:writes.Contains($sibling)) 'shared path entered write ledger'
    }
    Run 'unsafe dedicated directory is refused' {
        $first = [IO.Path]::Combine($root, 'OneStarMaker')
        $second = [IO.Path]::Combine($first, 'Artifacts')
        [IO.Directory]::CreateDirectory($second) | Out-Null
        $final = [IO.Path]::Combine($second, 'credentials')
        Invoke-StorePrivate { param($base, $target) $script:TestBase = $base; $script:TestRoot = $target } @($root, $final)
        try { Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null; throw 'accepted unsafe directory' } catch {
            Assert-SafeError $_
            if ($_.Exception.Message -eq 'accepted unsafe directory') { throw }
        }
        Assert (-not [IO.Directory]::Exists($final)) 'credential directory created under unsafe parent'
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
    Run 'decryptable malformed records refuse reads and replacement' {
        # DPAPI で復号可能でも schema が壊れた active は拒否し、交換前の byte を守る。
        Write-CredentialRecord 'osm' $sentinelId $sentinelSecret $false | Out-Null
        $active = [IO.Path]::Combine($root, 'osm.active')
        $now = [DateTimeOffset]::UtcNow.ToString('o')
        foreach ($variant in @('invalid-created-date','invalid-updated-date','created-date-array','access-key-array','extra-field','duplicate-field')) {
            $record = [pscustomobject]@{
                Version = 1; Profile = 'osm'; Bucket = 'osm-artifacts'; Endpoint = $null
                Generation = [Guid]::NewGuid().ToString('N'); CreatedUtc = $now; UpdatedUtc = $now
                TokenReference = $null; AccessKeyId = $sentinelId; SecretAccessKey = $sentinelSecret
            }
            switch ($variant) {
                'invalid-created-date' { $record.CreatedUtc = '2026-99-99TINVALID' }
                'invalid-updated-date' { $record.UpdatedUtc = '2026-99-99TINVALID' }
                'created-date-array' { $record.CreatedUtc = @($now, $now) }
                'access-key-array' { $record.AccessKeyId = @($sentinelId, $sentinelId) }
                'extra-field' { $record | Add-Member -NotePropertyName Unexpected -NotePropertyValue 'x' }
            }
            $json = $record | ConvertTo-Json -Compress -Depth 3
            if ($variant -eq 'duplicate-field') { $json = $json.TrimEnd('}') + ',"Version":1}' }
            $plain = [Text.Encoding]::UTF8.GetBytes($json)
            try { $cipher = [Security.Cryptography.ProtectedData]::Protect($plain, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser) }
            finally { [Array]::Clear($plain, 0, $plain.Length) }
            [IO.File]::WriteAllBytes($active, $cipher)
            $before = [IO.File]::ReadAllBytes($active)
            try { Get-CredentialStatus 'osm' | Out-Null; throw 'accepted malformed status' } catch {
                Assert-SafeError $_
                if ($_.Exception.Message -ne 'Credential operation unavailable.') { throw }
            }
            try { Write-CredentialRecord 'osm' ($sentinelId + 'R') ($sentinelSecret + 'R') $true | Out-Null; throw 'accepted malformed replacement' } catch {
                Assert-SafeError $_
                if ($_.Exception.Message -ne 'Credential operation unavailable.') { throw }
            }
            Assert ([Convert]::ToHexString($before) -ceq [Convert]::ToHexString([IO.File]::ReadAllBytes($active))) 'malformed active changed'
            [Array]::Clear($cipher, 0, $cipher.Length)
            [Array]::Clear($before, 0, $before.Length)
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
    Run 'noninteractive invocation is refused' {
        # 公式ホスト解析の有限な接頭辞集合と全略語長を網羅する。実ホスト起動との
        # 照合は別途行い、この回帰ではCLIの固定文法による安全側拒否も保持する。
        $dashes = @('-', [string][char]0x2013, [string][char]0x2014, [string][char]0x2015)
        $prefixes = @('/') + $dashes + @($dashes | ForEach-Object { $_ + $_ })
        foreach ($prefix in $prefixes) {
            foreach ($length in 4..14) {
                foreach ($spelling in @('noninteractive', 'NONINTERACTIVE', 'NoNiNtErAcTiVe')) {
                    $switch = $prefix + $spelling.Substring(0, $length)
                    Assert (Invoke-CommandsPrivate { param($value) Test-NonInteractiveInvocation @('pwsh.dll', $value) } @($switch)) 'host spelling missed'
                }
            }
            # Trim() が受理する全BMP空白を、先頭と末尾それぞれで確認する。
            foreach ($code in 0..0xffff) {
                if (-not [char]::IsWhiteSpace([char]$code)) { continue }
                foreach ($switch in @(([string][char]$code + $prefix + 'noni'), ($prefix + 'noni' + [char]$code))) {
                    Assert (Invoke-CommandsPrivate { param($value) Test-NonInteractiveInvocation @($value) } @($switch)) 'trimmed spelling missed'
                }
            }
            foreach ($name in @('', 'n', 'no', 'non', 'noninteractivex', 'noni:true', 'noni=false', 'non interactive')) {
                Assert (-not (Invoke-CommandsPrivate { param($value) Test-NonInteractiveInvocation @($value) } @($prefix + $name))) 'invalid name matched'
            }
        }
        # 2個目を除けるのは同じdashだけ。slash重複、混在、3個以上は一致しない。
        foreach ($first in (@('/') + $dashes)) {
            foreach ($second in (@('/') + $dashes)) {
                $expected = $first -ne '/' -and $first -ceq $second
                $actual = Invoke-CommandsPrivate { param($value) Test-NonInteractiveInvocation @($value) } @($first + $second + 'noni')
                Assert ($actual -eq $expected) 'duplicate prefix boundary'
            }
            Assert (-not (Invoke-CommandsPrivate { param($value) Test-NonInteractiveInvocation @($value) } @($first + $first + $first + 'noni'))) 'triple prefix matched'
        }
        foreach ($arguments in @(
            @('pwsh.dll', '-NoProfile', '-NonInteractive', '-File', 'artifacts.ps1'),
            @('pwsh.dll', '-NONI', '-NoProfile', '-File', 'artifacts.ps1'),
            @('pwsh.dll', '/noni', '-NoProfile', '-fi', 'artifacts.ps1'),
            @('pwsh.dll', '-ex', 'Bypass', '-NonInter', '-f', 'artifacts.ps1'),
            @('pwsh.dll', (([string][char]0x2013) + 'NonInteractive'), '-File', 'artifacts.ps1'),
            @('pwsh.dll', (([string][char]0x2014) + 'noni'), '-File', 'artifacts.ps1'),
            @('pwsh.dll', (([string][char]0x2015) + 'NONI'), '-File', 'artifacts.ps1'),
            @('pwsh.dll', '-c', '& ./artifacts.ps1', '-noni')
        )) {
            Assert (Invoke-CommandsPrivate { param($argv) Test-NonInteractiveInvocation $argv } @(,$arguments)) 'noninteractive invocation accepted'
        }
        foreach ($arguments in @(
            @('pwsh.dll', '-c', 'Write-Output -NonInteractive'),
            @('pwsh.dll', '-File', 'artifacts.ps1', 'noni'),
            @('pwsh.dll', '-NoProfile', '-File', 'artifacts.ps1')
        )) {
            Assert (-not (Invoke-CommandsPrivate { param($argv) Test-NonInteractiveInvocation $argv } @(,$arguments))) 'unrelated argument refused'
        }
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
    # 別 Windows ユーザーと実 PTY の観察は Phase C の独立記録に委ねる。
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
    Invoke-StorePrivate { $script:TestRoot = $null; $script:TestBase = $null; $script:Fault = $null; $script:Clock = $null; $script:FakeValidator = $null }
    & $pathModule { $script:WriteLedger = $null }
    if ([IO.Directory]::Exists($root)) { [IO.Directory]::Delete($root, $true) }
}
