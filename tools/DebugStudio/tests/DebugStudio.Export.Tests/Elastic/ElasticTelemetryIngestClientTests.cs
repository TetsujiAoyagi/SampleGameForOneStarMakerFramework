#nullable enable

using System.Net;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Text.Json;
using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// Elastic HTTP client の method / URI / header / bulk 応答解析を固定する。
/// </summary>
public sealed class ElasticTelemetryIngestClientTests
{
    [Fact]
    public async Task PreflightAsync_GETルートへ疎通確認する()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"tagline\":\"You Know, for Search\"}"),
            });
        var client = CreateClient(handler);

        var result = await client.PreflightAsync();

        Assert.True(result.Success);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri("http://localhost:9200/"), request.RequestUri);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task BootstrapAsync_templateとpipelineをPUTしdefault_pipelineを含める()
    {
        string? templateBody = null;
        var handler = new RecordingHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Put && templateBody == null)
            {
                templateBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}"),
            };
        });
        var client = CreateClient(handler, apiKeyBase64Value: "abc123");

        var result = await client.BootstrapAsync();

        Assert.True(result.Success);
        Assert.Equal(2, handler.Requests.Count);

        var templateRequest = handler.Requests[0];
        Assert.Equal(HttpMethod.Put, templateRequest.Method);
        Assert.Equal(new Uri("http://localhost:9200/_index_template/debugstudio-telemetry"), templateRequest.RequestUri);
        Assert.Equal("ApiKey abc123", templateRequest.Headers.Authorization?.ToString());
        using var templateDocument = JsonDocument.Parse(templateBody!);
        var settings = templateDocument.RootElement.GetProperty("template").GetProperty("settings");
        Assert.Equal(
            "debugstudio-telemetry",
            settings.GetProperty("index").GetProperty("default_pipeline").GetString());
        Assert.False(settings.TryGetProperty("default_pipeline", out _));

        var pipelineRequest = handler.Requests[1];
        Assert.Equal(HttpMethod.Put, pipelineRequest.Method);
        Assert.Equal(new Uri("http://localhost:9200/_ingest/pipeline/debugstudio-telemetry"), pipelineRequest.RequestUri);
    }

    [Fact]
    public async Task PushBulkAsync_POST_bulkへapplication_x_ndjsonと終端改行付きpayloadを送る()
    {
        var payload = Encoding.UTF8.GetBytes("{\"create\":{\"_index\":\"debugstudio-telemetry-2026.04.29\"}}\n{}\n");
        byte[]? capturedPayload = null;
        var handler = new RecordingHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                capturedPayload = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"errors\":false,\"items\":[{\"create\":{\"status\":201}}]}"),
            };
        });
        var client = CreateClient(handler);

        var result = await client.PushBulkAsync(payload);

        Assert.True(result.Success);
        Assert.NotNull(capturedPayload);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal(new Uri("http://localhost:9200/_bulk"), handler.Requests[0].RequestUri);
        Assert.Equal("application/x-ndjson", handler.Requests[0].Content!.Headers.ContentType!.MediaType);
        Assert.Equal(payload, capturedPayload);
    }

    [Fact]
    public void ParseBulkResponse_errors_trueとitem_statusを解析する()
    {
        const string body = """
            {
              "errors": true,
              "items": [
                { "create": { "status": 201 } },
                { "create": { "status": 409, "error": { "type": "version_conflict_engine_exception" } } }
              ]
            }
            """;

        var result = ElasticTelemetryIngestClient.ParseBulkResponse(body);

        Assert.False(result.Success);
        Assert.Equal(1, result.AcceptedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains("409", result.ItemErrors[0], StringComparison.Ordinal);
        Assert.Contains("version_conflict_engine_exception", result.ItemErrors[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"errors":false}""")]
    [InlineData("""{"errors":false,"items":[]}""")]
    [InlineData("""{"errors":false,"items":[{"index":{"status":201}}]}""")]
    [InlineData("""{"errors":false,"items":[{"create":{}}]}""")]
    [InlineData("""{"errors":false,"items":[{"create":{"status":"201"}}]}""")]
    [InlineData("""{"errors":"false","items":[{"create":{"status":201}}]}""")]
    public void ParseBulkResponse_検証不能な応答は成功扱いにしない(string responseBody)
    {
        var result = ElasticTelemetryIngestClient.ParseBulkResponse(responseBody);

        Assert.False(result.Success);
        Assert.Equal(0, result.AcceptedCount);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public void ParseBulkResponse_不正JSONを安全な失敗結果に変換する()
    {
        var result = ElasticTelemetryIngestClient.ParseBulkResponse("{not-json");

        Assert.False(result.Success);
        Assert.Contains("invalid JSON", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushBulkAsync_通信断は自動retryせず受理不明として返す()
    {
        var handler = new RecordingHttpMessageHandler(_ => throw new HttpRequestException("connection reset"));
        var client = CreateClient(handler);

        var result = await client.PushBulkAsync(Encoding.UTF8.GetBytes("{}\n"));

        Assert.False(result.Success);
        Assert.False(result.IsRetryable);
        Assert.Contains("409", result.Message, StringComparison.Ordinal);
        Assert.Contains("Do not automatically retry", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushBulkAsync_取消は受理不明として自動retry不可にする()
    {
        var handler = new RecordingHttpMessageHandler(_ => throw new OperationCanceledException("request canceled"));
        var client = CreateClient(handler);

        var result = await client.PushBulkAsync(Encoding.UTF8.GetBytes("{}\n"));

        Assert.False(result.Success);
        Assert.False(result.IsRetryable);
        Assert.Contains("duplicate documents", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreflightAsync_応答本文を安全な診断へ含めない()
    {
        const string sensitiveTelemetry = "telemetry-message-should-not-leak";
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                ReasonPhrase = "Bad Request",
                Content = new StringContent(sensitiveTelemetry),
            });
        var client = CreateClient(handler);

        var result = await client.PreflightAsync();

        Assert.False(result.Success);
        Assert.Contains("HTTP 400 Bad Request", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveTelemetry, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushBulkAsync_HTTP失敗の応答本文を安全な診断へ含めない()
    {
        const string sensitiveTelemetry = "telemetry-message-should-not-leak";
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                ReasonPhrase = "Bad Request",
                Content = new StringContent(sensitiveTelemetry),
            });
        var client = CreateClient(handler);

        var result = await client.PushBulkAsync(Encoding.UTF8.GetBytes("{}\n"));

        Assert.False(result.Success);
        Assert.Contains("HTTP 400 Bad Request", result.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveTelemetry, result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PushBulkAsync_response本文読取失敗を受理不明の安全な失敗に変換する()
    {
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ThrowingHttpContent(),
            });
        var client = CreateClient(handler);

        var result = await client.PushBulkAsync(Encoding.UTF8.GetBytes("{}\n"));

        Assert.False(result.Success);
        Assert.False(result.IsRetryable);
        Assert.Contains("duplicate documents", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://example.com:9200")]
    [InlineData("http://localhost:9200/proxy")]
    [InlineData("http://localhost:9200?target=external")]
    public void Constructor_loopback以外またはroot以外を拒否する(string endpoint)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ElasticTelemetryIngestClient(new HttpClient(), new Uri(endpoint), apiKeyBase64Value: null));

        Assert.Contains("endpoint", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ElasticTelemetryIngestClient CreateClient(
        HttpMessageHandler handler,
        string? apiKeyBase64Value = null)
    {
        return new ElasticTelemetryIngestClient(
            new HttpClient(handler),
            new Uri("http://localhost:9200"),
            apiKeyBase64Value);
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            ArgumentNullException.ThrowIfNull(responder);
            _responder = (request, _) => Task.FromResult(responder(request));
        }

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder ?? throw new ArgumentNullException(nameof(responder));
        }

        public List<HttpRequestMessage> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return await _responder(request, cancellationToken);
        }
    }

    private sealed class ThrowingHttpContent : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            Task.FromException(new HttpRequestException("response body read failed"));

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return true;
        }
    }
}
