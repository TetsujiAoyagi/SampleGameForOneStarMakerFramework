#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace OneStarMaker.Runtime.BuildContent.Distribution
{
    public interface IContentArtifactSource : IDisposable { Task<Stream> OpenReadAsync(string relativePath,CancellationToken cancellationToken); }

    public sealed class LocalContentArtifactSource : IContentArtifactSource
    {
        private readonly string _root;
        public LocalContentArtifactSource(string root)=>_root=Path.GetFullPath(root??throw new ArgumentNullException(nameof(root)));
        public Task<Stream> OpenReadAsync(string relativePath,CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); try { return Task.FromResult<Stream>(new FileStream(ContentDeliveryFiles.Resolve(_root,relativePath),FileMode.Open,FileAccess.Read,FileShare.Read)); }
          catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){ throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure,"Local artifact could not be opened.",ex); } }
        public void Dispose() { }
    }

    public sealed class HttpContentArtifactSource : IContentArtifactSource
    {
        private readonly Uri _base; private readonly HttpClient _client;
        public HttpContentArtifactSource(Uri baseUri)
        { _base=baseUri??throw new ArgumentNullException(nameof(baseUri)); if(!_base.IsAbsoluteUri||(_base.Scheme!="http"&&_base.Scheme!="https")) throw new ArgumentException("HTTP base URI is invalid.",nameof(baseUri));
          _client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(60)}; }
        public async Task<Stream> OpenReadAsync(string relativePath,CancellationToken cancellationToken)
        {
            if(!ContentManifestValidation.Relative(relativePath)) throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure,"Artifact path is invalid.");
            var uri=new Uri(_base,string.Join("/",relativePath.Split('/').Select(Uri.EscapeDataString)));
            if(uri.Scheme!=_base.Scheme||uri.Host!=_base.Host||uri.Port!=_base.Port) throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure,"Artifact URI escapes its base.");
            try { var response=await _client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
              if(response.StatusCode< HttpStatusCode.OK || response.StatusCode>=HttpStatusCode.MultipleChoices){ response.Dispose(); throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure,"Artifact request failed: "+(int)response.StatusCode); }
              return new ResponseStream(await response.Content.ReadAsStreamAsync(),response); }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested){ throw; }
            catch(ContentDeliveryException){ throw; }
            catch(Exception ex){ throw new ContentDeliveryException(ContentDeliveryFailureCode.TransportFailure,"Artifact request failed.",ex); }
        }
        public void Dispose()=>_client.Dispose();
        private sealed class ResponseStream:Stream
        { private readonly Stream _inner; private HttpResponseMessage? _response; internal ResponseStream(Stream inner,HttpResponseMessage response){_inner=inner;_response=response;}
          protected override void Dispose(bool disposing){if(disposing){_inner.Dispose();_response?.Dispose();_response=null;}base.Dispose(disposing);} public override bool CanRead=>_inner.CanRead; public override bool CanSeek=>_inner.CanSeek; public override bool CanWrite=>false; public override long Length=>_inner.Length; public override long Position{get=>_inner.Position;set=>_inner.Position=value;} public override void Flush()=>_inner.Flush(); public override int Read(byte[] buffer,int offset,int count)=>_inner.Read(buffer,offset,count); public override long Seek(long offset,SeekOrigin origin)=>_inner.Seek(offset,origin); public override void SetLength(long value)=>throw new NotSupportedException(); public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException(); }
    }
}
