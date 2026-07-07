#nullable enable

using System;
using System.Buffers;
using System.Buffers.Binary;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Contracts.Tests;

/// <summary>
/// DebugSocketProtocol と envelope serialize/deserialize の往復安全性を検証するテスト群。
///
/// <para>
/// 正常系の往復、不正 framing、壊れた payload 等、実運用で遭遇し得る失敗ケースを重点的に扱う。
/// Exhaustive fuzzing は行わず、意味のある境界条件と実用的な破損パターンに注力。
/// </para>
/// </summary>
public sealed class ProtocolRoundtripTests
{
    #region 正常系往復テスト (Round-trip success cases)

    [Fact]
    public void シンプルなLogEnvelopeの往復が成功する()
    {
        // ログエントリの payload を envelope に包み、framing して送信
        var originalLog = new LogEnvelopeV1
        {
            ApplicationName = "TestApp",
            TimestampUnixTimeMilliseconds = 1234567890123L,
            Category = "Test.Category",
            LogLevel = 2,
            Message = "テストログメッセージ",
            ThreadId = 42,
            ThreadName = "MainThread",
        };

        var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, originalLog, requestId: null);

        // 受信側で framing を剥がし、envelope を取り出す
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.Equal((int)DebugSocketMessageType.Log, envelope.MessageType);
        Assert.Null(envelope.RequestId);

