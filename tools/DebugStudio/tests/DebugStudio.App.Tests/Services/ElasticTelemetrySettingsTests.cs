#nullable enable

using DebugStudio.App.Core.Services;

namespace DebugStudio.App.Tests.Services;

/// <summary>
/// 環境変数 reader 注入で process-global 競合を避けつつ、設定と秘密非漏洩を固定する。
/// </summary>
public sealed class ElasticTelemetrySettingsTests
{
    [Fact]
    public void TryCreate_既定loopbackとApiKey有無だけをUI向けに返す()
    {
        var reader = new StubElasticEnvironmentReader
        {
            ElasticUrl = null,
            KibanaUrl = "http://127.0.0.1:5601",
            ElasticApiKey = "abc123",
        };

        var succeeded = ElasticTelemetrySettings.TryCreate(reader, out var settings, out var errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.NotNull(settings);
        Assert.Equal("localhost", settings!.ElasticUrl.Host);
        Assert.Equal(9200, settings.ElasticUrl.Port);
        Assert.True(settings.HasApiKey);
        Assert.Contains("ApiKey=configured", settings.DescribeConfigurationForUi(), StringComparison.Ordinal);
        Assert.DoesNotContain("abc123", settings.DescribeConfigurationForUi(), StringComparison.Ordinal);
    }

    [Fact]
    public void TryCreate_外部endpointは拒否する()
    {
        var reader = new StubElasticEnvironmentReader
        {
            ElasticUrl = "http://example.com:9200",
        };

        var succeeded = ElasticTelemetrySettings.TryCreate(reader, out _, out var errorMessage);

        Assert.False(succeeded);
        Assert.Contains("loopback", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubElasticEnvironmentReader : IElasticEnvironmentReader
    {
        public string? ElasticUrl { get; init; }

        public string? ElasticApiKey { get; init; }

        public string? KibanaUrl { get; init; }

        public string? ReadElasticUrl() => ElasticUrl;

        public string? ReadElasticApiKey() => ElasticApiKey;

        public string? ReadKibanaUrl() => KibanaUrl;
    }
}
