using DebugStudio.Cli;
using DebugStudio.Client;

namespace DebugStudio.Cli.Tests;

public sealed class CliArgumentParserTests
{
    [Fact]
    public void Parse_sendの主要オプションを解釈できる()
    {
        var result = CliArgumentParser.Parse(
        [
            "send",
            "--uri", "ws://127.0.0.1:5011/debugsocket/",
            "--command", "debugsocket.ping",
            "--payload", "{\"ping\":true}",
            "--timeout-seconds", "9.5",
        ]);

        Assert.True(result.Success);
        Assert.NotNull(result.Options);
        Assert.Equal(new Uri("ws://127.0.0.1:5011/debugsocket/"), result.Options!.ControlUri);
        Assert.Equal("debugsocket.ping", result.Options.CommandType);
        Assert.Equal("{\"ping\":true}", result.Options.PayloadJson);
        Assert.Equal(TimeSpan.FromSeconds(9.5), result.Options.Timeout);
    }

    [Fact]
    public void Parse_sendは既定値を補完する()
    {
        var result = CliArgumentParser.Parse(
        [
            "send",
            "--command", "debugsocket.ping",
        ]);

        Assert.True(result.Success);
        Assert.NotNull(result.Options);
        Assert.Equal(DebugStudioControlPlaneDefaults.DefaultControlUri, result.Options!.ControlUri);
        Assert.Equal("{}", result.Options.PayloadJson);
        Assert.Equal(TimeSpan.FromSeconds(15), result.Options.Timeout);
    }

    [Fact]
    public void Parse_control_uri別名を解釈できる()
    {
        var result = CliArgumentParser.Parse(
        [
            "send",
            "--control-uri", "ws://127.0.0.1:5012/cli-control/",
            "--command", "debugsocket.ping",
        ]);

        Assert.True(result.Success);
        Assert.NotNull(result.Options);
        Assert.Equal(new Uri("ws://127.0.0.1:5012/cli-control/"), result.Options!.ControlUri);
    }

    [Fact]
    public void Parse_command未指定はusageエラーになる()
    {
        var result = CliArgumentParser.Parse(["send"]);

        Assert.False(result.Success);
        Assert.True(result.ShowUsage);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("--command", result.ErrorMessage, StringComparison.Ordinal);
    }
}
