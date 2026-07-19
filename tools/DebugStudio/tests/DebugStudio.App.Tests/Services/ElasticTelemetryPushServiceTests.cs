#nullable enable

using System.Net;
using System.Net.Http;
using System.Text;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Elastic;

namespace DebugStudio.App.Tests.Services;

/// <summary>
/// retained telemetry のみを対象にし、0 件時は bulk を呼ばず L0 非干渉を保つ。
/// </summary>
public sealed class ElasticTelemetryPushServiceTests
{
    [Fact]
    public async Task PushRetainedTelemetryAsync_telemetry0件なら_bulkを呼ばない()
    {
        var store = new TelemetryStore();
        store.AppendServiceStatus(new DebugSocketServiceStatusEnvelopeV1
        {
            Status = "running",
            Message = "steady",
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });

        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        var service = CreateService(store, handler);

        var result = await service.PushRetainedTelemetryAsync();

        Assert.False(result.Success);
        Assert.Contains("empty", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task PushRetainedTelemetryAsync_serviceStatusは含めずtelemetryのみ送る()
    {
        var store = new TelemetryStore();
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "boot",
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            IsSuccess = true,
        });
        store.AppendServiceStatus(new DebugSocketServiceStatusEnvelopeV1
        {
            Status = "running",
            Message = "steady",
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        });

        byte[]? bulkPayload = null;
        var handler = new RecordingHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Post)
            {
                bulkPayload = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"errors\":false,\"items\":[{\"create\":{\"status\":201}}]}"),
            };
        });
        var service = CreateService(store, handler);

        var result = await service.PushRetainedTelemetryAsync();

        Assert.True(result.Success);
        Assert.NotNull(bulkPayload);
        var payloadText = Encoding.UTF8.GetString(bulkPayload!);
        Assert.DoesNotContain("serviceStatus", payloadText, StringComparison.Ordinal);
        Assert.Contains("boot", payloadText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreflightAsync_失敗しても例外を外へ投げない()
    {
        var store = new TelemetryStore();
        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("down"),
            });
        var service = CreateService(store, handler);

        var result = await service.PreflightAsync();

        Assert.False(result.Success);
        Assert.False(result.IsRetryable);
    }

    [Fact]
    public async Task PushRetainedTelemetryAsync_settingsは一度だけ読みclientとKibana表示を揃える()
    {
        var store = new TelemetryStore();
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "boot",
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            IsSuccess = true,
        });

        var reader = new ChangingElasticEnvironmentReader();
        Uri? clientEndpoint = null;
        var service = new ElasticTelemetryPushService(
            store,
            reader,
            settings =>
            {
                clientEndpoint = settings.ElasticUrl;
                return new ElasticTelemetryIngestClient(
                    new HttpClient(new RecordingHttpMessageHandler(_ =>
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"errors\":false,\"items\":[{\"create\":{\"status\":201}}]}"),
                        })),
                    settings.ElasticUrl,
                    settings.ApiKeyBase64Value);
            });

        var result = await service.PushRetainedTelemetryAsync();

        Assert.True(result.Success);
        Assert.Equal("localhost", clientEndpoint!.Host);
        Assert.Equal("localhost", result.KibanaUrl!.Host);
        Assert.Equal(1, reader.ElasticUrlReadCount);
        Assert.Equal(1, reader.KibanaUrlReadCount);
        Assert.Equal(1, reader.ApiKeyReadCount);
    }

    [Fact]
    public async Task PushRetainedTelemetryAsync_bulk受理件数が送信件数と違えば失敗にする()
    {
        var store = new TelemetryStore();
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "first",
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            IsSuccess = true,
        });
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "second",
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            IsSuccess = true,
        });

        var handler = new RecordingHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"errors\":false,\"items\":[{\"create\":{\"status\":201}}]}"),
            });
        var service = CreateService(store, handler);

        var result = await service.PushRetainedTelemetryAsync();

        Assert.False(result.Success);
        Assert.Equal(1, result.AcceptedCount);
        Assert.Contains("full delivery cannot be verified", result.Message, StringComparison.Ordinal);
    }

    private static ElasticTelemetryPushService CreateService(TelemetryStore store, HttpMessageHandler handler)
    {
        var reader = new StubElasticEnvironmentReader();
        return new ElasticTelemetryPushService(
            store,
            reader,
            _ => new ElasticTelemetryIngestClient(new HttpClient(handler), new Uri("http://localhost:9200"), null));
    }

    private sealed class StubElasticEnvironmentReader : IElasticEnvironmentReader
    {
        public string? ReadElasticUrl() => null;

        public string? ReadElasticApiKey() => null;

        public string? ReadKibanaUrl() => null;
    }

    private sealed class ChangingElasticEnvironmentReader : IElasticEnvironmentReader
    {
        public int ElasticUrlReadCount { get; private set; }

        public int ApiKeyReadCount { get; private set; }

        public int KibanaUrlReadCount { get; private set; }

        public string? ReadElasticUrl()
        {
            ElasticUrlReadCount++;
            return ElasticUrlReadCount == 1
                ? "http://localhost:9200"
                : "http://127.0.0.1:9201";
        }

        public string? ReadElasticApiKey()
        {
            ApiKeyReadCount++;
            return null;
        }

        public string? ReadKibanaUrl()
        {
            KibanaUrlReadCount++;
            return KibanaUrlReadCount == 1
                ? "http://localhost:5601"
                : "http://127.0.0.1:5602";
        }
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
}
