#nullable enable

using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests.Stores;

public sealed class TelemetryStoreCloneTests
{
    [Fact]
    public void AppendTelemetry_cloneでcameraFieldsが保持される()
    {
        var store = new TelemetryStore(retainedCapacity: 4);
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "CameraSystemSnapshot",
            EndTimestampUtcTicks = 123,
            CameraTotalViewCount = 3,
            CameraAdditionalViewCount = 2,
            CameraBlendingViewCount = 1,
            CameraMaxStackDepthTotal = 4,
            CameraViewId = 5,
            CameraActiveCameraHash = 6,
        });

        var snapshot = store.GetTelemetrySnapshot();
        Assert.Single(snapshot);
        Assert.Equal(3, snapshot[0].CameraTotalViewCount);
        Assert.Equal(2, snapshot[0].CameraAdditionalViewCount);
        Assert.Equal(1, snapshot[0].CameraBlendingViewCount);
        Assert.Equal(4, snapshot[0].CameraMaxStackDepthTotal);
        Assert.Equal(5, snapshot[0].CameraViewId);
        Assert.Equal(6, snapshot[0].CameraActiveCameraHash);
    }

    [Fact]
    public void AppendTelemetry_cloneでKindとPayloadが保持される()
    {
        var store = new TelemetryStore(retainedCapacity: 4);
        store.AppendTelemetry(new DebugTelemetryEnvelopeV1
        {
            Name = "ProfilerSummary",
            Kind = "sample",
            SchemaVersion = 3,
            EndTimestampUtcTicks = 123,
            ElapsedMs = 0,
            Payload = new DebugTelemetryPayloadV1
            {
                Shape = 2,
                Fps = 60f,
                CpuMs = 14f,
            },
        });

        var snapshot = store.GetTelemetrySnapshot();
        Assert.Single(snapshot);
        Assert.Equal("sample", snapshot[0].Kind);
        Assert.NotNull(snapshot[0].Payload);
        Assert.Equal((byte)2, snapshot[0].Payload!.Shape);
        Assert.Equal(14f, snapshot[0].Payload.CpuMs);
    }
}
