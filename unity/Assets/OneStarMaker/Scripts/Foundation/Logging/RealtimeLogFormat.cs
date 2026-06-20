#nullable enable

namespace OneStarMaker.Foundation.Logging
{
    /// <summary>
    /// realtime stream 側に流すログのシリアライズ形式。
    ///
    /// <para>
    /// rolling file は常に JSON のため、この enum は realtime transport のみを切り替える。
    /// </para>
    /// </summary>
    public enum RealtimeLogFormat
    {
        /// <summary>
        /// JSON のまま stream に流す。
        /// 可読性は高いが、帯域効率や受信側処理コストの面では MessagePack より不利。
        /// </summary>
        Json = 0,

        /// <summary>
        /// MessagePack でバイナリ化して stream に流す。
        /// sender/receiver が C# 前提のため、現在の本命フォーマット。
        /// </summary>
        MessagePack = 1,
    }
}
