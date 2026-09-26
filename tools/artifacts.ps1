param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Arguments)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    if ($Arguments.Count -lt 4 -or $Arguments[0] -cne 'credentials' -or
        $Arguments[1] -cnotin @('set','status','remove') -or
        $Arguments[2] -cne '--profile' -or $Arguments[3] -cne 'osm') {
        throw 'Invalid command.'
    }
    $action = $Arguments[1]
    $replace = $false
    if ($action -eq 'set') {
        if ($Arguments.Count -eq 5 -and $Arguments[4] -ceq '--replace') { $replace = $true }
        elseif ($Arguments.Count -ne 4) { throw 'Invalid command.' }
    } elseif ($Arguments.Count -ne 4) { throw 'Invalid command.' }

    Import-Module (Join-Path $PSScriptRoot 'Artifacts/Credentials/CredentialCommands.psm1') -Force
    switch ($action) {
        'set' {
            $result = Invoke-CredentialSet 'osm' $replace
            if ($result.Outcome -eq 'committed with cleanup pending') {
                [Console]::Error.WriteLine('Committed locally; cleanup pending. R2 connectivity unverified.')
                exit 2
            }
            [Console]::WriteLine('Credential committed locally; R2 connectivity unverified.')
        }
        'status' { Invoke-CredentialStatus 'osm' | ForEach-Object { [Console]::WriteLine($_) } }
        'remove' { [Console]::WriteLine((Invoke-CredentialRemove 'osm')) }
    }
    exit 0
} catch {
    # Never include exception messages or stack traces: caller and system exceptions can contain secrets or paths.
    [Console]::Error.WriteLine('Credential operation failed.')
    exit 1
}
