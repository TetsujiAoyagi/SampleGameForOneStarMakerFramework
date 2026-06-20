#nullable enable

using System;
using System.Buffers;
using System.Buffers.Binary;
using MessagePack;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// Unity 側 DebugSocket protocol と互換の framed MessagePack helper。
///
/// <para>
/// WebSocket 自体は message 境界を持つが、logger の stream 実装とも共存しやすくするため、
/// v1 では常に [4byte little-endian length][MessagePack(DebugSocketEnvelopeV1)] へ統一している。
/// </para>
/// </summary>
public static class DebugSocketProtocol
{
    private static readonly MessagePackSerializerOptions SerializerOptions = MessagePackSerializerOptions.Standard;

    /// <summary>
    /// payload DTO を共通 envelope へ包み、framed binary を返す。
    /// 送信側の上位レイヤは「どの message type で送るか」だけを意識すればよい。
    /// </summary>
    public static byte[] SerializeMessage<TPayload>(
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId = null)
    {
        var payloadBytes = payload == null
            ? Array.Empty<byte>()
            : MessagePackSerializer.Serialize(payload, SerializerOptions);

        var envelope = new DebugSocketEnvelopeV1
        {
            MessageType = (int)messageType,
            RequestId = requestId,
            Payload = payloadBytes,
        };

        var envelopeBytes = MessagePackSerializer.Serialize(envelope, SerializerOptions);
        var framed = new byte[sizeof(int) + envelopeBytes.Length];
        BinaryPrimitives.WriteInt32LittleEndian(framed.AsSpan(0, sizeof(int)), envelopeBytes.Length);
        envelopeBytes.CopyTo(framed.AsSpan(sizeof(int)));
        return framed;
    }

    /// <summary>
    /// payload DTO を framed binary として <see cref="IBufferWriter{T}"/> へ直接書く。
    /// sender 側の writer-based 実装と receiver 側 contract helper の API 面を揃えるために用意する。
    /// </summary>
    public static void SerializeMessage<TPayload>(
        IBufferWriter<byte> writer,
        DebugSocketMessageType messageType,
        TPayload payload,
        string? requestId = null)
    {
        if (writer == null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        var payloadBytes = payload == null
            ? Array.Empty<byte>()
            : MessagePackSerializer.Serialize(payload, SerializerOptions);

        var envelope = new DebugSocketEnvelopeV1
        {
            MessageType = (int)messageType,
            RequestId = requestId,
            Payload = payloadBytes,
        };

        var envelopeBytes = MessagePackSerializer.Serialize(envelope, SerializerOptions);
        var prefix = writer.GetSpan(sizeof(int));
        BinaryPrimitives.WriteInt32LittleEndian(prefix[..sizeof(int)], envelopeBytes.Length);
        writer.Advance(sizeof(int));
        WriteBytes(writer, envelopeBytes);
    }

    /// <summary>
    /// framed binary を envelope へ戻す。
    /// 不正 framing / deserialize failure は false を返し、受信 loop を落とさず扱えるようにする。
    /// </summary>
    public static bool TryDeserializeEnvelope(ReadOnlyMemory<byte> framedMessage, out DebugSocketEnvelopeV1? envelope)
    {
        envelope = null;
        if (framedMessage.Length < sizeof(int))
        {
            return false;
        }

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(framedMessage.Span[..sizeof(int)]);
        if (payloadLength <= 0 || framedMessage.Length != sizeof(int) + payloadLength)
        {
            return false;
        }

        try
        {
            envelope = MessagePackSerializer.Deserialize<DebugSocketEnvelopeV1>(
                framedMessage[sizeof(int)..(sizeof(int) + payloadLength)],
                SerializerOptions);
            return envelope != null;
        }
        catch
        {
            envelope = null;
            return false;
        }
    }

    /// <summary>
    /// envelope 内 payload を指定 DTO として復号する。
    /// message type 判定は呼び出し側が先に行う前提。
    /// </summary>
    public static bool TryDeserializePayload<TPayload>(DebugSocketEnvelopeV1 envelope, out TPayload? payload)
    {
        payload = default;
        if (envelope.Payload == null)
        {
            return false;
        }

        try
        {
            payload = MessagePackSerializer.Deserialize<TPayload>(envelope.Payload, SerializerOptions);
            return true;
        }
        catch
        {
            payload = default;
            return false;
        }
    }

    private static void WriteBytes(IBufferWriter<byte> writer, ReadOnlySpan<byte> bytes)
    {
        var destination = writer.GetSpan(bytes.Length);
        bytes.CopyTo(destination);
        writer.Advance(bytes.Length);
    }
}
