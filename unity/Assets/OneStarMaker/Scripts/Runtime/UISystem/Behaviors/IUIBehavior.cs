#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// UI 遷移パイプラインの 1 ステップを表す契約。
    /// </summary>
    public interface IUIBehavior
    {
        /// <summary>
        /// 指定コンテキスト上で Behavior を非同期実行する。
        /// </summary>
        /// <param name="context">実行コンテキスト。</param>
        /// <param name="ct">キャンセルトークン。</param>
        UniTask ExecuteAsync(UIBehaviorContext context, CancellationToken ct);
    }
}
