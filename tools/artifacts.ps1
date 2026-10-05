param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    if ($Arguments.Count -lt 4) { throw 'Invalid command.' }
    if ($Arguments[0] -ceq 'credentials' -and $Arguments[1] -cin @('set','status','remove')) {
        # Existing local credential grammar and exit meanings remain unchanged.
        if ($Arguments[2] -cne '--profile' -or $Arguments[3] -cne 'osm') { throw 'Invalid command.' }
        $action = $Arguments[1]
        $replace = $false
        if ($action -eq 'set') {
            if ($Arguments.Count -eq 5 -and $Arguments[4] -ceq '--replace') { $replace = $true }
            elseif ($Arguments.Count -ne 4) { throw 'Invalid command.' }
        } elseif ($Arguments.Count -ne 4) { throw 'Invalid command.' }
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/Credentials/CredentialCommands.psm1') -Force
        switch ($action) {
            'set' {
                $outcome = Invoke-CredentialSet 'osm' $replace
                if ($outcome.Outcome -eq 'committed with cleanup pending') {
                    [Console]::Error.WriteLine('Committed locally; cleanup pending. R2 connectivity unverified.')
                    exit 2
                }
                [Console]::WriteLine('Credential committed locally; R2 connectivity unverified.')
            }
            'status' { Invoke-CredentialStatus 'osm' | ForEach-Object { [Console]::WriteLine($_) } }
            'remove' { [Console]::WriteLine((Invoke-CredentialRemove 'osm')) }
        }
        exit 0
    }
    $command = $Arguments[0]
    $action = $Arguments[1]
    if ($command -ceq 'publish' -and $Arguments.Count -eq 11 -and
        $Arguments[1] -ceq '--profile' -and $Arguments[2] -ceq 'osm' -and
        $Arguments[3] -ceq '--config' -and $Arguments[5] -ceq '--input-list' -and
        $Arguments[7] -ceq '--base' -and $Arguments[9] -ceq '--head') {
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/ArtifactApplication.psm1') -Force
        $outcome = Invoke-ArtifactPublish $Arguments[4] $Arguments[6] $Arguments[8] $Arguments[10]
    } elseif ($command -ceq 'fetch' -and $Arguments.Count -eq 9 -and
        $Arguments[1] -ceq '--profile' -and $Arguments[2] -ceq 'osm' -and
        $Arguments[3] -ceq '--config' -and $Arguments[5] -ceq '--reference' -and
        $Arguments[7] -ceq '--sha256') {
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/ArtifactApplication.psm1') -Force
        $outcome = Invoke-ArtifactFetch $Arguments[4] $Arguments[6] $Arguments[8]
    } elseif ($command -ceq 'credentials' -and $action -ceq 'rotate' -and $Arguments.Count -eq 6 -and
        $Arguments[2] -ceq '--profile' -and $Arguments[3] -ceq 'osm' -and $Arguments[4] -ceq '--config') {
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/Credentials/CredentialRotation.psm1') -Force
        $outcome = Invoke-CredentialRotation $Arguments[5]
    } elseif ($command -ceq 'credentials' -and $action -ceq 'confirm-revocation' -and $Arguments.Count -eq 10 -and
        $Arguments[2] -ceq '--profile' -and $Arguments[3] -ceq 'osm' -and $Arguments[4] -ceq '--config' -and
        $Arguments[6] -ceq '--generation' -and $Arguments[8] -ceq '--evidence') {
        Import-Module (Join-Path $PSScriptRoot 'Artifacts/Credentials/CredentialRotation.psm1') -Force
        $outcome = Invoke-CredentialRevocationConfirmation $Arguments[5] $Arguments[7] $Arguments[9]
    } else { throw 'Invalid command.' }
    [Console]::WriteLine((ConvertTo-Json -InputObject $outcome -Compress -Depth 20))
    if ($outcome.status -ceq 'passed') { exit 0 }
    if ($outcome.status -ceq 'pending') { exit 2 }
    exit 1
} catch {
    # 例外文字列・stack trace には秘密やパスが混ざり得るため、一般診断だけ表示する。
    [Console]::Error.WriteLine('Credential operation failed.')
    exit 1
}
