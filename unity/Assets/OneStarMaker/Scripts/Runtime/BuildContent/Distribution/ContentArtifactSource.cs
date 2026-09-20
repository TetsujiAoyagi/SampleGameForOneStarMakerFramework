#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    public interface IContentArtifactSource : IDisposable
    {
        Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken);
    }

    public sealed class LocalContentArtifactSource : IContentArtifactSource
    {
        private readonly string _root;
        public LocalContentArtifactSource(string root) => _root = Path.GetFullPath(root ?? throw new ArgumentNullException(nameof(root)));

        public Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return Task.FromResult<Stream>(new FileStream(ContentDeliveryFiles.Resolve(_root, relativePath),
                    FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure, "Local artifact could not be opened.", ex);
            }
        }

        public void Dispose() { }
    }

    public sealed class HttpContentArtifactSource : IContentArtifactSource
    {
        private readonly Uri _base;
        private readonly HttpClient _client;

        public HttpContentArtifactSource(Uri baseUri)
        {
            if (baseUri == null) throw new ArgumentNullException(nameof(baseUri));
            if (!baseUri.IsAbsoluteUri || (baseUri.Scheme != "http" && baseUri.Scheme != "https"))
                throw new ArgumentException("HTTP base URI is invalid.", nameof(baseUri));
            // 入力は file URL でなく成果物 directory。末尾 slash の有無で親 directory に変わらない。
            _base = new UriBuilder(baseUri) { Path = baseUri.AbsolutePath.TrimEnd('/') + "/", Query = "", Fragment = "" }.Uri;
            _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        }

        public async Task<Stream> OpenReadAsync(string relativePath, CancellationToken cancellationToken)
        {
            if (!ContentManifestValidation.Relative(relativePath))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure, "Artifact path is invalid.");
            var uri = new Uri(_base, string.Join("/", relativePath.Split('/').Select(Uri.EscapeDataString)));
            if (uri.Scheme != _base.Scheme || uri.Host != _base.Host || uri.Port != _base.Port
                || !uri.AbsolutePath.StartsWith(_base.AbsolutePath, StringComparison.Ordinal))
                throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure, "Artifact URI escapes its base.");

            // ResponseHeadersRead 後は HttpClient.Timeout の保護外。request 開始時の deadline を
            // response stream に譲渡し、最後の body read/dispose まで同じ60秒を維持する。
            var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(60));
            HttpResponseMessage? response = null;
            try
            {
                response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
                if (response.StatusCode < HttpStatusCode.OK || response.StatusCode >= HttpStatusCode.MultipleChoices)
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure,
                        "Artifact request failed: " + (int)response.StatusCode);
                var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return new ResponseStream(stream, response, deadline, cancellationToken);
            }
            catch (Exception ex) when (cancellationToken.IsCancellationRequested)
            {
                response?.Dispose();
                deadline.Dispose();
                throw new OperationCanceledException("Artifact request was canceled.", ex, cancellationToken);
            }
            catch (Exception ex)
            {
                response?.Dispose();
                deadline.Dispose();
                if (ex is ContentDeliveryException) throw;
                throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure, "Artifact request failed.", ex);
            }
        }

        public void Dispose() => _client.Dispose();

        private sealed class ResponseStream : Stream
        {
            private readonly Stream _inner;
            private readonly HttpResponseMessage _response;
            private readonly CancellationTokenSource _deadline;
            private readonly CancellationToken _requestCancellation;
            private readonly CancellationTokenRegistration _abort;
            private bool _disposed;

            internal ResponseStream(Stream inner, HttpResponseMessage response, CancellationTokenSource deadline,
                CancellationToken requestCancellation)
            {
                _inner = inner;
                _response = response;
                _deadline = deadline;
                _requestCancellation = requestCancellation;
                // transport 実装が ReadAsync token を観測しない場合も接続を閉じて read を解除する。
                _abort = deadline.Token.Register(response.Dispose);
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token);
                using var abortRead = cancellationToken.Register(_response.Dispose);
                try
                {
                    linked.Token.ThrowIfCancellationRequested();
                    return await _inner.ReadAsync(buffer, offset, count, linked.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException
                    or WebException or HttpRequestException)
                {
                    // Unity Mono の中断した HTTP stream は WebException を返す場合がある。
                    // OS/handler の例外型で caller cancellation の公開契約を変えない。
                    if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                    if (_requestCancellation.IsCancellationRequested) throw new OperationCanceledException(_requestCancellation);
                    throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure,
                        _deadline.IsCancellationRequested ? "HTTP response timed out." : "HTTP response body failed.", ex);
                }
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !_disposed)
                {
                    _disposed = true;
                    _abort.Dispose();
                    _deadline.Dispose();
                    _inner.Dispose();
                    _response.Dispose();
                }
                base.Dispose(disposing);
            }

            public override bool CanRead => !_disposed && _inner.CanRead;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count)
                => ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
