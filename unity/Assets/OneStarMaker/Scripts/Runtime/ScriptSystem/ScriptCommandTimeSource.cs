#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>待機に使う入力時間。どちらを選んでも layer の pause を尊重する。</summary>
    public enum ScriptCommandTimeSource : byte
    {
        Scaled = 0,
        Unscaled = 1,
    }
}
