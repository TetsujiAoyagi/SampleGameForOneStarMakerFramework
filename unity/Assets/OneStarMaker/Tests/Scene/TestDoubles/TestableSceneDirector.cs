#nullable enable

using System;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace OneStarMaker.Tests.SceneSystem.TestDoubles
{
    /// <summary>
    /// SceneDirector のテスト用サブクラス。
    /// Unity Scene I/O (Addressables / SceneManager) を排除する。
    /// </summary>
    public class TestableSceneDirector : SceneDirector
    {
        /// <summary>
        /// null でなければ PerformUnitySceneLoad 内でこの TCS の完了を待つ。
        /// テストで PNR 通過後～Stable 前の状態を検証するためのゲート。
        /// </summary>
        public UniTaskCompletionSource? UnitySceneLoadGate { get; set; }

        /// <summary>PerformUnitySceneUnload が呼ばれた回数。</summary>
        public int UnloadCallCount { get; private set; }

        public TestableSceneDirector(
            ISceneFactory sceneFactory,
            UICommon uiCommon,
            SceneResourceMap sceneResourceMap,
            ILoadingDisplay? loadingDisplay = null)
            : base(sceneFactory, uiCommon, sceneResourceMap, loadingDisplay ?? new NullLoadingDisplay())
        {
        }

        /// <summary>テスト用: 何もしない ILoadingDisplay。</summary>
        private class NullLoadingDisplay : ILoadingDisplay
        {
            public UniTask Show(LoadingDisplayType displayType, System.Threading.CancellationToken ct)
                => UniTask.CompletedTask;
            public UniTask Hide(System.Threading.CancellationToken ct)
                => UniTask.CompletedTask;
        }

        protected override async UniTask<(AsyncOperationHandle<SceneInstance>? Handle, GameObject[] RootObjects)>
            PerformUnitySceneLoad(string sceneIdentify, SceneResource sceneResource)
        {
            if (UnitySceneLoadGate != null)
            {
                await UnitySceneLoadGate.Task;
            }

            return (null, Array.Empty<GameObject>());
        }

        protected override UniTask PerformUnitySceneUnload(
            string sceneIdentify, AsyncOperationHandle<SceneInstance>? handle)
        {
            UnloadCallCount++;
            return UniTask.CompletedTask;
        }
    }
}
