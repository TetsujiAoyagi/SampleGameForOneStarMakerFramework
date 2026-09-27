param(
    [Parameter(Mandatory = $true)][string] $Endpoint,
    [ValidateSet('roundtrip', 'read', 'empty')][string] $Mode = 'roundtrip',
    [string] $Key,
    [string] $ExpectedHash,
    [int] $ExpectedBytes
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# A3前の実経路確認専用。実鍵・SDK例外・HTTP応答は出力せず、使い捨てのprobe keyだけを扱う。
$phase = 'input'
$cleanup = 'not-needed'
$putAttempted = $false
$success = $false
$bytes = $null
$context = $null
$hash = $null

function Invoke-ProbeOperation([string] $Verb, [hashtable] $Context) {
    $store = Import-Module (Join-Path $PSScriptRoot '../Credentials/CredentialStore.psm1') -PassThru -Force
    $callback = {
        param([string] $AccessKeyId, [string] $SecretAccessKey, [hashtable] $Request)
        $credentials = [Amazon.Runtime.BasicAWSCredentials]::new($AccessKeyId, $SecretAccessKey)
        $config = [Amazon.S3.AmazonS3Config]::new()
        $config.ServiceURL = $Request.Endpoint
        $config.AuthenticationRegion = 'auto'
        $config.ForcePathStyle = $true
        $config.Timeout = [TimeSpan]::FromSeconds(25)
        $client = [Amazon.S3.AmazonS3Client]::new($credentials, $config)
        try {
            switch ($Request.Verb) {
                'put' {
                    $stream = [IO.MemoryStream]::new($Request.Bytes, $false)
                    try {
                        $put = [Amazon.S3.Model.PutObjectRequest]::new()
                        $put.BucketName = 'osm-artifacts'
                        $put.Key = $Request.Key
                        $put.InputStream = $stream
                        $put.DisablePayloadSigning = $true
                        $put.DisableDefaultChecksumValidation = $true
                        $response = $client.PutObjectAsync($put).GetAwaiter().GetResult()
                        return $response.HttpStatusCode -eq [Net.HttpStatusCode]::OK
                    } finally { $stream.Dispose() }
                }
                'read' {
                    $get = [Amazon.S3.Model.GetObjectRequest]::new()
                    $get.BucketName = 'osm-artifacts'
                    $get.Key = $Request.Key
                    $response = $client.GetObjectAsync($get).GetAwaiter().GetResult()
                    try {
                        if ($response.HttpStatusCode -ne [Net.HttpStatusCode]::OK) { return $false }
                        $memory = [IO.MemoryStream]::new()
                        try {
                            $buffer = [byte[]]::new(4096)
                            while (($count = $response.ResponseStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                                if ($memory.Length + $count -gt 1024) { return $false }
                                $memory.Write($buffer, 0, $count)
                            }
                            $bytes = $memory.ToArray()
                            try {
                                $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
                                return $bytes.Length -eq $Request.ExpectedBytes -and $hash -ceq $Request.ExpectedHash -and
                                    [Text.Encoding]::ASCII.GetString($bytes).StartsWith('OSM-PREA3-PROBE:', [StringComparison]::Ordinal)
                            } finally { [Array]::Clear($bytes, 0, $bytes.Length) }
                        } finally { $memory.Dispose() }
                    } finally { $response.Dispose() }
                }
                'delete' {
                    $delete = [Amazon.S3.Model.DeleteObjectRequest]::new()
                    $delete.BucketName = 'osm-artifacts'
                    $delete.Key = $Request.Key
                    $response = $client.DeleteObjectAsync($delete).GetAwaiter().GetResult()
                    return $response.HttpStatusCode -eq [Net.HttpStatusCode]::NoContent
                }
                'empty' {
                    $list = [Amazon.S3.Model.ListObjectsV2Request]::new()
                    $list.BucketName = 'osm-artifacts'
                    $list.Prefix = 'probe/prea3/'
                    $list.MaxKeys = 1
                    $response = $client.ListObjectsV2Async($list).GetAwaiter().GetResult()
                    return $response.HttpStatusCode -eq [Net.HttpStatusCode]::OK -and $response.S3Objects.Count -eq 0
                }
                default { throw 'Invalid probe operation.' }
            }
        } finally { $client.Dispose() }
    }
    $request = @{ Verb = $Verb; Endpoint = $Context.Endpoint; Key = $Context.Key;
        Bytes = $Context.Bytes; ExpectedHash = $Context.ExpectedHash; ExpectedBytes = $Context.ExpectedBytes }
    # 復号値は同一processの同期callback内だけに渡す。出力はbool一つに制限する。
    return & $store {
        param([scriptblock] $Operation, [hashtable] $Argument)
        $paths = Get-Paths 'osm' $false
        $record = Read-Active $paths 'osm'
        try {
            $result = @(& $Operation $record.AccessKeyId $record.SecretAccessKey $Argument)
            if ($result.Count -ne 1 -or $result[0] -isnot [bool]) { throw 'Credential operation unavailable.' }
            return [bool]$result[0]
        } finally {
            $record.AccessKeyId = $null
            $record.SecretAccessKey = $null
            $record = $null
        }
    } $callback $request
}

try {
    if ($Endpoint -cnotmatch '^https://[0-9a-f]{32}\.r2\.cloudflarestorage\.com/?$') { throw 'Invalid endpoint.' }
    $Endpoint = $Endpoint.TrimEnd('/')
    $sdk = Join-Path $PSScriptRoot 'artifacts/sdk'
    $phase = 'sdk'
    foreach ($name in @('AWSSDK.Core.dll', 'AWSSDK.S3.dll')) {
        $dll = Join-Path $sdk $name
        if (-not [IO.File]::Exists($dll)) { throw 'SDK unavailable.' }
        Add-Type -Path $dll
    }

    if ($Mode -eq 'empty') {
        $phase = 'list'
        $emptyContext = @{ Endpoint = $Endpoint; Key = $null; Bytes = $null;
            ExpectedHash = $null; ExpectedBytes = 0 }
        $isEmpty = Invoke-ProbeOperation 'empty' $emptyContext
        [Console]::WriteLine("probe/prea3 empty: $($isEmpty.ToString().ToLowerInvariant())")
        exit 0
    }

    if ($Mode -eq 'read') {
        $phase = 'input'
        if ($Key -cnotmatch '^probe/prea3/[0-9a-f]{32}\.txt$' -or
            $ExpectedHash -cnotmatch '^[0-9a-f]{64}$' -or $ExpectedBytes -lt 1 -or $ExpectedBytes -gt 1024) {
            throw 'Invalid read request.'
        }
        $phase = 'read'
        $context = @{ Endpoint = $Endpoint; Key = $Key; Bytes = $null;
            ExpectedHash = $ExpectedHash; ExpectedBytes = $ExpectedBytes }
        if (-not (Invoke-ProbeOperation 'read' $context)) { throw 'Read mismatch.' }
        [Console]::WriteLine('read: pass')
        exit 0
    }

    $id = [Guid]::NewGuid().ToString('N')
    $Key = "probe/prea3/$id.txt"
    $bytes = [Text.Encoding]::ASCII.GetBytes("OSM-PREA3-PROBE:$id")
    $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    $context = @{ Endpoint = $Endpoint; Key = $Key; Bytes = $bytes;
        ExpectedHash = $hash; ExpectedBytes = $bytes.Length }

    $phase = 'put'
    $putAttempted = $true
    if (-not (Invoke-ProbeOperation 'put' $context)) { throw 'Upload failed.' }
    $phase = 'separate-read'
    $child = @(& pwsh -NoProfile -File $PSCommandPath -Endpoint $Endpoint -Mode read -Key $Key `
        -ExpectedHash $hash -ExpectedBytes $bytes.Length 2>&1)
    if ($LASTEXITCODE -ne 0 -or $child.Count -ne 1 -or $child[0] -cne 'read: pass') { throw 'Separate read failed.' }
    $success = $true
} catch {
    # 例外本文には鍵やSDKのrequest情報が含まれ得る。固定の段階名以外を出さない。
} finally {
    if ($putAttempted) {
        try {
            if (Invoke-ProbeOperation 'delete' $context) { $cleanup = 'removed' }
            else { $cleanup = 'unconfirmed' }
        } catch { $cleanup = 'unconfirmed' }
    }
    if ($bytes) { [Array]::Clear($bytes, 0, $bytes.Length) }
}

if ($success -and $cleanup -eq 'removed') {
    [Console]::WriteLine("roundtrip: pass; key=$Key; sha256=$hash; bytes=$($context.ExpectedBytes); cleanup=$cleanup")
    exit 0
}
[Console]::Error.WriteLine("roundtrip: failed at $phase; key=$Key; cleanup=$cleanup")
exit 1
