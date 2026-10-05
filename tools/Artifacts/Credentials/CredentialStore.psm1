Set-StrictMode -Version Latest
Import-Module (Join-Path $PSScriptRoot 'CredentialPathAcl.psm1') -Force
Import-Module (Join-Path $PSScriptRoot 'CredentialRecord.psm1') -Force

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

function Read-Envelope([hashtable] $Paths, [string] $Profile) {
    Assert-CredentialFile $Paths.Active
    if (-not [IO.File]::Exists($Paths.Active)) { throw 'Credential operation unavailable.' }
    $cipher = [IO.File]::ReadAllBytes($Paths.Active)
    try { return Unprotect-Envelope $cipher $Profile }
    finally { [Array]::Clear($cipher, 0, $cipher.Length) }
}

function Read-Active([hashtable] $Paths, [string] $Profile) {
    # backup/candidate は障害復旧用の残骸であり、active に昇格させない。
    Assert-CredentialFile $Paths.Active
    if (-not [IO.File]::Exists($Paths.Active)) { throw 'Credential operation unavailable.' }
    return (Read-Envelope $Paths $Profile).Active
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
        $oldEnvelope = if ($exists) { Read-Envelope $paths $Profile } else { $null }
        if ($oldEnvelope -and $null -ne $oldEnvelope.Retired) { throw 'Credential operation unavailable.' }
        $old = if ($oldEnvelope) { $oldEnvelope.Active } else { $null }
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
        $newValue = if ($oldEnvelope -and $oldEnvelope.Version -eq 2) {
            [pscustomobject]@{ Version = 2; Active = $record; Retired = $null; TransitionId = $null }
        } else { $record }
        $cipher = Protect-Record $newValue
        $candidate = [IO.Path]::Combine($paths.Root, "$Profile.candidate.$([Guid]::NewGuid().ToString('N'))")
        try {
            Assert-CredentialWrite $paths.Root $candidate
            $stream = [IO.FileStream]::new($candidate, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try { $stream.Write($cipher, 0, $cipher.Length); $stream.Flush($true) } finally { $stream.Dispose() }
            Set-CredentialFileAcl $candidate $paths.Root
            $verified = Unprotect-Envelope ([IO.File]::ReadAllBytes($candidate)) $Profile
            if ($verified.Active.Generation -cne $record.Generation) { throw 'Credential operation unavailable.' }
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
    $envelope = Read-Envelope $paths $Profile
    $record = $envelope.Active
    return [pscustomobject]@{
        Profile = $record.Profile; Bucket = $record.Bucket; Endpoint = $record.Endpoint
        Generation = $record.Generation; CreatedUtc = $record.CreatedUtc; UpdatedUtc = $record.UpdatedUtc
        TokenReference = $record.TokenReference
        State = if ($null -eq $envelope.Retired) { 'stable' } else { 'pending-revocation' }
        RetiredGeneration = if ($null -eq $envelope.Retired) { $null } else { $envelope.Retired.Generation }
        TransitionId = $envelope.TransitionId
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
        if ($present -and $null -ne (Read-Envelope $paths $Profile).Retired) { throw 'Credential operation unavailable.' }
        if ($present) { Assert-CredentialWrite $paths.Root $paths.Active; [IO.File]::Delete($paths.Active) }
        Remove-OwnedFiles $paths $Profile
        return $(if ($present) { 'removed' } else { 'already absent' })
    } finally { $lock.Dispose() }
}

function Test-TransportValueSafe($Value, [string] $AccessKeyId, [string] $SecretAccessKey, [int] $Depth = 0) {
    if ($Depth -gt 4 -or $null -eq $Value) { return ($Depth -le 4) }
    if ($Value -is [string]) {
        return -not $Value.Contains($AccessKeyId, [StringComparison]::Ordinal) -and
            -not $Value.Contains($SecretAccessKey, [StringComparison]::Ordinal)
    }
    if ($Value -is [bool] -or $Value -is [byte] -or $Value -is [sbyte] -or
        $Value -is [short] -or $Value -is [ushort] -or $Value -is [int] -or
        $Value -is [uint] -or $Value -is [long] -or $Value -is [ulong] -or
        $Value -is [decimal] -or $Value -is [double] -or $Value -is [float] -or
        $Value -is [DateTime] -or $Value -is [DateTimeOffset] -or $Value -is [Guid]) {
        return $true
    }
    if ($Value -is [byte[]]) { return $false }
    if ($Value -is [Collections.IDictionary]) {
        $entries = @($Value.GetEnumerator())
        if ($entries.Count -gt 64) { return $false }
        foreach ($entry in $entries) {
            if ($entry.Key -isnot [string] -or $entry.Key -cnotmatch '^[A-Za-z][A-Za-z0-9_]{0,63}$' -or
                $entry.Key -cmatch '(?i)(secret|accesskey|authorization|credential|exception|response|request|token)') {
                return $false
            }
            if (-not (Test-TransportValueSafe $entry.Value $AccessKeyId $SecretAccessKey ($Depth + 1))) { return $false }
        }
        return $true
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = @($Value)
        if ($items.Count -gt 64) { return $false }
        foreach ($item in $items) {
            if (-not (Test-TransportValueSafe $item $AccessKeyId $SecretAccessKey ($Depth + 1))) { return $false }
        }
        return $true
    }
    $properties = @($Value.PSObject.Properties | Where-Object { $_.MemberType -in @('NoteProperty','Property','AliasProperty') })
    if ($properties.Count -eq 0 -or $properties.Count -gt 64) { return $false }
    foreach ($property in $properties) {
        if ($property.Name -cnotmatch '^[A-Za-z][A-Za-z0-9_]{0,63}$' -or
            $property.Name -cmatch '(?i)(secret|accesskey|authorization|credential|exception|response|request|token)') {
            return $false
        }
        if (-not (Test-TransportValueSafe $property.Value $AccessKeyId $SecretAccessKey ($Depth + 1))) { return $false }
    }
    return $true
}

function Invoke-CredentialTransport([string] $Profile, [scriptblock] $Transport, [object] $Request) {
    # R2/HTTPの責務はStoreへ持ち込まない。Storeは秘密を復号して、同一processの
    # 一操作 callbackへだけ渡し、すべてのPowerShell streamを回収してから
    # 固定された非秘密の値だけを呼び出し元へ返す。callbackの例外本文やSDK
    # responseをそのまま通すと、失敗経路で秘密が漏れるため、形式違反も同じ
    # 固定エラーへ畳み込む。
    if ($Profile -cne 'osm' -or $Transport -isnot [scriptblock] -or $null -eq $Request) {
        throw 'Credential operation unavailable.'
    }
    $paths = Get-Paths $Profile $false
    $record = Read-Active $paths $Profile
    return Invoke-RestrictedTransport $record $Transport $Request
}

function Invoke-RestrictedTransport($Record, [scriptblock] $Transport, [object] $Request) {
    if ($null -eq $Record -or $Transport -isnot [scriptblock] -or $null -eq $Request) {
        throw 'Credential operation unavailable.'
    }
    $captured = $null
    $consoleOut = [IO.StringWriter]::new([Globalization.CultureInfo]::InvariantCulture)
    $consoleError = [IO.StringWriter]::new([Globalization.CultureInfo]::InvariantCulture)
    $originalConsoleOut = [Console]::Out
    $originalConsoleError = [Console]::Error
    try {
        # SDKのConsole出力はPowerShell streamのリダイレクト外へ出る。callback
        # 中だけ専用writerへ向け、復元前に空であることも非秘密検査する。
        [Console]::SetOut($consoleOut)
        [Console]::SetError($consoleError)
        try {
            $captured = @(& $Transport $Record.AccessKeyId $Record.SecretAccessKey $Record.Generation $Request *>&1)
        } finally {
            [Console]::SetOut($originalConsoleOut)
            [Console]::SetError($originalConsoleError)
        }
        if ($consoleOut.ToString().Length -ne 0 -or $consoleError.ToString().Length -ne 0) {
            throw 'Credential operation unavailable.'
        }
        if ($captured.Count -ne 1 -or -not (Test-TransportValueSafe $captured[0] $Record.AccessKeyId $Record.SecretAccessKey)) {
            throw 'Credential operation unavailable.'
        }
        return $captured[0]
    } catch {
        throw 'Credential operation unavailable.'
    } finally {
        if ($Record) {
            $Record.AccessKeyId = $null
            $Record.SecretAccessKey = $null
        }
        $Record = $null
        $captured = $null
        $consoleOut.Dispose()
        $consoleError.Dispose()
    }
}

function New-CredentialCandidate([string] $Profile, [string] $AccessKeyId, [string] $SecretAccessKey) {
    $paths = Get-Paths $Profile $true
    $lock = Enter-StoreLock $paths
    try {
        $envelope = Read-Envelope $paths $Profile
        if ($null -ne $envelope.Retired -or $AccessKeyId -cnotmatch '^[\x21-\x7e]{1,256}$' -or
            $SecretAccessKey -cnotmatch '^[\x21-\x7e]{1,256}$') { throw 'Credential operation unavailable.' }
        $now = (Get-Now).ToUniversalTime().ToString('o')
        $record = [pscustomobject]@{
            Version = 1; Profile = $Profile; Bucket = 'osm-artifacts'; Endpoint = $null
            Generation = [Guid]::NewGuid().ToString('N'); CreatedUtc = $now; UpdatedUtc = $now
            TokenReference = $null; AccessKeyId = $AccessKeyId; SecretAccessKey = $SecretAccessKey
        }
        Assert-Record $record $Profile
        $candidate = [IO.Path]::Combine($paths.Root, "$Profile.candidate.$([Guid]::NewGuid().ToString('N'))")
        $cipher = Protect-Record $record
        try {
            Assert-CredentialWrite $paths.Root $candidate
            $stream = [IO.FileStream]::new($candidate, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
            try { $stream.Write($cipher, 0, $cipher.Length); $stream.Flush($true) } finally { $stream.Dispose() }
            Set-CredentialFileAcl $candidate $paths.Root
            $verified = Unprotect-Record ([IO.File]::ReadAllBytes($candidate)) $Profile
            if ($verified.Generation -cne $record.Generation) { throw 'Credential operation unavailable.' }
            return [pscustomobject]@{ Path = $candidate; Generation = $record.Generation; ExpectedGeneration = $envelope.Active.Generation }
        } finally { [Array]::Clear($cipher, 0, $cipher.Length) }
    } finally { $lock.Dispose() }
}

function Read-Candidate([string] $Profile, [string] $Path) {
    $paths = Get-Paths $Profile $false
    if ($Path -cnotmatch ('\A' + [regex]::Escape($paths.Root) + '\\' + [regex]::Escape($Profile) + '\.candidate\.[0-9a-f]{32}\z')) {
        throw 'Credential operation unavailable.'
    }
    Assert-CredentialFile $Path
    if (-not [IO.File]::Exists($Path)) { throw 'Credential operation unavailable.' }
    $cipher = [IO.File]::ReadAllBytes($Path)
    try { return Unprotect-Record $cipher $Profile }
    finally { [Array]::Clear($cipher, 0, $cipher.Length) }
}

function Invoke-CredentialCandidateTransport([string] $Profile, [string] $CandidatePath,
    [scriptblock] $Transport, [object] $Request) {
    $record = Read-Candidate $Profile $CandidatePath
    return Invoke-RestrictedTransport $record $Transport $Request
}

function Invoke-CredentialRetiredTransport([string] $Profile, [string] $TransitionId,
    [scriptblock] $Transport, [object] $Request) {
    $paths = Get-Paths $Profile $false
    $envelope = Read-Envelope $paths $Profile
    if ($null -eq $envelope.Retired -or $envelope.TransitionId -cne $TransitionId) {
        throw 'Credential operation unavailable.'
    }
    return Invoke-RestrictedTransport $envelope.Retired $Transport $Request
}

function Save-CredentialEnvelope([hashtable] $Paths, [string] $Profile, $Envelope) {
    Assert-Envelope $Envelope $Profile
    $candidate = [IO.Path]::Combine($Paths.Root, "$Profile.candidate.$([Guid]::NewGuid().ToString('N'))")
    $backup = [IO.Path]::Combine($Paths.Root, "$Profile.backup.$([Guid]::NewGuid().ToString('N'))")
    $cipher = Protect-Record $Envelope
    $committed = $false
    try {
        Assert-CredentialWrite $Paths.Root $candidate
        $stream = [IO.FileStream]::new($candidate, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $stream.Write($cipher, 0, $cipher.Length); $stream.Flush($true) } finally { $stream.Dispose() }
        Set-CredentialFileAcl $candidate $Paths.Root
        $verified = Unprotect-Envelope ([IO.File]::ReadAllBytes($candidate)) $Profile
        if ($verified.Active.Generation -cne $Envelope.Active.Generation -or
            $verified.TransitionId -cne $Envelope.TransitionId) { throw 'Credential operation unavailable.' }
        Invoke-Fault 'before-commit'
        foreach ($changed in @($candidate, $Paths.Active, $backup)) { Assert-CredentialWrite $Paths.Root $changed }
        [IO.File]::Replace($candidate, $Paths.Active, $backup)
        $committed = $true
        try { Invoke-Fault 'after-commit'; Remove-OwnedFiles $Paths $Profile; return 'success' }
        catch { return 'committed with cleanup pending' }
    } finally {
        [Array]::Clear($cipher, 0, $cipher.Length)
        if (-not $committed -and [IO.File]::Exists($candidate)) {
            try { Assert-CredentialWrite $Paths.Root $candidate; [IO.File]::Delete($candidate) } catch { }
        }
    }
}

function Set-CredentialCandidateActive([string] $Profile, [string] $CandidatePath,
    [string] $ExpectedGeneration, [string] $CandidateGeneration) {
    $paths = Get-Paths $Profile $false
    $lock = Enter-StoreLock $paths
    try {
        $old = Read-Envelope $paths $Profile
        if ($null -ne $old.Retired -or $old.Active.Generation -cne $ExpectedGeneration) {
            throw 'Credential operation unavailable.'
        }
        $candidate = Read-Candidate $Profile $CandidatePath
        if ($candidate.Generation -cne $CandidateGeneration) { throw 'Credential operation unavailable.' }
        $envelope = [pscustomobject]@{ Version = 2; Active = $candidate; Retired = $old.Active
            TransitionId = [Guid]::NewGuid().ToString('N') }
        $outcome = Save-CredentialEnvelope $paths $Profile $envelope
        return [pscustomobject]@{ Outcome = $outcome; TransitionId = $envelope.TransitionId
            ActiveGeneration = $candidate.Generation; RetiredGeneration = $old.Active.Generation }
    } finally { $lock.Dispose() }
}

function Confirm-CredentialRetired([string] $Profile, [string] $TransitionId,
    [string] $ActiveGeneration, [string] $RetiredGeneration) {
    $paths = Get-Paths $Profile $false
    $lock = Enter-StoreLock $paths
    try {
        $old = Read-Envelope $paths $Profile
        if ($null -eq $old.Retired -or $old.TransitionId -cne $TransitionId -or
            $old.Active.Generation -cne $ActiveGeneration -or $old.Retired.Generation -cne $RetiredGeneration) {
            throw 'Credential operation unavailable.'
        }
        $stable = [pscustomobject]@{ Version = 2; Active = $old.Active; Retired = $null; TransitionId = $null }
        return Save-CredentialEnvelope $paths $Profile $stable
    } finally { $lock.Dispose() }
}

Export-ModuleMember -Function Write-CredentialRecord, Get-CredentialStatus, Remove-CredentialRecord, Invoke-CredentialTransport,
    New-CredentialCandidate, Invoke-CredentialCandidateTransport, Invoke-CredentialRetiredTransport,
    Set-CredentialCandidateActive, Confirm-CredentialRetired
