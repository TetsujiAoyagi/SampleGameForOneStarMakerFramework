#nullable enable

using System;

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// 生成したスレッドだけを許可する。シーン API の前に拒否するため、Unity 型には依存しない。
    /// </summary>
    public sealed class DebugGameObjectThreadGate
    {
        private readonly int _threadId;

        public DebugGameObjectThreadGate()
        {
            _threadId = Environment.CurrentManagedThreadId;
        }

        public bool AllowsCaller => _threadId == Environment.CurrentManagedThreadId;
    }
}
