#nullable enable

using System;
using System.Collections.Generic;

namespace OneStarMaker.Runtime.DebugCommands
{
    /// <summary>
    /// 登録済みデバッグコマンドの目録。
    /// 名前の解決は登録時の辞書で、実行はその委譲を呼ぶだけである。
    /// ソケットの受信側も、別の呼び出し側も、同じ TryExecute を使う。
    /// この型はソケットの封筒もスクリプトの機械も知らない。実行は同期で、呼び出し側のスレッドで行う。
    /// </summary>
    public sealed class DebugCommandCatalog
    {
        private readonly Dictionary<string, DebugCommandHandler> _handlers;
        private static readonly DebugCommandResult NotRegistered = DebugCommandResult.Fail("Debug command is not registered.");

        public DebugCommandCatalog()
        {
            _handlers = new Dictionary<string, DebugCommandHandler>(StringComparer.Ordinal);
        }

        public int Count => _handlers.Count;

        public void Register(string name, DebugCommandHandler handler)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Debug command name is required.", nameof(name));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_handlers.ContainsKey(name))
            {
                throw new InvalidOperationException("Debug command is already registered.");
            }

            _handlers.Add(name, handler);
        }

        public bool TryExecute(string name, string? payloadJson, out DebugCommandResult result)
        {
            if (string.IsNullOrEmpty(name) || !_handlers.TryGetValue(name, out var handler))
            {
                result = NotRegistered;
                return false;
            }

            result = handler(payloadJson ?? string.Empty);
            return true;
        }
    }

    /// <summary>
    /// 1 コマンドの処理。引数は JSON 文字列のまま渡し、この層では解釈しない。
    /// </summary>
    public delegate DebugCommandResult DebugCommandHandler(string payloadJson);
}
