#nullable enable

namespace OneStarMaker.Runtime.ScriptSystem
{
    /// <summary>アプリ所有の操作を同期実行する。共通の待機命令は runner が処理する。</summary>
    public interface IScriptCommandHost
    {
        ScriptCommandResult Execute(int commandId, long argument);
    }
}
