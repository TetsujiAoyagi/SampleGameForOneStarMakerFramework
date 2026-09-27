#nullable enable

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;

namespace OneStarMaker.Artifacts.Probe;

public sealed class TransportObservation
{
    public string Operation { get; init; } = string.Empty;
    public int? HttpStatus { get; init; }
    public string StatusClass { get; init; } = "transport-error";
    public string? S3Code { get; init; }
    public int? ByteCount { get; init; }
    public string? BodySha256 { get; init; }
    public string? PrefixSha256 { get; init; }
    public bool EofConfirmed { get; init; }
    public bool LimitReached { get; init; }
    public bool TimedOut { get; init; }
    public bool Redirected { get; init; }
    public string? RequestMethod { get; init; }
    public string? RequestUri { get; init; }
    public bool AuthorizationPresent { get; init; }
    public bool SignatureQueryPresent { get; init; }
    public bool TargetChangingQueryPresent { get; init; }
}

public static class RouteTransport
{
    private const int UnsignedReadLimit = 8192;
    private static readonly Regex SafeErrorCode = new("^[A-Za-z][A-Za-z0-9]{0,63}$", RegexOptions.CultureInvariant);

    public static TransportObservation Execute(
        string accessKeyId,
        string secretAccessKey,
        string endpoint,
        string operation,
        string key,
        byte[]? payload,
        int expectedBytes,
        int deadlineMilliseconds)
    {
        if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(secretAccessKey) ||
            string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(operation) ||
            string.IsNullOrEmpty(key) || expectedBytes is < 1 or > 1024 ||
            deadlineMilliseconds is < 1 or > 30000)
        {
            return Failure(operation, "invalid-input", false);
        }