        // envelope 内 payload を LogEnvelopeV1 として復号
        Assert.True(DebugSocketProtocol.TryDeserializePayload<LogEnvelopeV1>(envelope, out var deserializedLog));
        Assert.NotNull(deserializedLog);
        Assert.Equal(originalLog.ApplicationName, deserializedLog.ApplicationName);
        Assert.Equal(originalLog.TimestampUnixTimeMilliseconds, deserializedLog.TimestampUnixTimeMilliseconds);
        Assert.Equal(originalLog.Category, deserializedLog.Category);
        Assert.Equal(originalLog.LogLevel, deserializedLog.LogLevel);
        Assert.Equal(originalLog.Message, deserializedLog.Message);
        Assert.Equal(originalLog.ThreadId, deserializedLog.ThreadId);
        Assert.Equal(originalLog.ThreadName, deserializedLog.ThreadName);
    }

    [Fact]
    public void RequestIdを持つDebugCommandEnvelopeの往復が成功する()
    {
        // コマンド送信時は必ず requestId を付与
        var originalCommand = new DebugCommandEnvelopeV1
        {
            RequestId = "req-12345",
            CommandType = "Hierarchy.Refresh",
            PayloadJson = "{\"force\":true}",
        };

        var framed = DebugSocketProtocol.SerializeMessage(
            DebugSocketMessageType.DebugCommand,
            originalCommand,
            requestId: originalCommand.RequestId);

        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal((int)DebugSocketMessageType.DebugCommand, envelope.MessageType);
        Assert.Equal("req-12345", envelope.RequestId);

        Assert.True(DebugSocketProtocol.TryDeserializePayload<DebugCommandEnvelopeV1>(envelope, out var deserialized));
        Assert.NotNull(deserialized);
        Assert.Equal(originalCommand.RequestId, deserialized.RequestId);
        Assert.Equal(originalCommand.CommandType, deserialized.CommandType);
        Assert.Equal(originalCommand.PayloadJson, deserialized.PayloadJson);
    }

    [Fact]
    public void CapabilityHandshakeHelloの往復が成功する()
    {
        // Handshake hello は capability bitset と supported message types を含む
        var originalHello = new CapabilityHandshakeHelloEnvelopeV1
        {
            ClientName = "DebugStudio.WPF",
            ClientInstanceId = Guid.NewGuid().ToString(),
            MinSchemaVersion = 1,
            MaxSchemaVersion = 1,
            SupportedCapabilities = DebugStudioCapability.LogStream | DebugStudioCapability.HierarchySnapshot,
            SupportedMessageTypes = new[] { 1, 2, 3, 8, 9 },
        };

        var framed = DebugSocketProtocol.SerializeMessage(
            DebugSocketMessageType.CapabilityHello,
            originalHello);

        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);

        Assert.True(DebugSocketProtocol.TryDeserializePayload<CapabilityHandshakeHelloEnvelopeV1>(envelope, out var deserialized));
        Assert.NotNull(deserialized);
        Assert.Equal(originalHello.ClientName, deserialized.ClientName);
        Assert.Equal(originalHello.ClientInstanceId, deserialized.ClientInstanceId);
        Assert.Equal(originalHello.MinSchemaVersion, deserialized.MinSchemaVersion);
        Assert.Equal(originalHello.MaxSchemaVersion, deserialized.MaxSchemaVersion);
        Assert.Equal(originalHello.SupportedCapabilities, deserialized.SupportedCapabilities);
        Assert.Equal(originalHello.SupportedMessageTypes, deserialized.SupportedMessageTypes);
    }

    [Fact]
    public void Null_payloadの往復が成功する()
    {
        // payload が空である envelope も正しく serialize/deserialize できる
        var framed = DebugSocketProtocol.SerializeMessage<object?>(
            DebugSocketMessageType.Unknown,
            payload: null);

        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal((int)DebugSocketMessageType.Unknown, envelope.MessageType);
        Assert.NotNull(envelope.Payload);
        Assert.Empty(envelope.Payload);
    }

    [Fact]
    public void Writer直書きSerializeMessageはbyte配列版と同じframingを生成する()
    {
        var payload = new LogEnvelopeV1
        {
            SchemaVersion = 1,
            ApplicationName = "WriterTest",
            TimestampUnixTimeMilliseconds = 456789,
            Category = "Test.Writer",
            LogLevel = 2,
            EventId = 7,
            EventName = "WriterPath",
            Message = "writer-output",
            ThreadId = 5,
            ThreadName = "MainThread",
            MemberName = "Run",
            FilePath = "C:\\src\\Writer.cs",
            LineNumber = 99,
        };

        var viaArray = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, payload, requestId: "req-writer");
        var writer = new ArrayBufferWriter<byte>();
        DebugSocketProtocol.SerializeMessage(writer, DebugSocketMessageType.Log, payload, requestId: "req-writer");

        Assert.Equal(viaArray, writer.WrittenSpan.ToArray());
    }

    #endregion

    #region フレーミング不正テスト (Invalid framing tests)

    [Fact]
    public void 空バイト配列はデシリアライズ失敗を返す()
    {
        var result = DebugSocketProtocol.TryDeserializeEnvelope(Array.Empty<byte>(), out var envelope);

        Assert.False(result);
        Assert.Null(envelope);
    }

    [Fact]
    public void 長さ情報のみでpayloadが無い場合は失敗する()
    {
        // framing header (4 bytes) だけ存在し、後続の envelope payload が無い
        var malformed = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(0, 4), 100);

        var result = DebugSocketProtocol.TryDeserializeEnvelope(malformed, out var envelope);

        Assert.False(result);
        Assert.Null(envelope);
    }

    [Fact]
    public void 長さ情報が負値の場合は失敗する()
    {
        // payload length に負の値を指定
        var malformed = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(0, 4), -1);

        var result = DebugSocketProtocol.TryDeserializeEnvelope(malformed, out var envelope);

        Assert.False(result);
        Assert.Null(envelope);
    }

    [Fact]
    public void 長さ情報がゼロの場合は失敗する()
    {
        // payload length が 0 の場合も、envelope として妥当でないため失敗
        var malformed = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(0, 4), 0);

        var result = DebugSocketProtocol.TryDeserializeEnvelope(malformed, out var envelope);

        Assert.False(result);
        Assert.Null(envelope);
    }

    [Fact]
    public void 実際のpayloadサイズと長さ情報が一致しない場合は失敗する()
    {
        // framing header が 10 bytes を示すが、実際には 5 bytes しか無い
        var malformed = new byte[4 + 5];
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(0, 4), 10);
        // 残り 5 bytes は 0x00 で埋まる

        var result = DebugSocketProtocol.TryDeserializeEnvelope(malformed, out var envelope);

        Assert.False(result);
        Assert.Null(envelope);
    }

    [Fact]
    public void MessagePack形式として壊れているpayloadは失敗する()
    {
        // framing は正しいが、envelope payload が MessagePack として invalid
        var malformed = new byte[4 + 8];
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(0, 4), 8);
        // payload 部分をランダムバイト列で埋める (MessagePack として不正)
        for (int i = 4; i < malformed.Length; i++)
        {
            malformed[i] = (byte)(i * 17 % 256);
        }

        var result = DebugSocketProtocol.TryDeserializeEnvelope(malformed, out var envelope);

        Assert.False(result);
        Assert.Null(envelope);
    }

    #endregion

    #region Payloadデシリアライズ失敗テスト (Payload deserialize failure tests)

    [Fact]
    public void Envelope_payloadが空の場合TryDeserializePayloadは失敗する()
    {
        // envelope 自体は正しいが、payload が空配列
        var envelopeWithEmptyPayload = new DebugSocketEnvelopeV1
        {
            MessageType = (int)DebugSocketMessageType.Log,
            Payload = Array.Empty<byte>(),
        };

        var result = DebugSocketProtocol.TryDeserializePayload<LogEnvelopeV1>(envelopeWithEmptyPayload, out var payload);

        Assert.False(result);
        Assert.Null(payload);
    }

    [Fact]
    public void Envelope_payloadがnullの場合TryDeserializePayloadは失敗する()
    {
        // envelope の Payload が null (通常は起こらないが defensive check)
        var envelopeWithNullPayload = new DebugSocketEnvelopeV1
        {
            MessageType = (int)DebugSocketMessageType.Log,
            Payload = null!,
        };

        var result = DebugSocketProtocol.TryDeserializePayload<LogEnvelopeV1>(envelopeWithNullPayload, out var payload);

        Assert.False(result);
        Assert.Null(payload);
    }

    [Fact]
    public void 異なる型でpayloadをデシリアライズしようとすると失敗する()
    {
        // LogEnvelopeV1 として serialize したが、DebugCommandEnvelopeV1 として deserialize を試みる
        var originalLog = new LogEnvelopeV1
        {
            ApplicationName = "TestApp",
            Message = "Test message",
        };

        var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, originalLog);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);

        // LogEnvelopeV1 の payload を DebugCommandEnvelopeV1 として読もうとすると失敗
        var result = DebugSocketProtocol.TryDeserializePayload<DebugCommandEnvelopeV1>(envelope, out var wrongPayload);

        Assert.False(result);
        Assert.Null(wrongPayload);
    }

    [Fact]
    public void 壊れたMessagePack_payloadをTryDeserializePayloadで扱うと失敗する()
    {
        // envelope は正しいが、payload 内容が MessagePack として不正
        var envelopeWithCorruptPayload = new DebugSocketEnvelopeV1
        {
            MessageType = (int)DebugSocketMessageType.Log,
            Payload = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC, 0xFB },
        };

        var result = DebugSocketProtocol.TryDeserializePayload<LogEnvelopeV1>(envelopeWithCorruptPayload, out var payload);

        Assert.False(result);
        Assert.Null(payload);
    }

    #endregion

    #region 境界条件テスト (Boundary condition tests)

    [Fact]
    public void 非常に長いメッセージ文字列でも往復が成功する()
    {
        // 大きな message を含む payload が正しく serialize/deserialize できる
        var longMessage = new string('あ', 10_000);
        var largeLog = new LogEnvelopeV1
        {
            ApplicationName = "LargePayloadApp",
            Message = longMessage,
        };

        var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, largeLog);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);

        Assert.True(DebugSocketProtocol.TryDeserializePayload<LogEnvelopeV1>(envelope, out var deserialized));
        Assert.NotNull(deserialized);
        Assert.Equal(longMessage, deserialized.Message);
    }

    [Fact]
    public void Unicode文字列を含むpayloadの往復が成功する()
    {
        // 日本語、絵文字、特殊文字を含む payload
        var unicodeLog = new LogEnvelopeV1
        {
            ApplicationName = "🚀UnicodeApp",
            Category = "テスト.カテゴリー",
            Message = "Hello 世界 🌍 special chars: \n\r\t",
        };

        var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, unicodeLog);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);

        Assert.True(DebugSocketProtocol.TryDeserializePayload<LogEnvelopeV1>(envelope, out var deserialized));
        Assert.NotNull(deserialized);
        Assert.Equal(unicodeLog.ApplicationName, deserialized.ApplicationName);
        Assert.Equal(unicodeLog.Category, deserialized.Category);
        Assert.Equal(unicodeLog.Message, deserialized.Message);
    }

    [Fact]
    public void RequestIdが非常に長い文字列でも往復が成功する()
    {
        // requestId として UUID を複数連結した長い文字列を使う
        var longRequestId = string.Concat(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());

        var command = new DebugCommandEnvelopeV1
        {
            RequestId = longRequestId,
            CommandType = "Test.Command",
            PayloadJson = "{}",
        };

        var framed = DebugSocketProtocol.SerializeMessage(
            DebugSocketMessageType.DebugCommand,
            command,
            requestId: longRequestId);

        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal(longRequestId, envelope.RequestId);

        Assert.True(DebugSocketProtocol.TryDeserializePayload<DebugCommandEnvelopeV1>(envelope, out var deserialized));
        Assert.NotNull(deserialized);
        Assert.Equal(longRequestId, deserialized.RequestId);
    }

    #endregion

    #region MessageType整合性テスト (MessageType consistency tests)

    [Fact]
    public void SerializeMessage時に指定したMessageTypeがenvelope内に正しく設定される()
    {
        // 各 MessageType が envelope の MessageType フィールドへ正しく反映される
        var testPayload = new LogEnvelopeV1 { Message = "test" };

        var logFramed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, testPayload);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(logFramed, out var logEnv));
        Assert.Equal((int)DebugSocketMessageType.Log, logEnv!.MessageType);

        var telemetryFramed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Telemetry, testPayload);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(telemetryFramed, out var telemetryEnv));
        Assert.Equal((int)DebugSocketMessageType.Telemetry, telemetryEnv!.MessageType);

        var statusFramed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.ServiceStatus, testPayload);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(statusFramed, out var statusEnv));
        Assert.Equal((int)DebugSocketMessageType.ServiceStatus, statusEnv!.MessageType);
    }

    [Fact]
    public void 未知のMessageType値でも往復が成功する()
    {
        // 将来の拡張で追加される message type が来ても、envelope 自体は deserialize できる
        var unknownTypeValue = 9999;
        var testPayload = new LogEnvelopeV1 { Message = "future type" };

        var framed = DebugSocketProtocol.SerializeMessage((DebugSocketMessageType)unknownTypeValue, testPayload);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal(unknownTypeValue, envelope.MessageType);
    }

    #endregion

    #region SchemaVersion一貫性テスト (SchemaVersion consistency tests)

    [Fact]
    public void EnvelopeのSchemaVersionが常に1である()
    {
        // v1 protocol では envelope の SchemaVersion は必ず 1
        var payload = new LogEnvelopeV1 { Message = "schema check" };
        var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, payload);

        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);
        Assert.Equal(1, envelope.SchemaVersion);
    }

    [Fact]
    public void Payload内のSchemaVersionも往復で保持される()
    {
        // LogEnvelopeV1 の SchemaVersion フィールドが往復で保持される
        var payload = new LogEnvelopeV1
        {
            SchemaVersion = 1,
            Message = "schema preservation test",
        };

        var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Log, payload);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);

        Assert.True(DebugSocketProtocol.TryDeserializePayload<LogEnvelopeV1>(envelope, out var deserialized));
        Assert.NotNull(deserialized);
        Assert.Equal(1, deserialized.SchemaVersion);
    }

    #endregion

    #region Telemetry envelope テスト

    [Fact]
    public void DebugTelemetryEnvelopeV1のcameraFields往復が成功する()
    {
        var original = new DebugTelemetryEnvelopeV1
        {
            Name = "CameraSystemSnapshot",
            TraceId = 100,
            SpanId = 200,
            EndTimestampUtcTicks = 999,
            ElapsedMs = 12.5,
            IsSuccess = true,
            Level = 2,
            SceneFrom = 10,
            SceneTo = 20,
            CameraTotalViewCount = 3,
            CameraAdditionalViewCount = 2,
            CameraBlendingViewCount = 1,
            CameraMaxStackDepthTotal = 4,
            CameraViewId = 5,
            CameraActiveCameraHash = 6,
        };

        var framed = DebugSocketProtocol.SerializeMessage(DebugSocketMessageType.Telemetry, original);
        Assert.True(DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope));
        Assert.NotNull(envelope);

        Assert.True(DebugSocketProtocol.TryDeserializePayload<DebugTelemetryEnvelopeV1>(envelope, out var deserialized));
        Assert.NotNull(deserialized);
        Assert.Equal(original.Name, deserialized!.Name);
        Assert.Equal(original.SceneFrom, deserialized.SceneFrom);
        Assert.Equal(original.SceneTo, deserialized.SceneTo);
        Assert.Equal(original.CameraTotalViewCount, deserialized.CameraTotalViewCount);
        Assert.Equal(original.CameraAdditionalViewCount, deserialized.CameraAdditionalViewCount);
        Assert.Equal(original.CameraBlendingViewCount, deserialized.CameraBlendingViewCount);
        Assert.Equal(original.CameraMaxStackDepthTotal, deserialized.CameraMaxStackDepthTotal);
        Assert.Equal(original.CameraViewId, deserialized.CameraViewId);
        Assert.Equal(original.CameraActiveCameraHash, deserialized.CameraActiveCameraHash);
    }

    #endregion
}
