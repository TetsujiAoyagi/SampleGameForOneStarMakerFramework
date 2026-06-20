#nullable enable

using System;
using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// DebugStudio 側から届く capability hello。
    /// 接続直後に「viewer が何を理解できるか」を Unity 側へ知らせる。
    /// </summary>
    [MessagePackObject]
    public sealed class CapabilityHandshakeHelloEnvelopeV1
    {
        [Key(0)]
        public int SchemaVersion { get; set; } = 1;

        [Key(1)]
        public string ClientName { get; set; } = string.Empty;

        [Key(2)]
        public string ClientInstanceId { get; set; } = string.Empty;

        [Key(3)]
        public int MinSchemaVersion { get; set; } = 1;

        [Key(4)]
        public int MaxSchemaVersion { get; set; } = 1;

        [Key(5)]
        public DebugStudioCapability SupportedCapabilities { get; set; } = DebugStudioCapability.None;

        [Key(6)]
        public int[] SupportedMessageTypes { get; set; } = Array.Empty<int>();
    }
}
