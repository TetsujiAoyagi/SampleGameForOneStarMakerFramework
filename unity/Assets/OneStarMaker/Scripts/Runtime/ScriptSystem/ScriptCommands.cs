#nullable enable

using System;

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>Framework 所有の共通命令。アプリの命令 ID は 0 以上を使う。</summary>
    public static class ScriptCommands
    {
        public const int WaitMillisecondsCommandId = -1;

        public static ScriptInstruction WaitMilliseconds(long milliseconds)
        {
            if (milliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(milliseconds), "Wait duration must be nonnegative.");
            }

            return ScriptInstruction.HostCommand(WaitMillisecondsCommandId, milliseconds);
        }
    }
}
