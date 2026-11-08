#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 記録の到達度。現在の loader は <see cref="Traceable"/> までしか書かない。
    /// <see cref="Reproducible"/> は意図的な先行宣言で、再現に要る情報を記録する後続の作業が使う。
    /// 0 は意図的に未定義にし、default の値で到達度を主張させない。
    /// </summary>
    public enum ContentTraceability
    {
        /// <summary>どの要求に、どの供給元が何と答え、どの候補をどの根拠で採ったかを説明できる。</summary>
        Traceable = 1,

        /// <summary>同じ内容を再取得・再現できる。</summary>
        Reproducible = 2,
    }
}
