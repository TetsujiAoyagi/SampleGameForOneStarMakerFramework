#nullable enable

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// L1 Verify 用の Elastic HTTP client。
/// preflight / bootstrap / `_bulk` を loopback endpoint へだけ送る。
/// </summary>
public sealed class ElasticTelemetryIngestClient
{
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient;
    private readonly Uri _elasticBaseUrl;
    private readonly string? _apiKeyAuthorizationHeaderValue;

    /// <summary>
    /// HttpClient は呼び出し側が所有する。timeout を短くしたい場合も client 自体の Timeout は変更しない。
    /// </summary>
    public ElasticTelemetryIngestClient(HttpClient httpClient, Uri elasticBaseUrl, string? apiKeyBase64Value)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(elasticBaseUrl);
        if (!ElasticLoopbackEndpointPolicy.TryValidate(
                elasticBaseUrl.AbsoluteUri,
                ElasticLoopbackEndpointPolicy.DefaultElasticUrl,
                out var validatedBaseUrl,
                out var validationError))
        {
            throw new ArgumentException(validationError, nameof(elasticBaseUrl));
        }

        // service 経由だけでなく直接生成されても外部送信を許可しない。
        _elasticBaseUrl = validatedBaseUrl;
        _apiKeyAuthorizationHeaderValue = CreateApiKeyAuthorizationHeaderValue(apiKeyBase64Value);
    }

    public Uri ElasticBaseUrl => _elasticBaseUrl;

    /// <summary>
    /// Elastic root へ GET / し、疎通だけ確認する。
    /// </summary>
    public async Task<ElasticPreflightResult> PreflightAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SendPreflightAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ElasticIngestTransportException ex)
        {
            return ElasticPreflightResult.Failed(ex.Message, ex.IsRetryable);
        }
    }

    /// <summary>
    /// telemetry index template と ingest pipeline を PUT する。
    /// </summary>
    public async Task<ElasticBootstrapResult> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await SendBootstrapAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ElasticIngestTransportException ex)
        {
            return ElasticBootstrapResult.Failed(ex.Message, ex.IsRetryable);
        }
    }

    /// <summary>
    /// 事前構築済み NDJSON を `_bulk` へ POST する。
    /// </summary>
    public async Task<ElasticBulkPushResult> PushBulkAsync(byte[] ndjsonPayload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ndjsonPayload);

        if (ndjsonPayload.Length == 0)
        {
            throw new ArgumentException("Bulk payload must not be empty.", nameof(ndjsonPayload));
        }

        try
        {
            return await SendBulkAsync(ndjsonPayload, cancellationToken).ConfigureAwait(false);
        }
        catch (ElasticIngestTransportException ex)
        {
            return ElasticBulkPushResult.Failed(ex.Message, isRetryable: ex.IsRetryable);
        }
        catch (JsonException)
        {
            return ElasticBulkPushResult.Failed(
                "Elastic bulk returned an invalid JSON response; accepted items cannot be verified.",
                isRetryable: false);
        }
        catch (OperationCanceledException)
        {
            return ElasticBulkPushResult.Failed(CreateUncertainDeliveryMessage(), isRetryable: false);
        }
        catch (HttpRequestException)
        {
            return ElasticBulkPushResult.Failed(CreateUncertainDeliveryMessage(), isRetryable: false);
        }
    }

    private async Task<ElasticPreflightResult> SendPreflightAsync(CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, new Uri(_elasticBaseUrl, "/"));
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            return ElasticPreflightResult.Succeeded("Elastic preflight succeeded.");
        }

        return ElasticPreflightResult.Failed(
            $"Elastic preflight failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
            isRetryable: false);
    }

    private async Task<ElasticBootstrapResult> SendBootstrapAsync(CancellationToken cancellationToken)
    {
        var templateResult = await PutJsonAsync(
            new Uri(_elasticBaseUrl, "_index_template/" + ElasticTelemetryIndexTemplateDefinition.TemplateName),
            ElasticTelemetryIndexTemplateDefinition.CreateBootstrapJson(),
            cancellationToken).ConfigureAwait(false);
        if (!templateResult.Success)
        {
            return ElasticBootstrapResult.Failed(templateResult.Message, templateResult.IsRetryable);
        }

        var pipelineResult = await PutJsonAsync(
            new Uri(_elasticBaseUrl, "_ingest/pipeline/" + ElasticTelemetryIngestPipelineDefinition.PipelineName),
            ElasticTelemetryIngestPipelineDefinition.CreateBootstrapJson(),
            cancellationToken).ConfigureAwait(false);
        if (!pipelineResult.Success)
        {
            return ElasticBootstrapResult.Failed(pipelineResult.Message, pipelineResult.IsRetryable);
        }

        return ElasticBootstrapResult.Succeeded("Elastic bootstrap succeeded.");
    }

    private async Task<ElasticBulkPushResult> SendBulkAsync(byte[] ndjsonPayload, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, new Uri(_elasticBaseUrl, "_bulk"));
        request.Content = new ByteArrayContent(ndjsonPayload);
        request.Content.Headers.TryAddWithoutValidation("Content-Type", ElasticBulkTelemetryNdjsonBuilder.BulkContentType);

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return ElasticBulkPushResult.Failed(
                $"Elastic bulk failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
                isRetryable: false);
        }

        // bulk item の status/error.type は安全な構造化診断として必要だが、
        // response 本文を UI/ログ向けメッセージへ丸ごと転記してはならない。
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseBulkResponse(body);
    }

    private async Task<(bool Success, string Message, bool IsRetryable)> PutJsonAsync(
        Uri requestUri,
        string json,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Put, requestUri);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return (true, string.Empty, false);
        }

        return (false, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}.", false);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, Uri requestUri)
    {
        var request = new HttpRequestMessage(method, requestUri);
        if (!string.IsNullOrEmpty(_apiKeyAuthorizationHeaderValue))
        {
            request.Headers.TryAddWithoutValidation("Authorization", _apiKeyAuthorizationHeaderValue);
        }

        return request;
    }

    /// <summary>
    /// 注入済み HttpClient の Timeout は触らず、リクエスト単位で短い timeout を掛ける。
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(DefaultRequestTimeout);

        try
        {
            return await _httpClient.SendAsync(request, timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            throw new ElasticIngestTransportException(
                "Elastic request was canceled or timed out after it may have reached the server. Do not automatically retry; manual re-run can create duplicate documents or HTTP 409.",
                isRetryable: false,
                innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ElasticIngestTransportException(
                "Elastic request connection failed after it may have reached the server. Do not automatically retry; manual re-run can create duplicate documents or HTTP 409.",
                isRetryable: false,
                innerException: ex);
        }
    }

    public static ElasticBulkPushResult ParseBulkResponse(string responseBody)
    {
        try
        {
            return ParseBulkResponseCore(responseBody);
        }
        catch (JsonException)
        {
            return ElasticBulkPushResult.Failed(
                "Elastic bulk returned an invalid JSON response; accepted items cannot be verified.",
                isRetryable: false);
        }
    }

    private static ElasticBulkPushResult ParseBulkResponseCore(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return ElasticBulkPushResult.Failed("Elastic bulk returned an empty body.", isRetryable: false);
        }

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return InvalidBulkResponse("response root is not an object");
        }

        if (!root.TryGetProperty("errors", out var errorsElement) ||
            (errorsElement.ValueKind != JsonValueKind.True && errorsElement.ValueKind != JsonValueKind.False))
        {
            return InvalidBulkResponse("errors flag is missing or not boolean");
        }

        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return InvalidBulkResponse("items is missing or not an array");
        }

        if (items.GetArrayLength() == 0)
        {
            return InvalidBulkResponse("items is empty");
        }

        var acceptedCount = 0;
        var failedCount = 0;
        var itemErrors = new List<string>();

        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("create", out var createAction) ||
                createAction.ValueKind != JsonValueKind.Object)
            {
                return InvalidBulkResponse("item does not contain a create action");
            }

            if (!createAction.TryGetProperty("status", out var statusElement) ||
                statusElement.ValueKind != JsonValueKind.Number ||
                !statusElement.TryGetInt32(out var status))
            {
                return InvalidBulkResponse("create action status is missing or not numeric");
            }

            if (status >= (int)HttpStatusCode.OK && status <= 299)
            {
                acceptedCount++;
                continue;
            }

            failedCount++;
            var errorType = createAction.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.Object &&
                errorElement.TryGetProperty("type", out var typeElement) &&
                typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : null;
            itemErrors.Add($"status={status}, error.type={errorType ?? "unknown"}");
        }

        var errorsFlag = errorsElement.GetBoolean();
        if (errorsFlag || failedCount > 0)
        {
            return ElasticBulkPushResult.Failed(
                $"Elastic bulk completed with item errors. accepted={acceptedCount}, failed={failedCount}.",
                isRetryable: false,
                acceptedCount,
                failedCount,
                itemErrors);
        }

        return ElasticBulkPushResult.Succeeded(
            $"Elastic bulk succeeded. accepted={acceptedCount}.",
            acceptedCount);
    }

    private static string? CreateApiKeyAuthorizationHeaderValue(string? apiKeyBase64Value)
    {
        if (string.IsNullOrWhiteSpace(apiKeyBase64Value))
        {
            return null;
        }

        return "ApiKey " + apiKeyBase64Value.Trim();
    }

    private static ElasticBulkPushResult InvalidBulkResponse(string reason)
    {
        return ElasticBulkPushResult.Failed(
            $"Elastic bulk returned an invalid response: {reason}. Accepted items cannot be verified.",
            isRetryable: false);
    }

    private static string CreateUncertainDeliveryMessage()
    {
        return "Elastic bulk response could not be read after the request may have reached the server. Do not automatically retry; manual re-run can create duplicate documents or HTTP 409.";
    }

}

