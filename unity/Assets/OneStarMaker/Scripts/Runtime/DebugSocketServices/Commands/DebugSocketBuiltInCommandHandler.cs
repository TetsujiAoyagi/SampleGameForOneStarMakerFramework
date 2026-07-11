#nullable enable

using System;
using System.Globalization;
using OneStarMaker.Foundation.DebugSocket;

namespace OneStarMaker.Runtime.DebugSocketServices.Commands
{
    /// <summary>
    /// DebugSocket サービス自身が提供する built-in command の応答生成。
    /// dispatcher より先に実行して、アプリ固有 dispatcher が未実装でも疎通確認できる。
    /// </summary>
    internal static class DebugSocketBuiltInCommandHandler
    {
        public static bool TryHandle(
            DebugCommandEnvelopeV1 command,
            DebugSocketService.RuntimeDiagnosticsSnapshot diagnosticsSnapshot,
            out DebugCommandResultEnvelopeV1 result)
        {
            if (string.Equals(command.CommandType, "ping", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command.CommandType, "debugsocket.ping", StringComparison.OrdinalIgnoreCase))
            {
                result = new DebugCommandResultEnvelopeV1
                {
                    Success = true,
                    Message = "DebugSocket ping succeeded.",
                    PayloadJson = string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"service\":\"debugsocket\",\"status\":\"ok\",\"timestampUnixTimeMilliseconds\":{0}}}",
                        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()),
                };
                return true;
            }

            if (string.Equals(command.CommandType, "runtime-diagnostics", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command.CommandType, "debugsocket.runtime-diagnostics", StringComparison.OrdinalIgnoreCase))
            {
                var sessionIdJson = diagnosticsSnapshot.SessionId == null
                    ? "null"
                    : $"\"{EscapeJsonString(diagnosticsSnapshot.SessionId)}\"";
                var lastStartErrorJson = diagnosticsSnapshot.LastStartError == null
                    ? "null"
                    : $"\"{EscapeJsonString(diagnosticsSnapshot.LastStartError)}\"";

                result = new DebugCommandResultEnvelopeV1
                {
                    Success = true,
                    Message = "Runtime diagnostics snapshot captured.",
                    PayloadJson = string.Format(
                        CultureInfo.InvariantCulture,
                        "{{\"transportMode\":\"{0}\",\"configuredEndpoint\":\"{1}\",\"listenerPrefix\":\"{2}\",\"autoStart\":{3},\"isRunning\":{4},\"hasActiveSession\":{5},\"sessionId\":{6},\"pendingQueueLength\":{7},\"maxQueueLength\":{8},\"droppedBeforeSessionCount\":{9},\"droppedQueueOverflowCount\":{10},\"lastStartError\":{11}}}",
                        EscapeJsonString(diagnosticsSnapshot.TransportMode),
                        EscapeJsonString(diagnosticsSnapshot.ConfiguredEndpoint),
                        EscapeJsonString(diagnosticsSnapshot.ListenerPrefix),
                        diagnosticsSnapshot.AutoStart ? "true" : "false",
                        diagnosticsSnapshot.IsRunning ? "true" : "false",
                        diagnosticsSnapshot.HasActiveSession ? "true" : "false",
                        sessionIdJson,
                        diagnosticsSnapshot.PendingQueueLength,
                        diagnosticsSnapshot.MaxQueueLength,
                        diagnosticsSnapshot.DroppedBeforeSessionCount,
                        diagnosticsSnapshot.DroppedQueueOverflowCount,
                        lastStartErrorJson),
                };
                return true;
            }

            result = new DebugCommandResultEnvelopeV1();
            return false;
        }

        // v1 では command ごとの payload 契約を serializer 依存で固定しないため、
        // 既存クライアントが期待するプロパティ名・順序・null 表現を手組み JSON で維持する。
        private static string EscapeJsonString(string value)
        {
            return value
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }
    }
}
