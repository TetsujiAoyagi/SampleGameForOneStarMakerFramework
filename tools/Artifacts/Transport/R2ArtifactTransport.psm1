Set-StrictMode -Version Latest

$script:AssemblyPath = Join-Path $PSScriptRoot 'artifacts/transport/R2ArtifactTransport.dll'

 # Build/Contentは検証済みの固定keyへのPUT/GETだけを許す。共通TransportがDELETEを持っていても、
 # Build DELETEは件数清掃（BuildCleanup）が付ける印 BuildRetentionDelete=$true（bool）、
 # Content DELETEは件数清掃（ContentCleanup）が付ける印 ContentRetentionDelete=$true（bool）の
 # requestだけを通す。Contentの印はContentのkeyにだけ効き、印付きでもBuild・Evidence等のkeyのDELETEは拒否する。
 # 印は権限ではなく、削除の所有者をコード上に残す境界の表明（同一ユーザー内の信頼が前提）。
 # `build delete`／`content delete` のgrammarは作らない。
 # 判定は通信・DLLに触れない純粋関数にし、許可表をofflineで検証できるようにする。

function Test-R2ArtifactRequestAllowed([hashtable] $Request) {
    if ($null -eq $Request) { return $false }
    $buildDelete = $Request.ContainsKey('BuildRetentionDelete') -and $Request.BuildRetentionDelete -is [bool] -and $Request.BuildRetentionDelete
    $contentDelete = $Request.ContainsKey('ContentRetentionDelete') -and $Request.ContentRetentionDelete -is [bool] -and $Request.ContentRetentionDelete
    return -not ($Request.Endpoint -cnotmatch '\Ahttps://[0-9a-f]{32}\.r2\.cloudflarestorage\.com\z' -or
        $Request.Operation -cnotin @('put','get','delete') -or
        $Request.Key -cnotmatch '\A(?:(?:probe/(?:locked|unlocked)|evidence/first-use|development/evidence/v2)/[A-Za-z0-9/._-]{1,480}|development/builds/v1/[0-9a-f]{64}/[a-z0-9][a-z0-9-]{0,63}/[a-z0-9][a-z0-9-]{0,63}/[a-z0-9][a-z0-9-]{0,63}/[0-9a-f]{32}/bundle\.zip|development/content/v1/[0-9a-f]{64}/[a-z0-9][a-z0-9-]{0,63}/[a-z0-9][a-z0-9-]{0,63}/[0-9]{8}T[0-9]{9}Z-[0-9a-f]{32}/(?:transport\.json|content/(?:(?!\.{1,2}/)[A-Za-z0-9._-]{1,240}/)*(?!\.{1,2}\z)[A-Za-z0-9._-]{1,240}))\z' -or
        ($Request.Operation -ceq 'delete' -and $Request.Key -cmatch '\Adevelopment/builds/v1/' -and -not $buildDelete) -or
        ($Request.Operation -ceq 'delete' -and -not $contentDelete -and $Request.Key -cmatch '\Adevelopment/content/v1/') -or
        ($Request.Operation -ceq 'delete' -and $contentDelete -and $Request.Key -cnotmatch '\Adevelopment/content/v1/') -or
        ($Request.Operation -ceq 'delete' -and $Request.Key -cmatch '\Aevidence/first-use/.*/bundle\.zip\z' -and -not ($Request.ContainsKey('ResetAuthorized') -and $Request.ResetAuthorized -eq $true)) -or
        $Request.DeadlineMilliseconds -lt 1 -or $Request.DeadlineMilliseconds -gt 120000)
}

function Invoke-R2ArtifactOperation {
    param([string] $AccessKeyId, [string] $SecretAccessKey, [string] $Generation, [hashtable] $Request)
    if (-not [IO.File]::Exists($script:AssemblyPath) -or -not (Test-R2ArtifactRequestAllowed $Request)) {
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

Export-ModuleMember -Function Invoke-R2ArtifactOperation, Test-R2ArtifactRequestAllowed
