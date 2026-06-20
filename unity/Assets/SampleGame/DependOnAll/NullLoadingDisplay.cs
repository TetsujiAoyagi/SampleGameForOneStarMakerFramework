#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;

namespace SampleGame.DependOnAll
{
    /// <summary>
    /// ローディング表示の Null 実装。Phase 1 では何もしない。
    /// Phase 2 以降で BlackScreen / Indicator の実装に差し替える。
    /// </summary>
    public sealed class NullLoadingDisplay : ILoadingDisplay
    {
        public UniTask Show(LoadingDisplayType displayType, CancellationToken ct)
            => UniTask.CompletedTask;

        public UniTask Hide(CancellationToken ct)
            => UniTask.CompletedTask;
    }
}