        using var cancellation = new CancellationTokenSource(deadlineMilliseconds);
        try
        {
            return operation switch
            {
                "put" => Put(accessKeyId, secretAccessKey, endpoint, key, payload ?? Array.Empty<byte>(), cancellation.Token),
                "authenticated-get" => AuthenticatedGet(accessKeyId, secretAccessKey, endpoint, key, expectedBytes, cancellation.Token),
                "unsigned-get" => UnsignedGet(endpoint, key, expectedBytes, cancellation.Token),
                "delete" => Delete(accessKeyId, secretAccessKey, endpoint, key, cancellation.Token),
                _ => Failure(operation, "invalid-input", false)
            };
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return Failure(operation, "timeout", true);
        }
        catch
        {
            // SDK/HTTP exception messages can contain request material. The caller only
            // needs a closed transport classification and never the exception text.
            return Failure(operation, "transport-error", false);
        }
    }

    private static TransportObservation Put(
        string accessKeyId,
        string secretAccessKey,
        string endpoint,
        string key,
        byte[] payload,
        CancellationToken cancellation)
    {
        using var client = CreateClient(accessKeyId, secretAccessKey, endpoint);
        using var stream = new MemoryStream(payload, writable: false);
        var request = new PutObjectRequest
        {
            BucketName = "osm-artifacts",
            Key = key,
            InputStream = stream,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true
        };
        try
        {
            var response = client.PutObjectAsync(request, cancellation).GetAwaiter().GetResult();
            return new TransportObservation
            {
                Operation = "put",
                HttpStatus = (int)response.HttpStatusCode,
                StatusClass = ClassifyStatus((int)response.HttpStatusCode),
                EofConfirmed = true
            };
        }
        catch (AmazonS3Exception exception)
        {
            return FromS3Exception("put", exception);
        }
    }

    private static TransportObservation AuthenticatedGet(
        string accessKeyId,
        string secretAccessKey,
        string endpoint,
        string key,
        int expectedBytes,
        CancellationToken cancellation)
    {
        using var client = CreateClient(accessKeyId, secretAccessKey, endpoint);
        var request = new GetObjectRequest { BucketName = "osm-artifacts", Key = key };
        try
        {
            using var response = client.GetObjectAsync(request, cancellation).GetAwaiter().GetResult();
            var body = ReadBody(response.ResponseStream, expectedBytes, cancellation);
            return new TransportObservation
            {
                Operation = "authenticated-get",
                HttpStatus = (int)response.HttpStatusCode,
                StatusClass = ClassifyStatus((int)response.HttpStatusCode),
                ByteCount = body.ByteCount,
                BodySha256 = body.BodySha256,
                PrefixSha256 = body.PrefixSha256,
                EofConfirmed = body.EofConfirmed,
                LimitReached = body.LimitReached
            };
        }
        catch (AmazonS3Exception exception)
        {
            return FromS3Exception("authenticated-get", exception);
        }
    }

    private static TransportObservation UnsignedGet(
        string endpoint,
        string key,
        int expectedBytes,
        CancellationToken cancellation)
    {
        // 署名無し経路はAWS SDKを通すと自動署名され得るため、独立したHttpClientで生成します。
        // 400などの拒否を認証拒否と読み替えず、生成したmethod/URIと認証要素の有無だけを記録します。
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        var objectUri = BuildObjectUri(endpoint, key);
        using var request = new HttpRequestMessage(HttpMethod.Get, objectUri);
        var hasAuthorization = request.Headers.Authorization is not null;
        var hasSignatureQuery = objectUri.Query.Contains("X-Amz-", StringComparison.OrdinalIgnoreCase);
        var hasTargetChangingQuery = objectUri.Query.Length > 0;
        var requestMethod = request.Method.Method;
        var requestUri = objectUri.AbsoluteUri;
        using var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation)
            .GetAwaiter().GetResult();
        var body = response.Content is null
            ? new BodyObservation(0, null, null, true, false, null)
            : ReadBody(response.Content.ReadAsStream(cancellation), expectedBytes, cancellation, UnsignedReadLimit, captureS3Code: true);
        var status = (int)response.StatusCode;
        return new TransportObservation
        {
            Operation = "unsigned-get",
            HttpStatus = status,
            StatusClass = ClassifyStatus(status),
            S3Code = body.S3Code,
            ByteCount = body.ByteCount,
            BodySha256 = body.BodySha256,
            PrefixSha256 = body.PrefixSha256,
            EofConfirmed = body.EofConfirmed,
            LimitReached = body.LimitReached,
            Redirected = status is >= 300 and <= 399,
            RequestMethod = requestMethod,
            RequestUri = requestUri,
            AuthorizationPresent = hasAuthorization,
            SignatureQueryPresent = hasSignatureQuery,
            TargetChangingQueryPresent = hasTargetChangingQuery
        };
    }

    private static TransportObservation Delete(
        string accessKeyId,
        string secretAccessKey,
        string endpoint,
        string key,
        CancellationToken cancellation)
    {
        using var client = CreateClient(accessKeyId, secretAccessKey, endpoint);
        var request = new DeleteObjectRequest { BucketName = "osm-artifacts", Key = key };
        try
        {
            var response = client.DeleteObjectAsync(request, cancellation).GetAwaiter().GetResult();
            return new TransportObservation
            {
                Operation = "delete",
                HttpStatus = (int)response.HttpStatusCode,
                StatusClass = ClassifyStatus((int)response.HttpStatusCode),
                EofConfirmed = true
            };
        }
        catch (AmazonS3Exception exception)
        {
            return FromS3Exception("delete", exception);
        }
    }

    private static AmazonS3Client CreateClient(string accessKeyId, string secretAccessKey, string endpoint)
    {
        var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKeyId, secretAccessKey);
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            AuthenticationRegion = "auto",
            ForcePathStyle = true,
            Timeout = Timeout.InfiniteTimeSpan,
            LogResponse = false,
            LogMetrics = false
        };
        return new AmazonS3Client(credentials, config);
    }

    private static Uri BuildObjectUri(string endpoint, string key)
    {
        var escaped = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        return new Uri(endpoint + "/osm-artifacts/" + escaped, UriKind.Absolute);
    }

    private static BodyObservation ReadBody(
        Stream stream,
        int expectedBytes,
        CancellationToken cancellation,
        int maximumBytes = UnsignedReadLimit,
        bool captureS3Code = false)
    {
        // 本文を蓄積せず、期待長までのprefixと全体hashだけを逐次計算します。
        // 上限+1 byteを読んでEOF未確認を検出しつつ、その時点までに得たprefix証拠は保持します。
        using (stream)
        using (var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        using (var prefix = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
        {
            var buffer = new byte[1024];
            var count = 0;
            var prefixCount = 0;
            var limitReached = false;
            var errorCode = captureS3Code ? new ErrorCodeScanner() : null;
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                var read = stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellation).AsTask().GetAwaiter().GetResult();
                if (read == 0) break;
                var allowed = Math.Min(read, maximumBytes + 1 - count);
                if (allowed > 0)
                {
                    digest.AppendData(buffer, 0, allowed);
                    errorCode?.Append(buffer, 0, allowed);
                    if (prefixCount < expectedBytes)
                    {
                        var prefixLength = Math.Min(allowed, expectedBytes - prefixCount);
                        prefix.AppendData(buffer, 0, prefixLength);
                        prefixCount += prefixLength;
                    }
                    count += allowed;
                }
                if (count > maximumBytes)
                {
                    limitReached = true;
                    break;
                }
            }

            if (limitReached)
            {
                // 8193 byte目で止まっても、先頭N byteがobjectと一致した事実は露出判定に必要です。
                var prefixHashAtLimit = prefixCount == expectedBytes
                    ? Convert.ToHexString(prefix.GetHashAndReset()).ToLowerInvariant()
                    : null;
                return new BodyObservation(count, null, prefixHashAtLimit, false, true, errorCode?.Code);
            }

            var prefixHash = prefixCount == expectedBytes
                ? Convert.ToHexString(prefix.GetHashAndReset()).ToLowerInvariant()
                : null;
            return new BodyObservation(
                count,
                Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant(),
                prefixHash,
                true,
                false,
                errorCode?.Code);
        }
    }

    private sealed class ErrorCodeScanner
    {
        private const string OpeningTag = "<Code>";
        private const string ClosingTag = "</Code>";
        private readonly StringBuilder _openingCandidate = new();
        private readonly StringBuilder _codeCandidate = new();
        private bool _insideCode;
        private bool _finished;

        public string? Code { get; private set; }

        public void Append(byte[] bytes, int offset, int count)
        {
            for (var index = offset; index < offset + count && !_finished; index++)
            {
                var value = bytes[index];
                var character = value <= 0x7f ? (char)value : '\ufffd';
                if (!_insideCode)
                {
                    _openingCandidate.Append(character);
                    var candidate = _openingCandidate.ToString();
                    if (candidate.EndsWith(OpeningTag, StringComparison.Ordinal))
                    {
                        _insideCode = true;
                        _openingCandidate.Clear();
                        continue;
                    }

                    while (_openingCandidate.Length > 0 &&
                           !OpeningTag.StartsWith(_openingCandidate.ToString(), StringComparison.Ordinal))
                    {
                        _openingCandidate.Remove(0, 1);
                    }
                    continue;
                }

                _codeCandidate.Append(character);
                if (_codeCandidate.Length > 80)
                {
                    // Code要素だけを短く保持し、本文やMessageをメモリに残さないよう上限で打ち切ります。
                    _finished = true;
                    _codeCandidate.Clear();
                    continue;
                }

                var text = _codeCandidate.ToString();
                if (!text.EndsWith(ClosingTag, StringComparison.Ordinal)) continue;
                var code = text[..^ClosingTag.Length].Trim();
                if (SafeErrorCode.IsMatch(code)) Code = code;
                _finished = true;
                _codeCandidate.Clear();
            }
        }
    }

    private static TransportObservation FromS3Exception(string operation, AmazonS3Exception exception)
    {
        var status = (int)exception.StatusCode;
        return new TransportObservation
        {
            Operation = operation,
            HttpStatus = status > 0 ? status : null,
            StatusClass = ClassifyStatus(status),
            S3Code = SafeCode(exception.ErrorCode),
            EofConfirmed = true
        };
    }

    private static string? SafeCode(string? code) => code is not null && SafeErrorCode.IsMatch(code) ? code : null;

    private static string ClassifyStatus(int status) => status switch
    {
        >= 200 and <= 299 => "success",
        301 or 302 or 303 or 307 or 308 => "redirect",
        401 => "unauthorized",
        403 => "forbidden",
        404 => "not-found",
        429 => "too-many-requests",
        >= 500 and <= 599 => "server-error",
        _ => "other"
    };

    private static TransportObservation Failure(string operation, string statusClass, bool timedOut) => new()
    {
        Operation = operation,
        StatusClass = statusClass,
        TimedOut = timedOut,
        EofConfirmed = false
    };

    private sealed record BodyObservation(
        int ByteCount,
        string? BodySha256,
        string? PrefixSha256,
        bool EofConfirmed,
        bool LimitReached,
        string? S3Code);
}
