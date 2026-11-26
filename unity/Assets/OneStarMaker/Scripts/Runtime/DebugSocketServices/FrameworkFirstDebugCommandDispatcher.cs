#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.DebugSocket;
using OneStarMaker.Runtime.DebugCommands;

namespace OneStarMaker.Runtime.DebugSocketServices
{
    /// <summary>
    /// Framework が登録したコマンドの名前だけを自分で処理し、それ以外を Game の dispatcher へそのまま渡す dispatcher。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 振り分けは登録した名前の Ordinal の完全一致だけで行う。接頭辞（<c>osm.</c>）では振り分けないので、未登録の <c>osm.*</c> や
    /// 大小文字の違う名前は Game へ渡る。Game が同じ名前を登録していても、ここでは Framework の名前が先に引かれる。
    /// </para>
    /// <para>
    /// Framework の名前でない command（null の command、事前に cancel 済みの token を含む）は、同じ command と token で Game へ 1 回だけ渡し、
    /// その結果と例外をそのまま返す。ここで処理すると Game のコマンドの今の文言や挙動が変わるためである。
    /// Framework の名前で事前に cancel 済みなら、内部の <see cref="CatalogDebugCommandDispatcher"/> の固定の失敗
    /// （<c>Debug command was canceled.</c>）を返し、Game へは渡さない。Game へ渡すと「未登録」など別の文言が返り、
    /// Framework のコマンドの結果として誤読されるためである。
    /// </para>
    /// <para>
    /// 寿命は持ち主の DebugSocketService（Play 1 回）と同じ。登録は初期化の中だけで行い、利用中の登録と並行アクセスは扱わない。
    /// handler の例外は伝播させる（socket の router が失敗の envelope にする）。この型はコマンドの中身（Scene など）を知らない。
    /// </para>
    /// </remarks>
    internal sealed class FrameworkFirstDebugCommandDispatcher : IDebugCommandDispatcher
    {
        private readonly IDebugCommandDispatcher _game;
        private readonly DebugCommandCatalog _catalog = new();
        private readonly CatalogDebugCommandDispatcher _framework;

        // DebugCommandCatalog に名前の有無を問う口が無く、catalog は変えない方針なので、登録に成功した名前をここでも持つ。
        private readonly HashSet<string> _names = new(StringComparer.Ordinal);

        public FrameworkFirstDebugCommandDispatcher(IDebugCommandDispatcher game)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _framework = new CatalogDebugCommandDispatcher(_catalog);
        }

        /// <summary>Framework のコマンドを登録する。初期化の中だけで呼ぶ。名前と handler の検証は catalog に従う。</summary>
        internal void Register(string name, DebugCommandHandler handler)
        {
            _catalog.Register(name, handler);
            _names.Add(name);
        }

        public UniTask<DebugCommandResultEnvelopeV1> DispatchAsync(
            DebugCommandEnvelopeV1 command,
            CancellationToken cancellationToken)
        {
            // null の command と名前が null の command も、今どおり Game に判断させる（Game の dispatcher が null をどう扱うかを変えない）。
            if (command == null || command.CommandType == null || !_names.Contains(command.CommandType))
            {
                return _game.DispatchAsync(command!, cancellationToken);
            }

            return _framework.DispatchAsync(command, cancellationToken);
        }
    }
}
