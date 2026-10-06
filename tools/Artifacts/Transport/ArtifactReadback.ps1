param([Parameter(Mandatory = $true)][string] $RequestPath)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

try {
    Import-Module (Join-Path $PSScriptRoot '../ArtifactCommands.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../ArtifactPaths.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot '../Credentials/CredentialStore.psm1') -Force
    Import-Module (Join-Path $PSScriptRoot 'R2ArtifactTransport.psm1') -Force
    $request = Read-ArtifactJson $RequestPath
    $data = $request.Data
    Assert-Fields $data @('schemaVersion','operationRoot','endpoint','generation','key','expectedBytes',
        'expectedSha256','destination','deadlineMilliseconds')
    if ($data.schemaVersion -isnot [long] -or $data.schemaVersion -ne 1 -or
        $data.operationRoot -isnot [string] -or $data.endpoint -isnot [string] -or
        $data.endpoint -cnotmatch '\Ahttps://[0-9a-f]{32}\.r2\.cloudflarestorage\.com\z' -or
        $data.key -isnot [string] -or $data.key -cnotmatch '\A(?:probe/locked|evidence/first-use)/[0-9a-f]{64}/[0-9a-f]{40}/[0-9a-f]{32}/(?:bundle\.zip|protection-witness\.txt)\z' -or
        $data.generation -isnot [string] -or $data.expectedSha256 -isnot [string] -or
        $data.expectedBytes -isnot [int] -and $data.expectedBytes -isnot [long] -or
        $data.expectedBytes -lt 1 -or $data.expectedBytes -gt 256MB -or
        $data.destination -isnot [string] -or
        $data.deadlineMilliseconds -isnot [long] -or $data.deadlineMilliseconds -lt 1 -or $data.deadlineMilliseconds -gt 120000) {
        throw 'Readback request unavailable.'
    }
    Assert-Hex $data.generation 32
    Assert-Hex $data.expectedSha256 64
    Assert-ArtifactOperationFile $data.operationRoot $RequestPath
    if (-not (Test-ArtifactWithin $data.destination $data.operationRoot) -or
        [IO.File]::Exists($data.destination) -or [IO.Directory]::Exists($data.destination)) {
        throw 'Readback destination unavailable.'
    }
    $generation = $data.generation
    if ((Get-CredentialStatus 'osm').Generation -cne $generation) { throw 'Credential generation changed.' }
    $details = @{ Endpoint = $data.endpoint; Operation = 'get'; Key = $data.key; SourcePath = $null
        DestinationPath = $data.destination; DeadlineMilliseconds = $data.deadlineMilliseconds }
    $transportInvoker = (Get-Command Invoke-R2ArtifactOperation -ErrorAction Stop).ScriptBlock
    $callback = {
        param($id, $secret, $actualGeneration, $requestValue)
        if ($actualGeneration -cne $generation) { throw 'Credential generation changed.' }
        & $transportInvoker $id $secret $actualGeneration $requestValue
    }.GetNewClosure()
    $result = Invoke-CredentialTransport 'osm' $callback $details
    if ($result.Operation -cne 'get' -or $result.Generation -cne $generation) { throw 'Readback verification failed.' }
    $observedBytes = if ($null -ne $result.Bytes -and $result.Bytes -is [long] -and $result.Bytes -ge 0 -and $result.Bytes -le 256MB) { $result.Bytes } else { $null }
    $observedHash = if ($null -ne $result.Sha256 -and $result.Sha256 -is [string] -and $result.Sha256 -cmatch '\A[0-9a-f]{64}\z') { $result.Sha256 } else { $null }
    $passed = $result.HttpStatus -eq 200 -and $result.StatusClass -ceq 'success' -and
        $result.StatusObserved -and -not $result.TimedOut -and -not $result.Redirected -and
        $observedBytes -eq $data.expectedBytes -and $observedHash -ceq $data.expectedSha256
    $receipt = [ordered]@{ schemaVersion = 1; operation = 'get'; generation = $generation
        key = $data.key; bytes = $observedBytes; sha256 = $observedHash
        httpStatus = $result.HttpStatus; statusClass = $result.StatusClass; s3Code = $result.S3Code
        statusObserved = $result.StatusObserved; timedOut = $result.TimedOut; redirected = $result.Redirected
        status = if ($passed) { 'passed' } else { 'failed' } }
    [Console]::WriteLine((ConvertTo-Json -InputObject $receipt -Compress -Depth 4))
    if ($passed) { exit 0 } else { exit 1 }
} catch {
    # The parent rejects any nonzero child result; no exception body, SDK response or secret leaves here.
    exit 1
}
