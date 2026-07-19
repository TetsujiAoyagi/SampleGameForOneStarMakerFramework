#nullable enable

using DebugStudio.Contracts.Protocol;
using MessagePack;

namespace DebugStudio.Contracts.Tests;

/// <summary>
/// Unity / DebugStudio 双方の MessagePack DTO が同一 additive key で相関 field を往復できることを固定する。
/// </summary>
public sealed class CorrelationProtocolRoundtripTests
{
    [Fact]
    public void LogEnvelope_相関field付きpayloadの往復が成功する()
    {
        var original = new LogEnvelopeV1
        {
            ApplicationName = "TestApp",
            TimestampUnixTimeMilliseconds = 1234567890123L,
            Category = "Test.Category",
            LogLevel = 2,
            Message = "correlated log",
            ThreadId = 42,
            SessionId = "session-abc",
            ProducerSequence = 7,
            UnityFrameAtEmit = 120,
            TraceId = 1000,
            SpanId = 2000,
        };

        var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, original);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.True(DebugSocketProtocol.TryDeserializePayload(envelope!, out LogEnvelopeV1? deserialized));

        Assert.NotNull(deserialized);
        Assert.Equal(original.SessionId, deserialized!.SessionId);
        Assert.Equal(original.ProducerSequence, deserialized.ProducerSequence);
        Assert.Equal(original.UnityFrameAtEmit, deserialized.UnityFrameAtEmit);
        Assert.Equal(original.TraceId, deserialized.TraceId);
        Assert.Equal(original.SpanId, deserialized.SpanId);
    }

    [Fact]
    public void TelemetryEnvelope_相関field付きpayloadの往復が成功する()
    {
        var original = new DebugTelemetryEnvelopeV1
        {
            Name = "SceneTransition",
            TraceId = 10,
            SpanId = 11,
            ElapsedMs = 42.5,
            IsSuccess = true,
            SessionId = "session-xyz",
            ProducerSequence = 3,
            UnityFrameAtStart = 50,
            UnityFrameAtEnd = 55,
        };

        var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Telemetry, original);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.True(DebugSocketProtocol.TryDeserializePayload(envelope!, out DebugTelemetryEnvelopeV1? deserialized));

        Assert.NotNull(deserialized);
        Assert.Equal(original.SessionId, deserialized!.SessionId);
        Assert.Equal(original.ProducerSequence, deserialized.ProducerSequence);
        Assert.Equal(original.UnityFrameAtStart, deserialized.UnityFrameAtStart);
        Assert.Equal(original.UnityFrameAtEnd, deserialized.UnityFrameAtEnd);
    }

    [MessagePackObject]
    internal sealed class LegacyLogEnvelopeV1
    {
        [Key(0)] public int SchemaVersion { get; set; } = 1;
        [Key(1)] public string ApplicationName { get; set; } = string.Empty;
        [Key(2)] public long TimestampUnixTimeMilliseconds { get; set; }
        [Key(3)] public string Category { get; set; } = string.Empty;
        [Key(4)] public int LogLevel { get; set; }
        [Key(5)] public int EventId { get; set; }
        [Key(6)] public string? EventName { get; set; }
        [Key(7)] public string Message { get; set; } = string.Empty;
        [Key(8)] public string? Exception { get; set; }
        [Key(9)] public int ThreadId { get; set; }
        [Key(10)] public string? ThreadName { get; set; }
        [Key(11)] public string? MemberName { get; set; }
        [Key(12)] public string? FilePath { get; set; }
        [Key(13)] public int LineNumber { get; set; }
    }

    [Fact]
    public void LogEnvelope_旧keyのみpayloadは相関fieldがdefaultになる()
    {
        var legacyBytes = MessagePackSerializer.Serialize(new LegacyLogEnvelopeV1
        {
            ApplicationName = "Legacy",
            Message = "old client",
            LogLevel = 2,
        });

        var current = MessagePackSerializer.Deserialize<LogEnvelopeV1>(legacyBytes);
        Assert.Equal("Legacy", current.ApplicationName);
        Assert.Equal(string.Empty, current.SessionId);
        Assert.Equal(0, current.ProducerSequence);
        Assert.Null(current.UnityFrameAtEmit);
        Assert.Null(current.TraceId);
        Assert.Null(current.SpanId);
    }
}
