#nullable enable

using System;
using System.Buffers;
using System.Buffers.Binary;
using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// DebugSocket の送受信プロトコルをまとめた helper。
    ///
    /// <para>
    /// v1 では WebSocket message 単位で送る場合でも、既存の Stream ベース logger と
    /// きれいに共存させるため、常に [length-prefix][MessagePack envelope] の形へ統一する。
    /// </para>
    /// </summary>
    public static class DebugSocketProtocol
    {
        private static readonly MessagePackSerializerOptions SerializerOptions = MessagePackSerializerOptions.Standard;

        /// <summary>
        /// payload DTO を共通 envelope に包み、stream transport と両立する framed binary を返す。
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
        /// payload DTO を framed binary として <see cref="IBufferWriter{T}"/> へ直接書き出す。
        ///
        /// <para>
        /// 現行 v1 contract は維持したまま、
        /// realtime formatter 側で最後の framed <c>byte[]</c> を作らずに済むようにする。
        /// payload / envelope の serialize 自体は従来どおりでも、
        /// 少なくとも「serialize 後にさらに framed 配列へ再コピーする」段を外せる。
        /// </para>
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
        /// framed binary から共通 envelope を復号する。
        /// <see cref="ReadOnlyMemory{T}"/> を受けることで、受信側が余計な byte[] コピーを作らずに decode できる。
        /// framing 不正時は false を返し、受信側が protocol error として扱えるようにする。
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
        /// envelope payload を指定 DTO として復号する。
        /// </summary>
        public static bool TryDeserializePayload<TPayload>(DebugSocketEnvelopeV1 envelope, out TPayload? payload)
        {
            payload = default;
            if (envelope == null || envelope.Payload == null)
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
}
