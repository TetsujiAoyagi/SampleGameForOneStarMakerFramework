#nullable enable

using DebugStudio.Contracts.Protocol;
using MessagePack;

namespace DebugStudio.Contracts.Tests;

public sealed class ControlReadRoundtripTests
{
    [Fact]
    public void Read_request_and_response_preserve_large_identifiers_and_typed_raw_values()
    {
        var request = new ControlReadRequestEnvelopeV1 { RequestId = "read-1", Operation = "inspect", NodeId = long.MaxValue, TimeoutMilliseconds = 60000 };
        var roundtrip = MessagePackSerializer.Deserialize<ControlReadRequestEnvelopeV1>(MessagePackSerializer.Serialize(request));
        Assert.Equal(long.MaxValue, roundtrip.NodeId);
        Assert.Equal(60000, roundtrip.TimeoutMilliseconds);
        var response = new ControlReadResponseEnvelopeV1 { RequestId = request.RequestId, Status = "ok", Reason = "ready",
            Inspector = new InspectorDetailEnvelopeV1 { TargetId = long.MaxValue, State = InspectorDetailState.Ready,
                Sections = [new InspectorSectionDtoV1 { Properties = [new InspectorPropertyDtoV1 { ValueTypeId = 1, RawValue = "true" }] }] } };
        var actual = MessagePackSerializer.Deserialize<ControlReadResponseEnvelopeV1>(MessagePackSerializer.Serialize(response));
        Assert.Equal(long.MaxValue, actual.Inspector!.TargetId);
        Assert.Equal("true", actual.Inspector.Sections[0].Properties[0].RawValue);
        Assert.Null(actual.Hierarchy);
    }
}
