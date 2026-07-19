#nullable enable

using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// L1 Verify は loopback endpoint のみ許可し、外部送信や URL 内 secret を防ぐ。
/// </summary>
public sealed class ElasticLoopbackEndpointPolicyTests
{
    [Theory]
    [InlineData("http://localhost:9200")]
    [InlineData("https://127.0.0.1:9200")]
    [InlineData("http://[::1]:9200")]
    public void TryValidate_loopbackのみ受理する(string url)
    {
        var succeeded = ElasticLoopbackEndpointPolicy.TryValidate(
            url,
            ElasticLoopbackEndpointPolicy.DefaultElasticUrl,
            out var validated,
            out var errorMessage);

        Assert.True(succeeded, errorMessage);
        Assert.Equal(url.TrimEnd('/'), validated.ToString().TrimEnd('/'));
    }

    [Theory]
    [InlineData("http://example.com:9200")]
    [InlineData("http://192.168.0.10:9200")]
    public void TryValidate_外部hostは拒否する(string url)
    {
        var succeeded = ElasticLoopbackEndpointPolicy.TryValidate(
            url,
            ElasticLoopbackEndpointPolicy.DefaultElasticUrl,
            out _,
            out var errorMessage);

        Assert.False(succeeded);
        Assert.Contains("loopback", errorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ftp://localhost:9200")]
    [InlineData("http://localhost:9200?x=1")]
    [InlineData("http://localhost:9200#frag")]
    [InlineData("http://user:secret@localhost:9200")]
    [InlineData("http://localhost:9200/proxy")]
    [InlineData("http://localhost:9200/proxy/elastic")]
    public void TryValidate_不正schemeやuserinfo_query_fragmentは拒否する(string url)
    {
        var succeeded = ElasticLoopbackEndpointPolicy.TryValidate(
            url,
            ElasticLoopbackEndpointPolicy.DefaultElasticUrl,
            out _,
            out _);

        Assert.False(succeeded);
    }

    [Fact]
    public void TryValidate_未設定時は既定localhostを使う()
    {
        var succeeded = ElasticLoopbackEndpointPolicy.TryValidate(
            null,
            ElasticLoopbackEndpointPolicy.DefaultElasticUrl,
            out var validated,
            out _);

        Assert.True(succeeded);
        Assert.Equal("http://localhost:9200/", validated.ToString());
    }
}
