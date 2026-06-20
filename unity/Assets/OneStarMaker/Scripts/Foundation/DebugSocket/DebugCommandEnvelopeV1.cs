#nullable enable

using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// 外部ツールから Unity 側へ送るデバッグコマンド。
    /// v1 では command type と JSON 文字列 payload の最小構成にとどめる。
    /// </summary>
    [MessagePackObject]
    public sealed class DebugCommandEnvelopeV1
    {
        /// <summary>呼び出し相関に使うリクエスト ID。</summary>
        [Key(0)]
        public string RequestId { get; set; } = string.Empty;

        /// <summary>コマンド種別。例: reload-scene, set-timescale</summary>
        [Key(1)]
        public string CommandType { get; set; } = string.Empty;

        /// <summary>
        /// コマンド固有の引数。
        /// v1 は payload schema を早く固定しすぎないため JSON 文字列で保持する。
        /// </summary>
        [Key(2)]
        public string PayloadJson { get; set; } = string.Empty;
    }
}
