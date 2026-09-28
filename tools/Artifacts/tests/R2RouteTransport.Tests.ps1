param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$assemblyPath = Join-Path $PSScriptRoot '../Probe/artifacts/route-transport/R2RouteTransport.dll'
if (-not [IO.File]::Exists($assemblyPath)) { throw 'Build R2RouteTransport before running its transport tests.' }
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($assemblyPath))
$type = $assembly.GetType('OneStarMaker.Artifacts.Probe.RouteTransport', $true)
$readBody = $type.GetMethod('ReadBody', [Reflection.BindingFlags]'NonPublic,Static')
if ($null -eq $readBody) { throw 'ReadBody test target unavailable.' }
$requestFactory = $type.GetMethod('CreateUnsignedRequest', [Reflection.BindingFlags]'NonPublic,Static')
$handlerFactory = $type.GetMethod('CreateUnsignedHandler', [Reflection.BindingFlags]'NonPublic,Static')
if ($null -eq $requestFactory -or $null -eq $handlerFactory) { throw 'Unsigned request test target unavailable.' }
$redirectHandlerType = $assembly.GetType('OneStarMaker.Artifacts.Probe.RouteTransport+RedirectRejectingHandler', $true)
$redirectClassifier = $type.GetMethod('IsRedirectRejection', [Reflection.BindingFlags]'NonPublic,Static')
$clientFactoryMethod = $type.GetMethod('CreateClient', [Reflection.BindingFlags]'NonPublic,Static')
if ($null -eq $redirectClassifier -or $null -eq $clientFactoryMethod) { throw 'Authenticated redirect guard test target unavailable.' }

function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }

# 本番のfactoryを直接呼び、fake observationの値だけでは隠れてしまうrequest生成契約を検査します。
$endpoint = 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'
$key = 'probe/unlocked/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/object.txt'
$request = $requestFactory.Invoke($null, [object[]]@($endpoint, $key))
$handler = $handlerFactory.Invoke($null, [object[]]@())
try {
    Assert ($request.Method -eq [Net.Http.HttpMethod]::Get) 'unsigned request method was not GET'
    Assert ($request.RequestUri.AbsoluteUri -ceq "$endpoint/osm-artifacts/$key") 'unsigned request URI was not the exact bucket/key path'
    Assert ($request.RequestUri.Query.Length -eq 0) 'unsigned request added a query string'
    Assert ($null -eq $request.Headers.Authorization) 'unsigned request added Authorization'
    Assert (-not $handler.AllowAutoRedirect) 'unsigned handler follows redirects'
} finally { $request.Dispose(); $handler.Dispose() }

# SDKのS3 redirect pipelineへ3xxを渡す前に止めるproduction handlerをfake応答で検査します。
$sdkClient = $clientFactoryMethod.Invoke($null, [object[]]@('dummy-access-key', 'dummy-secret-key', 'https://0123456789abcdef0123456789abcdef.r2.cloudflarestorage.com'))
try {
    Assert (-not $sdkClient.Config.AllowAutoRedirect) 'authenticated SDK config enables platform redirects'
    Assert ($sdkClient.Config.MaxErrorRetry -eq 0) 'authenticated SDK config may retry a blocked redirect'
    Assert ($sdkClient.Config.HttpClientFactory.GetType().Name -eq 'RedirectRejectingHttpClientFactory') 'authenticated SDK is not wired to the redirect guard'
} finally { $sdkClient.Dispose() }
$redirectFixtureSource = @'
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
public sealed class RouteProofRedirectFixture : HttpMessageHandler {
    public int RequestCount { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        RequestCount++;
        var response = new HttpResponseMessage(HttpStatusCode.TemporaryRedirect);
        response.Headers.Location = new System.Uri("https://different-host.invalid/redirected-object");
        return Task.FromResult(response);
    }
}
'@
Add-Type -TypeDefinition $redirectFixtureSource -ErrorAction Stop
$redirectHandler = [Activator]::CreateInstance($redirectHandlerType, $true)
$redirectFixture = [RouteProofRedirectFixture]::new()
$redirectHandler.InnerHandler = $redirectFixture
$redirectClient = [Net.Http.HttpClient]::new($redirectHandler)
$redirectRequest = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, 'https://original-host.invalid/object')
try {
    $rejected = $false
    try { [void]$redirectClient.SendAsync($redirectRequest).GetAwaiter().GetResult() }
    catch { $rejected = [bool]$redirectClassifier.Invoke($null, [object[]]@($_.Exception)) }
    Assert $rejected 'authenticated HTTP handler passed a 307 to the AWS SDK redirect pipeline'
    Assert ($redirectFixture.RequestCount -eq 1) 'authenticated redirect guard issued more than the original request'
} finally { $redirectRequest.Dispose(); $redirectClient.Dispose() }