/// <summary>
/// create action の受理不明な通信断・取消。
/// サーバが受理済みか判別できないため、自動 retry は許可しない。
/// </summary>
public sealed class ElasticIngestTransportException : Exception
{
    public ElasticIngestTransportException(string message, bool isRetryable, Exception? innerException = null)
        : base(message, innerException)
    {
        IsRetryable = isRetryable;
    }

    public bool IsRetryable { get; }
}

public sealed record ElasticPreflightResult(bool Success, string Message, bool IsRetryable)
{
    public static ElasticPreflightResult Succeeded(string message) => new(true, message, false);

    public static ElasticPreflightResult Failed(string message, bool isRetryable) => new(false, message, isRetryable);
}

public sealed record ElasticBootstrapResult(bool Success, string Message, bool IsRetryable)
{
    public static ElasticBootstrapResult Succeeded(string message) => new(true, message, false);

    public static ElasticBootstrapResult Failed(string message, bool isRetryable) => new(false, message, isRetryable);
}

public sealed record ElasticBulkPushResult(
    bool Success,
    string Message,
    int AcceptedCount,
    int FailedCount,
    bool IsRetryable,
    IReadOnlyList<string> ItemErrors)
{
    public static ElasticBulkPushResult Succeeded(string message, int acceptedCount) =>
        new(true, message, acceptedCount, 0, false, Array.Empty<string>());

    public static ElasticBulkPushResult Failed(
        string message,
        bool isRetryable,
        int acceptedCount = 0,
        int failedCount = 0,
        IReadOnlyList<string>? itemErrors = null) =>
        new(false, message, acceptedCount, failedCount, isRetryable, itemErrors ?? Array.Empty<string>());
}
