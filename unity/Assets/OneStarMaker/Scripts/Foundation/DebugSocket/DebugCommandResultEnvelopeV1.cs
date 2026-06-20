#nullable enable

using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// デバッグコマンド実行結果。
    /// requestId を必ず返し、非同期でも呼び出し元が結果を関連付けられるようにする。
    /// </summary>
    [MessagePackObject]
    public sealed class DebugCommandResultEnvelopeV1
    {
        /// <summary>元のリクエスト ID。</summary>
        [Key(0)]
        public string RequestId { get; set; } = string.Empty;

        /// <summary>実行成否。</summary>
        [Key(1)]
        public bool Success { get; set; }

        /// <summary>人間が読める結果メッセージ。</summary>
        [Key(2)]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 結果の追加データ。
        /// こちらも v1 は JSON 文字列にとどめ、command ごとの契約を硬くしすぎない。
        /// </summary>
        [Key(3)]
        public string PayloadJson { get; set; } = string.Empty;
    }
}
