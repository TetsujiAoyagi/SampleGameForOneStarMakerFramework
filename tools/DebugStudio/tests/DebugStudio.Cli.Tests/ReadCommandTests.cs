#nullable enable

using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using DebugStudio.Client;
using DebugStudio.Cli;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Cli.Tests;

public sealed class ReadCommandTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("9223372036854775808")]
    [InlineData("1.0")]
    public void Inspect_rejects_invalid_identifier(string id) => Assert.Equal(2, CliArgumentParser.Parse(["inspect", id]).ExitCode);

    [Theory]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void Read_rejects_invalid_timeout(string seconds) => Assert.Equal(2, CliArgumentParser.Parse(["hierarchy", "--timeout-seconds", seconds]).ExitCode);

    [Fact]
    public void Read_defaults_and_upper_boundary_leave_send_timeout_unrestricted()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), CliArgumentParser.Parse(["hierarchy"]).ReadOptions!.Timeout);
        Assert.Equal(long.MaxValue, CliArgumentParser.Parse(["inspect", "9223372036854775807", "--timeout-seconds", "60"]).ReadOptions!.NodeId);
        Assert.Equal(TimeSpan.FromSeconds(120), CliArgumentParser.Parse(["send", "--command", "ping", "--timeout-seconds", "120"]).Options!.Timeout);
    }

    [Theory]
    [InlineData("ok", "ready", 0)]
    [InlineData("error", "not-found", 1)]
    [InlineData("error", "timeout", 3)]
    public async Task Read_returns_one_json_object_and_distinct_exit(string status, string reason, int expected)
    {
        using var output = new StringWriter();
        var exit = await ReadCommand.ExecuteAsync(new(new Uri("ws://localhost/"), "inspect", long.MaxValue, TimeSpan.FromSeconds(15)), output,
            (_, request) => Task.FromResult(new ControlReadResponseEnvelopeV1 { Status = status, Reason = reason,
                Inspector = new() { TargetId = request.NodeId, State = InspectorDetailState.Ready, Sections = [new() { Properties = [new() { RawValue = "true", ValueTypeId = 1 }] }] } }));
        Assert.Equal(expected, exit);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("9223372036854775807", json.RootElement.GetProperty("data").GetProperty("targetId").GetString());
        Assert.Equal("true", json.RootElement.GetProperty("data").GetProperty("sections")[0].GetProperty("properties")[0].GetProperty("rawValue").GetString());
    }

    [Fact]
    public async Task 旧hostのcommand失敗応答はtimeoutではなく読取り失敗になる()
    {
        var reservation = new TcpListener(IPAddress.Loopback, 0);
        reservation.Start();
        var port = ((IPEndPoint)reservation.LocalEndpoint).Port;
        reservation.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/cli-control/");
        listener.Start();
        var server = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            using var socket = (await context.AcceptWebSocketAsync(null)).WebSocket;
            var buffer = new byte[8192];
            var received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(buffer[..received.Count], out var envelope));
            var frame = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.ControlCommandResponse,
                new ControlCommandResponseEnvelopeV1 { RequestId = envelope!.RequestId!, Status = ControlCommandRoundtripStatus.Failed }, envelope.RequestId);
            await socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, CancellationToken.None);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        });
        using var output = new StringWriter();
        var exit = await ReadCommand.ExecuteAsync(new(new Uri($"ws://127.0.0.1:{port}/cli-control/"), "hierarchy", 0, TimeSpan.FromSeconds(15)), output)
            .WaitAsync(TimeSpan.FromSeconds(5));
        await server.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(1, exit);
        using var json = JsonDocument.Parse(output.ToString());
        Assert.Equal("control-unsupported", json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public void Hierarchy_identifiers_are_strings_without_losing_empty_snapshot_state()
    {
        using var json = JsonDocument.Parse(ReadCommand.Serialize(new() { Status = "ok", Hierarchy = new()
        { HasSnapshot = true, Revision = 1, Nodes = [new() { NodeId = long.MaxValue, ParentId = 0 }] } }));
        var data = json.RootElement.GetProperty("data");
        Assert.True(data.GetProperty("hasSnapshot").GetBoolean());
        Assert.Equal("9223372036854775807", data.GetProperty("nodes")[0].GetProperty("nodeId").GetString());
        Assert.Equal("0", data.GetProperty("nodes")[0].GetProperty("parentId").GetString());
    }
}
