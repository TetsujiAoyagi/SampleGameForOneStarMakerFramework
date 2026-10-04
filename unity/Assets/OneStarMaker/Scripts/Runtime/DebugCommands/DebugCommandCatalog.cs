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
    /// <remarks>
    /// App の composition root が初期登録後に同じ instance を利用者へ渡す。利用中の登録と並行アクセスは非対応。
    /// handler の依存は取得・破棄しない。closure は App 寿命の依存だけを保持し、Scene 所有オブジェクトは登録しない。
    /// App 終了時は所有者が新規呼出しを止めて参照を破棄する。Unity API を使う handler は main thread から呼ぶ。
    /// </remarks>
    public sealed class DebugCommandCatalog
    {
        private readonly Dictionary<string, DebugCommandHandler> _handlers;
        private static readonly DebugCommandResult NotRegistered = DebugCommandResult.Fail("Debug command is not registered.");

        public DebugCommandCatalog()
        {
            _handlers = new Dictionary<string, DebugCommandHandler>(StringComparer.Ordinal);
        }

        public int Count => _handlers.Count;

        /// <summary>Ordinal の完全一致名を登録する。空白は trim せず、空白だけの名前も保持する。</summary>
        /// <exception cref="ArgumentException">名前が null または empty。</exception>
        /// <exception cref="ArgumentNullException">handler が null。</exception>
        /// <exception cref="InvalidOperationException">同名が登録済み。</exception>
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

        /// <summary>名前が解決できたかを返す。業務成否は result.Success であり、handler 例外は呼出し側へ伝播する。</summary>
        /// <remarks>名前が null / empty / 未登録なら固定失敗結果。payload は解釈せず、null のみ empty にする。
        /// 同期実行にキャンセル機構はなく、必要な事前判断は呼出し側が行う。</remarks>
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
