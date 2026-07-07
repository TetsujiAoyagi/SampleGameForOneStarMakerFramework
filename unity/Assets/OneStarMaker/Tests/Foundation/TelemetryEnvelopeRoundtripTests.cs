#nullable enable

using System;
using System.Collections.Generic;
using NUnit.Framework;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Foundation.Telemetry;

namespace OneStarMaker.Tests.Foundation
{
    [TestFixture]
    public sealed class TelemetryEnvelopeRoundtripTests
    {
        private const long TraceId = unchecked((long)0x1234567890ABCDEF);
        private const long SpanId = unchecked((long)0xFEDCBA0987654321);
        private const long ParentSpanId = unchecked((long)0x1111222233334444);
        private const long StartTicks = 638_000_000_000_000_000;
        private const long EndTicks = 638_000_000_001_000_000;
        private const double ElapsedMs = 42.75;

        [Test]
        public void Roundtrip_AllFields_WithNullTags()
        {
            var record = CreateRecord(tags: null);
            AssertRoundtrip(record, expectedTagBits: null);
        }

        [Test]
        public void Roundtrip_AllFields_WithCombinedTags()
        {
            var tags = TelemetryTagType.ManagedMemoryOver | TelemetryTagType.Bottleneck;
            var record = CreateRecord(tags);
            AssertRoundtrip(record, expectedTagBits: (int)tags);
        }

        [Test]
        public void Roundtrip_SceneRecord_LeavesCameraFieldsUnset()
        {
            var record = CreateRecord(tags: null);
            AssertRoundtrip(record, expectedTagBits: null);
            Assert.AreEqual(-1, record.MetadataValue.CameraTotalViewCount);
            Assert.AreEqual(-1, record.MetadataValue.CameraViewId);
        }

        [Test]
        public void Roundtrip_CameraFields_WithSceneFieldsIndependent()
        {
            var record = new TelemetryRecord(
                traceId: TraceId,
                spanId: SpanId,
                parentSpanId: ParentSpanId,
                name: TelemetryStartType.CameraSystemSnapshot,
                startTimestampUtcTicks: StartTicks,
                endTimestampUtcTicks: EndTicks,
                elapsedMs: ElapsedMs,
                isSuccess: true,
                tags: null,
                level: TelemetryLevel.Verbose,
                metadata: new Metadata(
                    cpuTime: 1.0f,
                    gpuTime: 2.0f,
                    managedMem: 100,
                    nativeMem: 200,
                    sceneFrom: 10,
                    sceneTo: 20,
                    cameraTotalViewCount: 3,
                    cameraAdditionalViewCount: 2,
                    cameraBlendingViewCount: 1,
                    cameraMaxStackDepthTotal: 4,
                    cameraViewId: 5,
                    cameraActiveCameraHash: 6));

            AssertRoundtrip(record, expectedTagBits: null);
        }

        private static TelemetryRecord CreateRecord(TelemetryTagType? tags)
        {
            return new TelemetryRecord(
                traceId: TraceId,
                spanId: SpanId,
                parentSpanId: ParentSpanId,
                name: TelemetryStartType.SceneTransition,
                startTimestampUtcTicks: StartTicks,
                endTimestampUtcTicks: EndTicks,
                elapsedMs: ElapsedMs,
                isSuccess: true,
                tags: tags,
                level: TelemetryLevel.Verbose,
                metadata: new Metadata(
                    cpuTime: 12.5f,
                    gpuTime: 3.25f,
                    managedMem: 1_048_576,
                    nativeMem: 2_097_152,
                    sceneFrom: 10,
                    sceneTo: 20));
        }

        private static void AssertRoundtrip(in TelemetryRecord record, int? expectedTagBits)
        {
            var envelope = DebugTelemetryEnvelopeV1.FromRecord(record);
            var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Telemetry, envelope);

            Assert.IsTrue(
                DebugSocketProtocol.TryDeserializeEnvelope(framed, out var decodedEnvelope),
                "framed message の envelope 復号に失敗");
            Assert.NotNull(decodedEnvelope);
            Assert.AreEqual((int)DebugSocketMessageType.Telemetry, decodedEnvelope!.MessageType);

            Assert.IsTrue(
                DebugSocketProtocol.TryDeserializePayload(decodedEnvelope, out DebugTelemetryEnvelopeV1? payload),
                "Telemetry payload の復号に失敗");
            Assert.NotNull(payload);

            Assert.AreEqual(1, payload!.SchemaVersion);
            Assert.AreEqual(record.TraceId, payload.TraceId);
            Assert.AreEqual(record.SpanId, payload.SpanId);
            Assert.AreEqual(record.ParentSpanId, payload.ParentSpanId);
            Assert.AreEqual(record.Name.ToStartTypeString(), payload.Name);
            Assert.AreEqual(record.StartTimestampUtcTicks, payload.StartTimestampUtcTicks);
            Assert.AreEqual(record.EndTimestampUtcTicks, payload.EndTimestampUtcTicks);
            Assert.AreEqual(record.ElapsedMs, payload.ElapsedMs);
            Assert.AreEqual(record.IsSuccess, payload.IsSuccess);
            Assert.AreEqual((int)record.Level, payload.Level);
            Assert.AreEqual(expectedTagBits, payload.TagBits);
            Assert.AreEqual(record.MetadataValue.CpuTime, payload.CpuTime);
            Assert.AreEqual(record.MetadataValue.GpuTime, payload.GpuTime);
            Assert.AreEqual(record.MetadataValue.ManagedMem, payload.ManagedMem);
            Assert.AreEqual(record.MetadataValue.NativeMem, payload.NativeMem);
            Assert.AreEqual(record.MetadataValue.SceneFrom, payload.SceneFrom);
            Assert.AreEqual(record.MetadataValue.SceneTo, payload.SceneTo);
            Assert.AreEqual(record.MetadataValue.CameraTotalViewCount, payload.CameraTotalViewCount);
            Assert.AreEqual(record.MetadataValue.CameraAdditionalViewCount, payload.CameraAdditionalViewCount);
            Assert.AreEqual(record.MetadataValue.CameraBlendingViewCount, payload.CameraBlendingViewCount);
            Assert.AreEqual(record.MetadataValue.CameraMaxStackDepthTotal, payload.CameraMaxStackDepthTotal);
            Assert.AreEqual(record.MetadataValue.CameraViewId, payload.CameraViewId);
            Assert.AreEqual(record.MetadataValue.CameraActiveCameraHash, payload.CameraActiveCameraHash);
        }
    }
}
