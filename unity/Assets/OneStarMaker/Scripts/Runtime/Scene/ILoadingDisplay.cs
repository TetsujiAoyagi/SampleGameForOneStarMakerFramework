#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// ローディング表示の抽象。Game 層で実装する。
    /// Canvas オーバーレイでも、専用シーンでも、実装は自由。
    /// </summary>
    public interface ILoadingDisplay
    {
        /// <summary>
        /// 指定モードでローディング表示を開始する。
        /// <see cref="LoadingDisplayType.None"/> の場合は何もしない。
        /// </summary>
        /// <param name="displayType">表示モード。</param>
        /// <param name="ct">キャンセルトークン。</param>
        UniTask Show(LoadingDisplayType displayType, CancellationToken ct);

        /// <summary>
        /// ローディング表示を終了する。
        /// Show が呼ばれていない場合は何もしない。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        UniTask Hide(CancellationToken ct);
    }
}
