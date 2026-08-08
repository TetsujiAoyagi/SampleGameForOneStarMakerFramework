#nullable enable

using System;
using System.IO;
using System.Linq;
using DebugStudio.Contracts.Protocol;
using MessagePack;

namespace DebugStudio.Contracts.Tests;

/// <summary>
/// PROTO-00: Unity / DebugStudio 双方が同じ golden bytes を満たすことを検証する。
/// fixture は生成前の現行実装から採取した。CLI message type 12/13 は含めない。
/// </summary>
public sealed class ProtocolGoldenCrossContractTests
{
    private static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard;

    [Fact]
    public void LogEnvelopeV1_Serialize_MatchesGoldenBytes()
    {
        var expected = LoadHex("log_envelope_v1.hex");
        var actual = MessagePackSerializer.Serialize(CreateGoldenLog(), Options);
        Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(actual));
    }

    [Fact]
    public void LogEnvelopeV1_Deserialize_GoldenBytes_PreservesFields()
    {
        var bytes = LoadHex("log_envelope_v1.hex");
        var decoded = MessagePackSerializer.Deserialize<LogEnvelopeV1>(bytes, Options);
        var expected = CreateGoldenLog();

        Assert.Equal(expected.SchemaVersion, decoded.SchemaVersion);
        Assert.Equal(expected.ApplicationName, decoded.ApplicationName);
        Assert.Equal(expected.TimestampUnixTimeMilliseconds, decoded.TimestampUnixTimeMilliseconds);
        Assert.Equal(expected.Category, decoded.Category);
        Assert.Equal(expected.LogLevel, decoded.LogLevel);
        Assert.Equal(expected.EventId, decoded.EventId);
        Assert.Equal(expected.EventName, decoded.EventName);
        Assert.Equal(expected.Message, decoded.Message);
        Assert.Null(decoded.Exception);
        Assert.Equal(expected.ThreadId, decoded.ThreadId);
        Assert.Equal(expected.ThreadName, decoded.ThreadName);
        Assert.Equal(expected.MemberName, decoded.MemberName);
        Assert.Equal(expected.FilePath, decoded.FilePath);
        Assert.Equal(expected.LineNumber, decoded.LineNumber);
        Assert.Equal(expected.SessionId, decoded.SessionId);
        Assert.Equal(expected.ProducerSequence, decoded.ProducerSequence);
        Assert.Equal(expected.UnityFrameAtEmit, decoded.UnityFrameAtEmit);
        Assert.Equal(expected.TraceId, decoded.TraceId);
        Assert.Equal(expected.SpanId, decoded.SpanId);
        // IgnoreMember Kind は wire に出ないが、LogLevel から導出できる
        Assert.Equal(Schema.LogEntryKind.Information, decoded.Kind);
    }

    [Fact]
    public void DebugTelemetryPayloadV1_Serialize_MatchesGoldenBytes()
    {
        var expected = LoadHex("debug_telemetry_payload_v1.hex");
        var actual = MessagePackSerializer.Serialize(CreateGoldenPayload(), Options);
        Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(actual));
    }

    [Fact]
    public void DebugTelemetryEnvelopeV1_Serialize_MatchesGoldenBytes()
    {
        var expected = LoadHex("debug_telemetry_envelope_v1.hex");
        var actual = MessagePackSerializer.Serialize(CreateGoldenTelemetry(), Options);
        Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(actual));
    }

    [Fact]
    public void DebugTelemetryEnvelopeV1_Deserialize_GoldenBytes_PreservesNestedPayload()
    {
        var bytes = LoadHex("debug_telemetry_envelope_v1.hex");
        var decoded = MessagePackSerializer.Deserialize<DebugTelemetryEnvelopeV1>(bytes, Options);
        var expected = CreateGoldenTelemetry();

        Assert.Equal(expected.SchemaVersion, decoded.SchemaVersion);
        Assert.Equal(expected.TraceId, decoded.TraceId);
        Assert.Equal(expected.SpanId, decoded.SpanId);
        Assert.Equal(expected.ParentSpanId, decoded.ParentSpanId);
        Assert.Equal(expected.Name, decoded.Name);
        Assert.Equal(expected.StartTimestampUtcTicks, decoded.StartTimestampUtcTicks);
        Assert.Equal(expected.EndTimestampUtcTicks, decoded.EndTimestampUtcTicks);
        Assert.Equal(expected.ElapsedMs, decoded.ElapsedMs);
        Assert.Equal(expected.IsSuccess, decoded.IsSuccess);
        Assert.Equal(expected.Level, decoded.Level);
        Assert.Null(decoded.TagBits);
        Assert.Equal(expected.CpuTime, decoded.CpuTime);
        Assert.Equal(expected.GpuTime, decoded.GpuTime);
        Assert.Equal(expected.ManagedMem, decoded.ManagedMem);
        Assert.Equal(expected.NativeMem, decoded.NativeMem);
        Assert.Equal(expected.SceneFrom, decoded.SceneFrom);
        Assert.Equal(expected.SceneTo, decoded.SceneTo);
        Assert.Equal(-1, decoded.CameraTotalViewCount);
        Assert.Equal(expected.SessionId, decoded.SessionId);
        Assert.Equal(expected.ProducerSequence, decoded.ProducerSequence);
        Assert.Equal(expected.UnityFrameAtStart, decoded.UnityFrameAtStart);
        Assert.Equal(expected.UnityFrameAtEnd, decoded.UnityFrameAtEnd);
        Assert.Equal(expected.Kind, decoded.Kind);
        Assert.NotNull(decoded.Payload);
        Assert.Equal(expected.Payload!.Shape, decoded.Payload!.Shape);
        Assert.Equal(expected.Payload.TargetIdentity, decoded.Payload.TargetIdentity);
        Assert.Equal(expected.Payload.Stage, decoded.Payload.Stage);
        Assert.Equal(expected.Payload.ManagedBeforeBytes, decoded.Payload.ManagedBeforeBytes);
        Assert.Equal(expected.Payload.NativeDeltaBytes, decoded.Payload.NativeDeltaBytes);
        Assert.Null(decoded.Payload.Fps);
    }

    [Fact]
    public void DebugSocketEnvelopeV1_FramedLog_MatchesGoldenBytes()
    {
        var expected = LoadHex("framed_log_envelope_v1.hex");
        var actual = DebugSocketProtocol.SerializeMessage(
            DebugSocketMessageType.Log,
            CreateGoldenLog(),
            requestId: "req-proto00");
        Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(actual));
    }

    [Fact]
    public void DebugSocketEnvelopeV1_FramedTelemetry_MatchesGoldenBytes()
    {
        var expected = LoadHex("framed_telemetry_envelope_v1.hex");
        var actual = DebugSocketProtocol.SerializeMessage(
            DebugSocketMessageType.Telemetry,
            CreateGoldenTelemetry(),
            requestId: null);
        Assert.Equal(Convert.ToHexString(expected), Convert.ToHexString(actual));
    }

    [Fact]
    public void FramedLogGolden_RoundtripsThroughProtocolHelpers()
    {
        var framed = LoadHex("framed_log_envelope_v1.hex");
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal(1, envelope!.SchemaVersion);
        Assert.Equal((int)DebugSocketMessageType.Log, envelope.MessageType);
        Assert.Equal("req-proto00", envelope.RequestId);
        Assert.True(DebugSocketProtocol.TryDeserializePayload<LogEnvelopeV1>(envelope, out var log));
        Assert.NotNull(log);
        Assert.Equal("golden-log", log!.Message);
        Assert.Equal(1000L, log.TraceId);
    }

    private static LogEnvelopeV1 CreateGoldenLog() => new()
    {
        SchemaVersion = 1,
        ApplicationName = "OneStarMaker",
        TimestampUnixTimeMilliseconds = 1722902400123,
        Category = "Foundation.DebugSocket",
        LogLevel = 2,
        EventId = 42,
        EventName = "Proto00",
        Message = "golden-log",
        Exception = null,
        ThreadId = 1,
        ThreadName = "Main",
        MemberName = "Emit",
        FilePath = "Assets/Log.cs",
        LineNumber = 10,
        SessionId = "sess-proto00",
        ProducerSequence = 7,
        UnityFrameAtEmit = 120,
        TraceId = 1000,
        SpanId = 2000,
    };

    private static DebugTelemetryPayloadV1 CreateGoldenPayload() => new()
    {
        Shape = 1,
        TargetIdentity = "Cell_0_0",
        Stage = "BeforeSceneLoad",
        ManagedBeforeBytes = 100,
        NativeBeforeBytes = 200,
        ManagedAfterBytes = 150,
        NativeAfterBytes = 250,
        ManagedDeltaBytes = 50,
        NativeDeltaBytes = 50,
    };

    private static DebugTelemetryEnvelopeV1 CreateGoldenTelemetry() => new()
    {
        SchemaVersion = 3,
        TraceId = unchecked((long)0x1234567890ABCDEF),
        SpanId = unchecked((long)0xFEDCBA0987654321),
        ParentSpanId = unchecked((long)0x1111222233334444),
        Name = "SceneLoad",
        StartTimestampUtcTicks = 638000000000000000,
        EndTimestampUtcTicks = 638000000001000000,
        ElapsedMs = 42.75,
        IsSuccess = true,
        Level = 0,
        TagBits = null,
        CpuTime = 12.5f,
        GpuTime = 3.25f,
        ManagedMem = 1048576,
        NativeMem = 2097152,
        SceneFrom = 10,
        SceneTo = 20,
        CameraTotalViewCount = -1,
        CameraAdditionalViewCount = -1,
        CameraBlendingViewCount = -1,
        CameraMaxStackDepthTotal = -1,
        CameraViewId = -1,
        CameraActiveCameraHash = -1,
        SessionId = "sess-proto00",
        ProducerSequence = 3,
        UnityFrameAtStart = 50,
        UnityFrameAtEnd = 55,
        Kind = "span",
        Payload = CreateGoldenPayload(),
    };

    private static byte[] LoadHex(string fileName)
    {
        var path = ResolveFixturePath(fileName);
        var hex = File.ReadAllText(path)
            .Where(c => !char.IsWhiteSpace(c))
            .Aggregate(string.Empty, (acc, c) => acc + c);
        return Convert.FromHexString(hex);
    }

    private static string ResolveFixturePath(string fileName)
    {
        // tests/DebugStudio.Contracts.Tests → repo root → protocol/debugsocket/fixtures/proto00
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "protocol",
                "debugsocket",
                "fixtures",
                "proto00",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"PROTO-00 fixture not found: {fileName}. Expected under protocol/debugsocket/fixtures/proto00/");
    }
}
