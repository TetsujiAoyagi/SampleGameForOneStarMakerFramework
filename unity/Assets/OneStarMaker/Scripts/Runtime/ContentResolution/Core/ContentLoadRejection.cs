#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// loader が、resolver の採用した候補をロードの前に拒んだ理由。resolver の結果とは別の欄で、resolver は使わない
    /// （候補の選択と実際のロードは別の事実なので、loader の拒否で resolver の成功を「候補が無かった」に書き換えない）。
    /// </summary>
    public enum ContentLoadRejection
    {
        /// <summary>拒んでいない。</summary>
        None = 0,

        /// <summary>loader が実装していないロード経路の候補だった。</summary>
        LoadPathUnsupported = 1,

        /// <summary>
        /// 候補を返した供給元が、候補と実際に読む directory の一致を答えられない（照合の口を持たない）。
        /// 確かめられない候補は読まない（読んだ物と記録が食い違っても気づけないため）。
        /// </summary>
        SourceBindingUnavailable = 2,

        /// <summary>供給元が、候補は今の AssetManagement が読む directory の物ではないと答えた。</summary>
        SourceBindingMismatch = 3,
    }
}
