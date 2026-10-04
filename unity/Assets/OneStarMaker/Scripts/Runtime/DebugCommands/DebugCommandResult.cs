#nullable enable

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// デバッグコマンドの結果。経路は持たない。
    /// 外部ツールの封筒へ載せるときも、プロセス内から読むときも、この値を使う。
    /// </summary>
    public readonly struct DebugCommandResult
    {
        public DebugCommandResult(bool success, string? message, string? payloadJson)
        {
            Success = success;
            Message = message ?? string.Empty;
            PayloadJson = payloadJson ?? string.Empty;
        }

        public bool Success { get; }

        public string Message { get; }

        public string PayloadJson { get; }

        public static DebugCommandResult Ok(string? message, string? payloadJson)
        {
            return new DebugCommandResult(true, message, payloadJson);
        }

        public static DebugCommandResult Fail(string? message)
        {
            return new DebugCommandResult(false, message, null);
        }
    }
}
