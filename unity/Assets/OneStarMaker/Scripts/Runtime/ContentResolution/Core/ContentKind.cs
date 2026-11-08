#nullable enable

namespace OneStarMaker.Runtime.ContentResolution
{
    /// <summary>
    /// 要求の種別。Scene と Object は同じ要求型・同じ規則で解決し、この値だけで区別する。
    /// 0 は「未指定」で、要求にも候補の一致判定にも使えない（default の値を有効な種別と読ませないため）。
    /// </summary>
    public enum ContentKind
    {
        None = 0,
        Scene = 1,
        Object = 2,
    }
}
