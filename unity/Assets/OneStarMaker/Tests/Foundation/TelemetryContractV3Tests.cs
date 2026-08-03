#nullable enable

using NUnit.Framework;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// Contract v3 の受け入れ契約テスト。
    /// 「sample に elapsed を意味として持たせない」「Scene span の payload に cpu を載せない」を固定する。
    /// </summary>
    [TestFixture]
    public sealed class TelemetryContractV3Tests
    {
        [Test]
        public void InferKind_ProfilerSummary_IsSample()
        {
            Assert.AreEqual(TelemetryKind.Sample, TelemetryKindRules.InferKind(TelemetryStartType.ProfilerSummary));
            Assert.AreEqual(TelemetryKind.Event, TelemetryKindRules.InferKind(TelemetryStartType.GcSpike));
            Assert.AreEqual(TelemetryKind.Span, TelemetryKindRules.InferKind(TelemetryStartType.SceneLoad));
        }

        [Test]
        public void FramePayload_GpuUnavailable_OmitsGpuMsOnWire()
        {
            var payload = TelemetryPayload.ForFrameSample(
                fps: 60f,
                cpuMs: 14f,
                gpuMs: 8f,
                gpuAvailable: false,
                managedBytes: 1000,
                nativeBytes: 2000);

            var wire = DebugTelemetryPayloadV1.FromPayload(payload);
            Assert.IsNotNull(wire);
            Assert.AreEqual(14f, wire!.CpuMs);
            Assert.IsNull(wire.GpuMs);
            Assert.AreEqual(false, wire.GpuAvailable);
        }

        [Test]
        public void SceneSpanEnvelope_HasTimingMemoryWithoutCpuFieldsInPayload()
        {
            var payload = TelemetryPayload.ForTimingMemory(
                managedBeforeBytes: 100,
                nativeBeforeBytes: 200,
                managedAfterBytes: 150,
                nativeAfterBytes: 220,
                targetIdentity: "Cell_0_0");

            var record = new TelemetryRecord(
                traceId: 1,
                spanId: 2,
                parentSpanId: -1,
                name: TelemetryStartType.SceneLoad,
                startTimestampUtcTicks: 10,
                endTimestampUtcTicks: 20,
                elapsedMs: 12.5,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: new Metadata(managedMem: 150, nativeMem: 220),
                kind: TelemetryKind.Span,
                payload: payload);

            var envelope = DebugTelemetryEnvelopeV1.FromRecord(record);
            Assert.AreEqual(3, envelope.SchemaVersion);
            Assert.AreEqual("span", envelope.Kind);
            Assert.IsNotNull(envelope.Payload);
            Assert.AreEqual("Cell_0_0", envelope.Payload!.TargetIdentity);
            Assert.AreEqual(50, envelope.Payload.ManagedDeltaBytes);
            // payload 側に cpu/gpu キーを持たない（null）
            Assert.IsNull(envelope.Payload.CpuMs);
            Assert.IsNull(envelope.Payload.GpuMs);
        }

        [Test]
        public void SampleRecord_DefaultsKindFromName()
        {
            var record = new TelemetryRecord(
                traceId: 1,
                spanId: 2,
                parentSpanId: -1,
                name: TelemetryStartType.ProfilerSummary,
                startTimestampUtcTicks: 1,
                endTimestampUtcTicks: 1,
                elapsedMs: 0,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: default);

            Assert.AreEqual(TelemetryKind.Sample, record.Kind);
            Assert.AreEqual("sample", DebugTelemetryEnvelopeV1.FromRecord(record).Kind);
        }

        [Test]
        public void InferKind_CameraSystemSnapshot_IsSample()
        {
            Assert.AreEqual(
                TelemetryKind.Sample,
                TelemetryKindRules.InferKind(TelemetryStartType.CameraSystemSnapshot));
        }

        [Test]
        public void CameraCountersPayload_RoundtripsOnWire()
        {
            var payload = TelemetryPayload.ForCameraCounters(3, 2, 1, 4);
            var record = new TelemetryRecord(
                traceId: 1,
                spanId: 2,
                parentSpanId: -1,
                name: TelemetryStartType.CameraSystemSnapshot,
                startTimestampUtcTicks: 1,
                endTimestampUtcTicks: 1,
                elapsedMs: 0,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: default,
                kind: TelemetryKind.Sample,
                payload: payload);

            var envelope = DebugTelemetryEnvelopeV1.FromRecord(record);
            Assert.AreEqual("sample", envelope.Kind);
            Assert.IsNotNull(envelope.Payload);
            Assert.AreEqual((byte)TelemetryPayloadShape.CameraCounters, envelope.Payload!.Shape);
            Assert.AreEqual(3, envelope.Payload.CameraTotalViewCount);
            Assert.AreEqual(4, envelope.Payload.CameraMaxStackDepthTotal);
        }

        [Test]
        public void TimingMemoryPayload_RoundtripsDeltaAndStage()
        {
            var payload = TelemetryPayload.ForTimingMemory(100, 200, 150, 250, stage: "BeforeSceneLoad");
            var record = new TelemetryRecord(
                traceId: 1,
                spanId: 2,
                parentSpanId: -1,
                name: TelemetryStartType.AppStartup,
                startTimestampUtcTicks: 1,
                endTimestampUtcTicks: 2,
                elapsedMs: 10,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Summary,
                metadata: new Metadata(managedMem: 150, nativeMem: 250),
                payload: payload);

            var framed = DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.Telemetry,
                DebugTelemetryEnvelopeV1.FromRecord(record));
            Assert.IsTrue(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var decoded));
            Assert.IsTrue(DebugSocketProtocol.TryDeserializePayload(decoded!, out DebugTelemetryEnvelopeV1? wire));
            Assert.AreEqual("BeforeSceneLoad", wire!.Payload!.Stage);
            Assert.AreEqual(50, wire.Payload.ManagedDeltaBytes);
            Assert.AreEqual(50, wire.Payload.NativeDeltaBytes);
            Assert.IsNull(wire.Payload.CpuMs);
        }

        [Test]
        public void FramePayload_RoundtripsCpuMs()
        {
            var payload = TelemetryPayload.ForFrameSample(60f, 14f, 8f, gpuAvailable: true, 1000, 2000);
            var wire = DebugTelemetryPayloadV1.FromPayload(payload);
            Assert.AreEqual(14f, wire!.CpuMs);
            Assert.AreEqual(8f, wire.GpuMs);
            Assert.AreEqual(60f, wire.Fps);
        }
    }
}
