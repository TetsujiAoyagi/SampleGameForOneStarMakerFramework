#nullable enable

using System;
using System.Buffers.Binary;
using NUnit.Framework;
using OneStarMaker.Foundation.DebugSocket;

namespace OneStarMaker.Tests.Foundation
{
    /// <summary>
    /// DS-01: DebugSocketService 分割前に固定する protocol 契約。
    /// DebugSocketService が inbound を解釈する際に依存する framing / envelope / payload の往復を検証する。
    /// </summary>
    [TestFixture]
    public sealed class DebugSocketProtocolRoundtripTests
    {
        [Test]
        public void Roundtrip_CapabilityHandshakeHelloEnvelopeV1_PreservesNegotiationFields()
        {
            // 守る契約: CapabilityHello 受信時に schema / capability / message type が欠落せず復元できること。
            // 退行時の障害: handshake 失敗、HierarchySnapshot 未発行、接続即切断。
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

            Assert.IsTrue(
                DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope),
                "CapabilityHello framed message の envelope 復号に失敗");
            Assert.NotNull(envelope);
            Assert.AreEqual((int)DebugSocketMessageType.CapabilityHello, envelope!.MessageType);

            Assert.IsTrue(
                DebugSocketProtocol.TryDeserializePayload(envelope, out CapabilityHandshakeHelloEnvelopeV1? deserialized),
                "CapabilityHello payload の復号に失敗");
            Assert.NotNull(deserialized);
            Assert.AreEqual(1, deserialized!.SchemaVersion);
            Assert.AreEqual(originalHello.ClientName, deserialized.ClientName);
            Assert.AreEqual(originalHello.ClientInstanceId, deserialized.ClientInstanceId);
            Assert.AreEqual(originalHello.MinSchemaVersion, deserialized.MinSchemaVersion);
            Assert.AreEqual(originalHello.MaxSchemaVersion, deserialized.MaxSchemaVersion);
            Assert.AreEqual(originalHello.SupportedCapabilities, deserialized.SupportedCapabilities);
            Assert.AreEqual(originalHello.SupportedMessageTypes, deserialized.SupportedMessageTypes);
        }

        [Test]
        public void Roundtrip_DebugCommandEnvelopeV1_PreservesEnvelopeAndPayloadRequestId()
        {
            // 守る契約: DebugCommand 受信時に envelope.RequestId と payload.RequestId の両方が相関用に保持されること。
            // 退行時の障害: CommandResult の requestId 不一致、built-in command の応答がクライアントへ届かない。
            const string requestId = "req-ds01-command-42";
            var originalCommand = new DebugCommandEnvelopeV1
            {
                RequestId = requestId,
                CommandType = "ping",
                PayloadJson = "{\"echo\":\"ds-01\"}",
            };

            var framed = DebugSocketProtocol.SerializeMessage(
                DebugSocketMessageType.DebugCommand,
                originalCommand,
                requestId: requestId);

            Assert.IsTrue(
                DebugSocketProtocol.TryDeserializeEnvelope(framed, out var envelope),
                "DebugCommand framed message の envelope 復号に失敗");
            Assert.NotNull(envelope);
            Assert.AreEqual((int)DebugSocketMessageType.DebugCommand, envelope!.MessageType);
            Assert.AreEqual(requestId, envelope.RequestId);

            Assert.IsTrue(
                DebugSocketProtocol.TryDeserializePayload(envelope, out DebugCommandEnvelopeV1? deserialized),
                "DebugCommand payload の復号に失敗");
            Assert.NotNull(deserialized);
            Assert.AreEqual(originalCommand.RequestId, deserialized!.RequestId);
            Assert.AreEqual(originalCommand.CommandType, deserialized.CommandType);
            Assert.AreEqual(originalCommand.PayloadJson, deserialized.PayloadJson);
        }

        [Test]
        public void TryDeserializeEnvelope_EmptyFrame_ReturnsFalse()
        {
            // 守る契約: framing 不正時は例外を投げず false を返し、受信側が protocol-error へ分岐できること。
            // 退行時の障害: 受信 loop の未処理例外、接続断、ServiceStatus 未通知。
            var result = DebugSocketProtocol.TryDeserializeEnvelope(Array.Empty<byte>(), out var envelope);

            Assert.IsFalse(result);
            Assert.IsNull(envelope);
        }

        [Test]
        public void TryDeserializeEnvelope_LengthMismatch_ReturnsFalse()
        {
            // 守る契約: length-prefix と実 payload 長の不一致は拒否されること。
            // 退行時の障害: 部分フレームの誤解釈、後続メッセージの連鎖破損。
            var malformed = new byte[4 + 5];
            BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(0, 4), 10);

            var result = DebugSocketProtocol.TryDeserializeEnvelope(malformed, out var envelope);

            Assert.IsFalse(result);
            Assert.IsNull(envelope);
        }

        [Test]
        public void TryDeserializeEnvelope_CorruptMessagePackPayload_ReturnsFalse()
        {
            // 守る契約: framing は正しくても envelope が MessagePack として壊れていれば拒否されること。
            // 退行時の障害: 不正バイト列の誤受理、以降の payload decode 失敗の連鎖。
            var malformed = new byte[4 + 8];
            BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(0, 4), 8);
            for (var i = 4; i < malformed.Length; i++)
            {
                malformed[i] = (byte)(i * 17 % 256);
            }

            var result = DebugSocketProtocol.TryDeserializeEnvelope(malformed, out var envelope);

            Assert.IsFalse(result);
            Assert.IsNull(envelope);
        }
    }
}
