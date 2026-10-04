#nullable enable

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// デバッグコマンドの結果。経路は持たない。
    /// 外部ツールの封筒へ載せるときも、プロセス内から読むときも、この値を使う。
    /// </summary>
    /// <remarks>コンストラクタは null message / payload を empty にするが、default(struct) の文字列は null になり得る。
    /// envelope adapter は default の文字列も empty へ正規化する。</remarks>
    public readonly struct DebugCommandResult
    {
        public DebugCommandResult(bool success, string? message, string? payloadJson)
        {
            Success = success;
            Message = message ?? string.Empty;
            PayloadJson = payloadJson ?? string.Empty;
        }

        /// <summary>handler が返した業務成否。catalog の名前解決の成否とは独立する。</summary>
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