function Read-TestBody([byte[]] $Bytes, [int] $ExpectedBytes, [int] $Limit = 8192) {
    $stream = [IO.MemoryStream]::new($Bytes, $false)
    try { return $readBody.Invoke($null, [object[]]@($stream, $ExpectedBytes, [Threading.CancellationToken]::None, $Limit, $true)) }
    finally { $stream.Dispose() }
}
function Read-CancelledTestBody([byte[]] $PrefixBytes, [int] $ExpectedBytes) {
    $streamType = @'
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
public sealed class PrefixThenCancelStream : Stream {
    private readonly byte[] _prefix; private readonly CancellationTokenSource _source; private bool _sent;
    public PrefixThenCancelStream(byte[] prefix, CancellationTokenSource source) { _prefix=prefix; _source=source; }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken=default) {
        if (!_sent) { _sent=true; _prefix.CopyTo(buffer); return ValueTask.FromResult(_prefix.Length); }
        _source.Cancel(); return ValueTask.FromCanceled<int>(_source.Token);
    }
    public override bool CanRead=>true; public override bool CanSeek=>false; public override bool CanWrite=>false;
    public override long Length=>throw new NotSupportedException(); public override long Position { get=>throw new NotSupportedException(); set=>throw new NotSupportedException(); }
    public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    public override void Flush()=>throw new NotSupportedException(); public override long Seek(long o,SeekOrigin so)=>throw new NotSupportedException();
    public override void SetLength(long v)=>throw new NotSupportedException(); public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
}
'@
    Add-Type -TypeDefinition $streamType -ErrorAction Stop
    $source = [Threading.CancellationTokenSource]::new()
    $stream = [PrefixThenCancelStream]::new($PrefixBytes, $source)
    try { return $readBody.Invoke($null, [object[]]@($stream, $ExpectedBytes, $source.Token, 8192, $false)) }
    finally { $source.Dispose() }
}
function Read-InterruptedTestBody([byte[]] $PrefixBytes, [int] $ExpectedBytes) {
    $streamType = @'
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
public sealed class PrefixThenIOExceptionStream : Stream {
    private readonly byte[] _prefix; private bool _sent;
    public PrefixThenIOExceptionStream(byte[] prefix) { _prefix=prefix; }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken=default) {
        if (!_sent) { _sent=true; _prefix.AsMemory().CopyTo(buffer); return ValueTask.FromResult(_prefix.Length); }
        return ValueTask.FromException<int>(new IOException("test interruption"));
    }
    public override bool CanRead=>true; public override bool CanSeek=>false; public override bool CanWrite=>false;
    public override long Length=>throw new NotSupportedException(); public override long Position { get=>throw new NotSupportedException(); set=>throw new NotSupportedException(); }
    public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    public override void Flush()=>throw new NotSupportedException(); public override long Seek(long o,SeekOrigin so)=>throw new NotSupportedException();
    public override void SetLength(long v)=>throw new NotSupportedException(); public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
}
'@
    Add-Type -TypeDefinition $streamType -ErrorAction Stop
    $stream=[PrefixThenIOExceptionStream]::new($PrefixBytes)
    return $readBody.Invoke($null, [object[]]@($stream,$ExpectedBytes,[Threading.CancellationToken]::None,8192,$false))
}

# <Code>を1024-byte read境界へまたがせても、本文ではなく安全なcode値だけを得ます。
$errorBytes = [Text.Encoding]::UTF8.GetBytes(('x' * 1018) + '<Code> AccessDenied </Code><Message>must not be retained</Message>')
$errorBody = Read-TestBody $errorBytes 16
Assert ($errorBody.S3Code -ceq 'AccessDenied') 'S3 error code was not extracted across read chunks'
Assert ($errorBody.EofConfirmed -and -not $errorBody.LimitReached) 'bounded error body did not reach EOF'
Assert ($errorBody.BodySha256 -ceq [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($errorBytes)).ToLowerInvariant()) 'full response digest changed'
Assert ($null -eq $errorBody.GetType().GetProperty('CapturedBytes')) 'transport retained a response-body byte array'

