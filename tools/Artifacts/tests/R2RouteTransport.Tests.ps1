param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$assemblyPath = Join-Path $PSScriptRoot '../Probe/artifacts/route-transport/R2RouteTransport.dll'
if (-not [IO.File]::Exists($assemblyPath)) { throw 'Build R2RouteTransport before running its transport tests.' }
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($assemblyPath))
$type = $assembly.GetType('OneStarMaker.Artifacts.Probe.RouteTransport', $true)
$readBody = $type.GetMethod('ReadBody', [Reflection.BindingFlags]'NonPublic,Static')
if ($null -eq $readBody) { throw 'ReadBody test target unavailable.' }

function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Read-TestBody([byte[]] $Bytes, [int] $ExpectedBytes, [int] $Limit = 8192) {
    $stream = [IO.MemoryStream]::new($Bytes, $false)
    try { return $readBody.Invoke($null, [object[]]@($stream, $ExpectedBytes, [Threading.CancellationToken]::None, $Limit, $true)) }
    finally { $stream.Dispose() }
}

# <Code>を1024-byte read境界へまたがせても、本文ではなく安全なcode値だけを得ます。
$errorBytes = [Text.Encoding]::UTF8.GetBytes(('x' * 1018) + '<Code> AccessDenied </Code><Message>must not be retained</Message>')
$errorBody = Read-TestBody $errorBytes 16
Assert ($errorBody.S3Code -ceq 'AccessDenied') 'S3 error code was not extracted across read chunks'
Assert ($errorBody.EofConfirmed -and -not $errorBody.LimitReached) 'bounded error body did not reach EOF'
Assert ($errorBody.BodySha256 -ceq [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($errorBytes)).ToLowerInvariant()) 'full response digest changed'
Assert ($null -eq $errorBody.GetType().GetProperty('CapturedBytes')) 'transport retained a response-body byte array'

# 上限到達時はEOFや全体hashを偽らず、期待object長のprefix証拠だけを残します。
$objectPrefix = [Text.Encoding]::ASCII.GetBytes('private-payload')
$oversized = $objectPrefix + ([byte[]]::new(8193))
$limitedBody = Read-TestBody $oversized $objectPrefix.Length
Assert ($limitedBody.LimitReached -and -not $limitedBody.EofConfirmed) 'oversized body was reported as complete'
Assert ($null -eq $limitedBody.BodySha256) 'limited body received a full-body digest'
$expectedPrefixHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($objectPrefix)).ToLowerInvariant()
Assert ($limitedBody.PrefixSha256 -ceq $expectedPrefixHash) 'object prefix digest was lost at the byte limit'

[Console]::WriteLine('R2RouteTransport tests passed: 2')
