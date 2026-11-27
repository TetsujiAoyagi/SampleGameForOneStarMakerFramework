#nullable enable

using System;
using OneStarMaker.Runtime.DebugSocketServices;
using OneStarMaker.Runtime.SceneSystem;

// namespace は Bootstrap の既存の file と同じ OneStarMaker.Runtime にする（folder で分けない）。
namespace OneStarMaker.Runtime
{
    /// <summary>
    /// Framework 自身のデバッグコマンドを、Game の dispatcher の前に置くかを決める構成の関数（composition root の一部）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 「Editor の Play でだけ Framework のコマンドを持つ」は composition root の公開の方針であり、catalog や dispatcher の意味ではない。
    /// Player では Game の dispatcher を同じ参照のまま返し、全てのコマンドの結果と文言を今と同じにする。
    /// </para>
    /// <para>
    /// 失敗しない形に保つ: 登録は固定の名前の 1 回ずつで、引数の null は呼出し側（initializer）が渡さない。
    /// ここが Editor でだけ例外を投げると BeforeSceneLoad を止めるので、条件付きの登録や外部入力による登録を足さない。
    /// 新しい Framework のコマンドはここに足す（initializer は触らない）。
    /// </para>
    /// </remarks>
    internal static class FrameworkDebugCommands
    {
        /// <param name="isEditor">Editor の Play か。呼出し側が <c>Application.isEditor</c> を渡す（テストは引数で切り替える）。</param>
        /// <param name="game">Game の dispatcher（<c>CreateDebugCommandDispatcher</c> の戻り値）。</param>
        /// <param name="getSceneDirector">
        /// 実行の時に今の SceneDirector を返す delegate。initializer を通して解決するので、Play をまたいで旧 Play の SceneDirector を捕まえない。
        /// </param>
        internal static IDebugCommandDispatcher Compose(
            bool isEditor,
            IDebugCommandDispatcher game,
            Func<SceneDirector?> getSceneDirector)
        {
            // game が null（派生の override が null を返した）なら包まずに返す。DebugSocketService の constructor が今と同じ
            // ArgumentNullException を投げ、Editor と Player で失敗の場所と文言が変わらない。
            if (!isEditor || game == null)
            {
                return game!;
            }

            var dispatcher = new FrameworkFirstDebugCommandDispatcher(game);
            dispatcher.Register(ScenesDebugCommand.CommandName, ScenesDebugCommand.CreateHandler(getSceneDirector));
            return dispatcher;
        }
    }
}
