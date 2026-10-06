using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;

namespace OneStarMaker.Artifacts.Transport;

public sealed class ArtifactObservation
{
    public string Operation { get; init; } = "";
    public int? HttpStatus { get; init; }
    public string StatusClass { get; init; } = "unknown";
    public string? S3Code { get; init; }
    public long? Bytes { get; init; }
    public string? Sha256 { get; init; }
    public bool StatusObserved { get; init; }
    public bool TimedOut { get; init; }
    public bool Redirected { get; init; }
}

public static class R2ArtifactTransport
{
    private static readonly Regex Endpoint = new("^https://[0-9a-f]{32}\\.r2\\.cloudflarestorage\\.com$", RegexOptions.CultureInvariant);
    private static readonly Regex SafeCode = new("^[A-Za-z][A-Za-z0-9]{0,63}$", RegexOptions.CultureInvariant);
    private const long TransferLimit = 256L * 1024 * 1024;

    public static ArtifactObservation Execute(string accessKeyId, string secretAccessKey, string endpoint,
        string operation, string key, string? sourcePath, string? destinationPath, int deadlineMilliseconds)
    {
        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey) ||
            !Endpoint.IsMatch(endpoint) || !SafeKey(key) || deadlineMilliseconds is < 1 or > 120000 ||
            operation is not ("put" or "get" or "delete") ||
            (operation == "delete" && key.StartsWith("evidence/first-use/", StringComparison.Ordinal) && key.EndsWith("/bundle.zip", StringComparison.Ordinal)))
            return Fail(operation, "invalid-input");
        using var cancellation = new CancellationTokenSource(deadlineMilliseconds);
        try
        {
            using var client = CreateClient(accessKeyId, secretAccessKey, endpoint);
            return operation switch
            {
                "put" => Put(client, key, sourcePath!, cancellation.Token),
                "get" => Get(client, key, destinationPath!, cancellation.Token),
                _ => Delete(client, key, cancellation.Token)
            };
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return new ArtifactObservation { Operation = operation, StatusClass = "timeout", TimedOut = true }; }
        catch (Exception error) when (IsRedirect(error)) { return new ArtifactObservation { Operation = operation, StatusClass = "redirect", Redirected = true }; }
        catch (AmazonS3Exception error) { return FromS3(operation, error); }
        catch { return Fail(operation, "transport-error"); }
    }

    private static ArtifactObservation Put(AmazonS3Client client, string key, string sourcePath, CancellationToken token)
    {
        if (string.IsNullOrEmpty(sourcePath)) return Fail("put", "invalid-input");
        using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is < 1 or > TransferLimit) return Fail("put", "invalid-input");
        var request = new PutObjectRequest
        {
            BucketName = "osm-artifacts", Key = key, InputStream = stream,
            DisablePayloadSigning = true, DisableDefaultChecksumValidation = true
        };
        var response = client.PutObjectAsync(request, token).GetAwaiter().GetResult();
        return Success("put", response.HttpStatusCode);
    }

    private static ArtifactObservation Get(AmazonS3Client client, string key, string destinationPath, CancellationToken token)
    {
        if (string.IsNullOrEmpty(destinationPath) || File.Exists(destinationPath)) return Fail("get", "invalid-input");
        using var response = client.GetObjectAsync(new GetObjectRequest { BucketName = "osm-artifacts", Key = key }, token).GetAwaiter().GetResult();
        using var source = response.ResponseStream;
        using var destination = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long count = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var size = source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, TransferLimit + 1 - count)), token).AsTask().GetAwaiter().GetResult();
            if (size == 0) break;
            count += size;
            if (count > TransferLimit) return Fail("get", "size-limit");
            destination.Write(buffer, 0, size);
            hash.AppendData(buffer, 0, size);
        }
        destination.Flush(true);
        return new ArtifactObservation { Operation = "get", HttpStatus = (int)response.HttpStatusCode,
            StatusClass = Classify(response.HttpStatusCode), Bytes = count,
            Sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), StatusObserved = true };
    }

    private static ArtifactObservation Delete(AmazonS3Client client, string key, CancellationToken token)
    {
        var response = client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = "osm-artifacts", Key = key }, token).GetAwaiter().GetResult();
        return Success("delete", response.HttpStatusCode);
    }

    private static ArtifactObservation Success(string operation, HttpStatusCode status) => new()
    {
        Operation = operation, HttpStatus = (int)status, StatusClass = Classify(status), StatusObserved = true
    };

    private static ArtifactObservation FromS3(string operation, AmazonS3Exception error)
    {
        // A finite S3 status/code tuple was observed. This does not claim error-body EOF.
        var code = error.ErrorCode;
        return new ArtifactObservation { Operation = operation, HttpStatus = (int)error.StatusCode,
            StatusClass = Classify(error.StatusCode), S3Code = code is not null && SafeCode.IsMatch(code) ? code : null,
            StatusObserved = true };
    }

    private static string Classify(HttpStatusCode status) => (int)status switch
    {
        >= 200 and <= 299 => "success", 401 => "unauthorized", 403 => "forbidden", 404 => "not-found",
        >= 300 and <= 399 => "redirect", _ => "other"
    };
    private static ArtifactObservation Fail(string operation, string status) => new() { Operation = operation, StatusClass = status };
    private static bool SafeKey(string value) =>
        value.Length is > 0 and <= 512 && value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or >= 'A' and <= 'Z' or '/' or '.' or '_' or '-') &&
        !value.Contains("//", StringComparison.Ordinal) && !value.Contains("..", StringComparison.Ordinal);

    private static AmazonS3Client CreateClient(string accessKeyId, string secretAccessKey, string endpoint)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint, AuthenticationRegion = "auto", ForcePathStyle = true,
            AllowAutoRedirect = false, HttpClientFactory = new RejectRedirectFactory(), MaxErrorRetry = 0,
            Timeout = Timeout.InfiniteTimeSpan, LogResponse = false, LogMetrics = false
        };
        return new AmazonS3Client(new Amazon.Runtime.BasicAWSCredentials(accessKeyId, secretAccessKey), config);
    }

    private static bool IsRedirect(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is RedirectRejectedException) return true;
        return false;
    }

    private sealed class RejectRedirectFactory : Amazon.Runtime.HttpClientFactory
    {
        public override HttpClient CreateHttpClient(Amazon.Runtime.IClientConfig config) =>
            new(new RejectRedirectHandler()) { Timeout = Timeout.InfiniteTimeSpan };
    }
    private sealed class RejectRedirectHandler : DelegatingHandler
    {
        public RejectRedirectHandler() : base(new HttpClientHandler { AllowAutoRedirect = false }) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            var response = await base.SendAsync(request, cancellation).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and <= 399)
            {
                response.Dispose();
                throw new RedirectRejectedException();
            }
            return response;
        }
    }
    private sealed class RedirectRejectedException : HttpRequestException { }
}
