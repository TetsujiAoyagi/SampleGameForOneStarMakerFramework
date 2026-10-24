#nullable enable

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Cli;

public sealed record ReadCommandCliOptions(Uri ControlUri, string Operation, long NodeId, TimeSpan Timeout);

internal static class ReadCommand
{
    public static async Task<int> ExecuteAsync(ReadCommandCliOptions options, TextWriter output,
        Func<Uri, ControlReadRequestEnvelopeV1, Task<ControlReadResponseEnvelopeV1>>? read = null)
    {
        read ??= (uri, request) => new CliReadControlPlaneClient().ReadAsync(uri, request);
        var result = await read(options.ControlUri, new ControlReadRequestEnvelopeV1
        {
            RequestId = Guid.NewGuid().ToString("N"), Operation = options.Operation, NodeId = options.NodeId,
            TimeoutMilliseconds = (int)Math.Ceiling(options.Timeout.TotalMilliseconds),
        }).ConfigureAwait(false);
        output.WriteLine(Serialize(result));
        return result.Reason == "timeout" ? 3 : result.Status == "ok" ? 0 : 1;
    }

    internal static string Serialize(ControlReadResponseEnvelopeV1 result)
    {
        object? data = null;
        if (result.Hierarchy is { } h)
            data = new { h.HasSnapshot, h.Revision, h.CapturedAtUnixTimeMilliseconds, h.ConnectionState, h.HandshakeState,
                Nodes = h.Nodes.Select(n => new { NodeId = n.NodeId.ToString(CultureInfo.InvariantCulture),
                    ParentId = n.ParentId.ToString(CultureInfo.InvariantCulture), n.TypeId, n.Flags, n.Depth,
                    n.SiblingIndex, n.ChildCount, n.TraversalIndex, n.Name, n.TypeName }) };
        else if (result.Inspector is { } i)
            data = new { TargetId = i.TargetId.ToString(CultureInfo.InvariantCulture), i.TargetName, i.TargetTypeId,
                i.TargetTypeName, i.State, i.Message, i.Revision, i.CapturedAtUnixTimeMilliseconds, i.Sections };
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        return JsonSerializer.Serialize(new { status = result.Status, reason = result.Reason, data }, jsonOptions);
    }
}
