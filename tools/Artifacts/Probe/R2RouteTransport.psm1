Set-StrictMode -Version Latest

$script:TransportAssembly = Join-Path $PSScriptRoot 'artifacts/route-transport/R2RouteTransport.dll'

function Import-RouteTransportAssembly {
    if (-not [IO.File]::Exists($script:TransportAssembly)) { throw 'Probe transport unavailable.' }
    try {
        $assembly = [Reflection.Assembly]::LoadFrom($script:TransportAssembly)
        if ($null -eq $assembly.GetType('OneStarMaker.Artifacts.Probe.RouteTransport', $true)) {
            throw 'Probe transport unavailable.'
        }
    } catch { throw 'Probe transport unavailable.' }
}

function Invoke-R2RouteTransport {
    param(
        [Parameter(Mandatory = $true)][string] $AccessKeyId,
        [Parameter(Mandatory = $true)][string] $SecretAccessKey,
        [Parameter(Mandatory = $true)][string] $Generation,
        [Parameter(Mandatory = $true)][hashtable] $Request
    )
    Import-RouteTransportAssembly
    if ($Request.Endpoint -cnotmatch '^https://[0-9a-f]{32}\.r2\.cloudflarestorage\.com$' -or
        $Request.Key -cnotmatch '^probe/(unlocked|locked)/[0-9a-f]{32}/[A-Za-z0-9._-]{1,64}$' -or
        $Request.Operation -notin @('put','authenticated-get','unsigned-get','delete') -or
        $Request.ExpectedBytes -lt 1 -or $Request.ExpectedBytes -gt 1024 -or
        $Request.DeadlineMilliseconds -lt 1 -or $Request.DeadlineMilliseconds -gt 30000) {
        throw 'Probe transport unavailable.'
    }
    try {
        $result = [OneStarMaker.Artifacts.Probe.RouteTransport]::Execute(
            $AccessKeyId, $SecretAccessKey, $Request.Endpoint, $Request.Operation,
            $Request.Key, $Request.Payload, [int]$Request.ExpectedBytes, [int]$Request.DeadlineMilliseconds)
        if ($null -eq $result -or $result.Operation -cne $Request.Operation) { throw 'Probe transport unavailable.' }
        return [pscustomobject]@{
            Operation = [string]$result.Operation
            HttpStatus = if ($null -eq $result.HttpStatus) { $null } else { [int]$result.HttpStatus }
            StatusClass = [string]$result.StatusClass
            S3Code = if ($null -eq $result.S3Code) { $null } else { [string]$result.S3Code }
            ByteCount = if ($null -eq $result.ByteCount) { $null } else { [int]$result.ByteCount }
            BodySha256 = if ($null -eq $result.BodySha256) { $null } else { [string]$result.BodySha256 }
            PrefixSha256 = if ($null -eq $result.PrefixSha256) { $null } else { [string]$result.PrefixSha256 }
            EofConfirmed = [bool]$result.EofConfirmed
            LimitReached = [bool]$result.LimitReached
            TimedOut = [bool]$result.TimedOut
            Redirected = [bool]$result.Redirected
            Generation = $Generation
            RequestMethod = if ($null -eq $result.RequestMethod) { $null } else { [string]$result.RequestMethod }
            RequestUri = if ($null -eq $result.RequestUri) { $null } else { [string]$result.RequestUri }
            AuthorizationPresent = [bool]$result.AuthorizationPresent
            SignatureQueryPresent = [bool]$result.SignatureQueryPresent
            TargetChangingQueryPresent = [bool]$result.TargetChangingQueryPresent
        }
    } catch { throw 'Probe transport unavailable.' }
}

Export-ModuleMember -Function Invoke-R2RouteTransport
