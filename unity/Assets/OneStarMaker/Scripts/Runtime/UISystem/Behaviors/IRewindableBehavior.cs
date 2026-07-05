#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Runtime.UISystem.Behaviors
{
    /// <summary>
    /// 割り込みポリシー <see cref="InterruptPolicy.Rewind"/> で逆再生可能な Behavior の契約。
    /// </summary>
    public interface IRewindableBehavior
    {
        /// <summary>
        /// 現在の進行率から開始状態へ逆再生する。
        /// </summary>
        /// <param name="context">実行コンテキスト。</param>
        /// <param name="progress">進行率（0〜1）。Runner が経過時間から近似算出する。</param>
        /// <param name="ct">キャンセルトークン。</param>
        UniTask RewindAsync(UIBehaviorContext context, float progress, CancellationToken ct);
    }
}
