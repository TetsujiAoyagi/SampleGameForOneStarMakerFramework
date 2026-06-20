#nullable enable

using MessagePack;

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// サービス自身の状態通知。
    /// 接続確立、切断、protocol error など「ログでもテレメトリでもない管理イベント」に使う。
    /// </summary>
    [MessagePackObject]
    public sealed class DebugSocketServiceStatusEnvelopeV1
    {
        [Key(0)]
        public string Status { get; set; } = string.Empty;

        [Key(1)]
        public string Message { get; set; } = string.Empty;

        [Key(2)]
        public long TimestampUnixTimeMilliseconds { get; set; }
    }
}
