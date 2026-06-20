using DebugStudio.Server;

namespace DebugStudio.Server.Tests;

public sealed class ConfigTests
{
    [Fact]
    public void DebugStudioServerOptions_既定値が期待どおり()
    {
        var options = new DebugStudioServerOptions();

        Assert.Equal("127.0.0.1", options.Host);
        Assert.Equal(5011, options.Port);
        Assert.Equal("/debugsocket/", options.WebSocketPath);
        Assert.True(options.Enabled);
        Assert.Equal(60, options.AcceptTimeoutSeconds);
    }

    [Theory]
    [InlineData("/debugsocket", "http://127.0.0.1:5011/debugsocket/")]
    [InlineData("debugsocket/", "http://127.0.0.1:5011/debugsocket/")]
    [InlineData("debugsocket", "http://127.0.0.1:5011/debugsocket/")]
    [InlineData("/", "http://127.0.0.1:5011/")]
    public void GetListenerPrefix_パスの揺れを正規化する(string path, string expected)
    {
        var options = new DebugStudioServerOptions
        {
            WebSocketPath = path,
        };

        Assert.Equal(expected, options.GetListenerPrefix());
    }
}
