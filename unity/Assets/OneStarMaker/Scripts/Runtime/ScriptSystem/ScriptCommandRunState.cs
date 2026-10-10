#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>単一 Run の観測状態。終端後の再実行には新しい runner が必要。</summary>
    public enum ScriptCommandRunState : byte
    {
        Running = 0,
        Waiting = 1,
        Halted = 2,
        Stopped = 3,
        Failed = 4,
    }
}