# read後に計上値だけを切り詰めず、上限+1 byteより先をstreamから消費しないことを実量で検査します。
$countingStreamSource = @'
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
public sealed class RouteProofCountingReadStream : Stream {
    private readonly byte[] _bytes; private int _position;
    public int TotalBytesRead { get; private set; }
    public int LargestRequestedBuffer { get; private set; }
    public int LastRequestedBuffer { get; private set; }
    public RouteProofCountingReadStream(byte[] bytes) { _bytes=bytes; }
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken=default) {
        LargestRequestedBuffer=Math.Max(LargestRequestedBuffer,buffer.Length);
        LastRequestedBuffer=buffer.Length;
        var count=Math.Min(buffer.Length,_bytes.Length-_position);
        _bytes.AsMemory(_position,count).CopyTo(buffer); _position+=count; TotalBytesRead+=count;
        return ValueTask.FromResult(count);
    }
    public override bool CanRead=>true; public override bool CanSeek=>false; public override bool CanWrite=>false;
    public override long Length=>throw new NotSupportedException(); public override long Position { get=>_position; set=>throw new NotSupportedException(); }
    public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    public override void Flush()=>throw new NotSupportedException(); public override long Seek(long o,SeekOrigin so)=>throw new NotSupportedException();
    public override void SetLength(long v)=>throw new NotSupportedException(); public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
}
'@
Add-Type -TypeDefinition $countingStreamSource -ErrorAction Stop
$countingStream=[RouteProofCountingReadStream]::new([byte[]]::new(20000))
$boundedRead=$readBody.Invoke($null,[object[]]@($countingStream,32,[Threading.CancellationToken]::None,8192,$false))
Assert ($boundedRead.ByteCount -eq 8193 -and $countingStream.TotalBytesRead -eq 8193) 'body cap reported less data than the stream actually consumed'
Assert ($boundedRead.LimitReached -and $countingStream.LargestRequestedBuffer -le 1024 -and $countingStream.LastRequestedBuffer -eq 1) 'body cap requested bytes beyond the single overflow-detection byte'

# 上限到達時はEOFや全体hashを偽らず、期待object長のprefix証拠だけを残します。
$objectPrefix = [Text.Encoding]::ASCII.GetBytes('private-payload')
$oversized = $objectPrefix + ([byte[]]::new(8193))
$limitedBody = Read-TestBody $oversized $objectPrefix.Length
Assert ($limitedBody.LimitReached -and -not $limitedBody.EofConfirmed) 'oversized body was reported as complete'
Assert ($null -eq $limitedBody.BodySha256) 'limited body received a full-body digest'
$expectedPrefixHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($objectPrefix)).ToLowerInvariant()
Assert ($limitedBody.PrefixSha256 -ceq $expectedPrefixHash) 'object prefix digest was lost at the byte limit'

# 遅延を使わず2回目のreadで期限を発火し、既に届いたprefix hashが残ることを確認します。
$cancelledBody = Read-CancelledTestBody $objectPrefix $objectPrefix.Length
Assert ($cancelledBody.TimedOut -and -not $cancelledBody.EofConfirmed) 'cancelled body was not classified as a partial timeout'
Assert ($cancelledBody.PrefixSha256 -ceq $expectedPrefixHash) 'timeout discarded the already observed object prefix'
Assert ($null -eq $cancelledBody.BodySha256) 'partial timeout received a full-body digest'

# 通信切断は期限timeoutとは別でも、到着済みprefixの証拠を失わせません。
$interruptedBody=Read-InterruptedTestBody $objectPrefix $objectPrefix.Length
Assert (-not $interruptedBody.TimedOut -and -not $interruptedBody.EofConfirmed) 'I/O interruption was reported as timeout or complete'
Assert ($interruptedBody.PrefixSha256 -ceq $expectedPrefixHash) 'I/O interruption discarded the already observed prefix'
Assert ($null -eq $interruptedBody.BodySha256) 'interrupted body received a full-body digest'

[Console]::WriteLine('R2RouteTransport tests passed: 5')
