#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>Completed 以外の値は、副作用の再試行なしで失敗完了として扱う。</summary>
    public enum ScriptCommandResult : byte
    {
        Rejected = 0,
        Completed = 1,
    }
}
