#nullable enable

using System;
using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// DebugSocket 上の共通 envelope。
    ///
    /// <para>
    /// payload の中身は message type ごとの MessagePack DTO だが、
    /// まず外側の envelope を読めば「これは何の種類のメッセージか」を判断できる。
    /// </para>
    /// </summary>
    [MessagePackObject]
    public sealed class DebugSocketEnvelopeV1
    {
        /// <summary>wire contract のバージョン。</summary>
        [Key(0)]
        public int SchemaVersion { get; set; } = 1;

        /// <summary>メッセージ種別。</summary>
        [Key(1)]
        public int MessageType { get; set; }

        /// <summary>
        /// 呼び出し相関用の request ID。
        /// コマンドと結果を結び付けるときに使う。
        /// </summary>
        [Key(2)]
        public string? RequestId { get; set; }

        /// <summary>
        /// メッセージ本体。
        /// 中身は message type に応じた別 DTO を MessagePack 化した byte[]。
        /// </summary>
        [Key(3)]
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }
}
