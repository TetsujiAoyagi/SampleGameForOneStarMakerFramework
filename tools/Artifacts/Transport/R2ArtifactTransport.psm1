Set-StrictMode -Version Latest

$script:AssemblyPath = Join-Path $PSScriptRoot 'artifacts/transport/R2ArtifactTransport.dll'

 # Buildは検証済みの固定keyへのPUT/GETだけを許す。共通TransportがDELETEを持っていても、
 # Buildの保持判定・利用中確認・削除直前排他は未接続なので、Build DELETEは入口で拒否する。

function Invoke-R2ArtifactOperation {
    param([string] $AccessKeyId, [string] $SecretAccessKey, [string] $Generation, [hashtable] $Request)
    if (-not [IO.File]::Exists($script:AssemblyPath) -or
        $Request.Endpoint -cnotmatch '\Ahttps://[0-9a-f]{32}\.r2\.cloudflarestorage\.com\z' -or
        $Request.Operation -cnotin @('put','get','delete') -or
        $Request.Key -cnotmatch '\A(?:(?:probe/(?:locked|unlocked)|evidence/first-use|development/evidence/v2)/[A-Za-z0-9/._-]{1,480}|development/builds/v1/[0-9a-f]{64}/[a-z0-9][a-z0-9-]{0,63}/[a-z0-9][a-z0-9-]{0,63}/[a-z0-9][a-z0-9-]{0,63}/[0-9a-f]{32}/bundle\.zip)\z' -or
        ($Request.Operation -ceq 'delete' -and $Request.Key -cmatch '\Adevelopment/builds/v1/') -or
        ($Request.Operation -ceq 'delete' -and $Request.Key -cmatch '\Aevidence/first-use/.*/bundle\.zip\z' -and -not ($Request.ContainsKey('ResetAuthorized') -and $Request.ResetAuthorized -eq $true)) -or
        $Request.DeadlineMilliseconds -lt 1 -or $Request.DeadlineMilliseconds -gt 120000) {
        throw 'Artifact transport unavailable.'
    }
    try {
        $assembly = [Reflection.Assembly]::LoadFrom($script:AssemblyPath)
        $type = $assembly.GetType('OneStarMaker.Artifacts.Transport.R2ArtifactTransport', $true)
        $observation = if($Request.ContainsKey('ResetAuthorized') -and $Request.ResetAuthorized -eq $true){
            $type::ExecuteReset($AccessKeyId,$SecretAccessKey,$Request.Endpoint,$Request.Operation,$Request.Key,$Request.DestinationPath,[int]$Request.DeadlineMilliseconds)
        }else{
        $type::Execute($AccessKeyId, $SecretAccessKey, $Request.Endpoint,
            $Request.Operation, $Request.Key, $Request.SourcePath, $Request.DestinationPath,
            [int]$Request.DeadlineMilliseconds)
        }
        return [pscustomobject]@{
            Operation = [string]$observation.Operation
            HttpStatus = if ($null -eq $observation.HttpStatus) { $null } else { [int]$observation.HttpStatus }
            StatusClass = [string]$observation.StatusClass
            S3Code = if ($null -eq $observation.S3Code) { $null } else { [string]$observation.S3Code }
            Bytes = if ($null -eq $observation.Bytes) { $null } else { [long]$observation.Bytes }
            Sha256 = if ($null -eq $observation.Sha256) { $null } else { [string]$observation.Sha256 }
            ETag = if ($null -eq $observation.ETag) { $null } else { [string]$observation.ETag }
            StatusObserved = [bool]$observation.StatusObserved
            TimedOut = [bool]$observation.TimedOut
            Redirected = [bool]$observation.Redirected
            Generation = $Generation
        }
    } catch { throw 'Artifact transport unavailable.' }
}

Export-ModuleMember -Function Invoke-R2ArtifactOperation
