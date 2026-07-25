#nullable enable

using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;

namespace OneStarMaker.Tests.SceneSystem.TestDoubles
{
    /// <summary>
    /// ILoadingDisplay のテスト用実装。
    /// Show/Hide の呼び出しを記録する。
    /// </summary>
    public class FakeLoadingDisplay : ILoadingDisplay
    {
        public int ShowCallCount { get; private set; }
        public int HideCallCount { get; private set; }
        public LoadingDisplayType LastDisplayType { get; private set; }
        public bool IsShowing { get; private set; }
        public UniTaskCompletionSource? ShowGate { get; set; }

        public async UniTask Show(LoadingDisplayType displayType, CancellationToken ct)
        {
            ShowCallCount++;
            LastDisplayType = displayType;

            if (ShowGate != null)
            {
                await ShowGate.Task.AttachExternalCancellation(ct);
            }

            ct.ThrowIfCancellationRequested();
            IsShowing = true;
        }

        public UniTask Hide(CancellationToken ct)
        {
            HideCallCount++;
            IsShowing = false;
            return UniTask.CompletedTask;
        }
    }
}
