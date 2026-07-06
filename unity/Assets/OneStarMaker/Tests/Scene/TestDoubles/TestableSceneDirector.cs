#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.SceneSystem;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;

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

        /// <summary>
        /// identity 別ゲート。特定シーンの Unity Scene ロードだけを保留する。
        /// 並行 AddScene テストで「親だけロード中」等の状況を作るために使う。
        /// <see cref="UnitySceneLoadGate"/>（全シーン共通）より先に評価される。
        /// </summary>
        public Dictionary<string, UniTaskCompletionSource> SceneLoadGates { get; } = new();

        /// <summary>
        /// PerformUnitySceneLoad の identity 別呼び出し回数。
        /// 並行 AddScene による二重ロード（H-1、21-scene-streaming.md §5）の検出用。
        /// </summary>
        public Dictionary<string, int> UnitySceneLoadCallCounts { get; } = new();

        /// <summary>
        /// 直近の PerformUnitySceneLoad に渡された priority を identity 別に記録する。
        /// T-03 の priority 伝搬検証用。
        /// </summary>
        public Dictionary<string, int> LastLoadPriorities { get; } = new();

        /// <summary>PerformUnitySceneUnload が呼ばれた回数。3-Phase Unload の検証用。</summary>
        public int UnloadCallCount { get; private set; }

        /// <summary>
        /// テスト用 SceneDirector を生成する。
        /// AssetManagement には FakeAssetBackend 入りのインスタンスを渡す想定。
        /// </summary>
        /// <param name="assetManagement">Fake バックエンド入り AssetManagement。</param>
        public TestableSceneDirector(
            ISceneFactory sceneFactory,
            UICommon uiCommon,
            SceneResourceMap sceneResourceMap,
            IAssetManagement assetManagement,
            ILoadingDisplay? loadingDisplay = null)
            : base(sceneFactory, uiCommon, sceneResourceMap, loadingDisplay ?? new NullLoadingDisplay(), assetManagement)
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

        protected override async UniTask<(bool AddressablesLoaded, GameObject[] RootObjects)>
            PerformUnitySceneLoad(string sceneIdentify, SceneResource sceneResource, int priority)
        {
            LastLoadPriorities[sceneIdentify] = priority;

            UnitySceneLoadCallCounts.TryGetValue(sceneIdentify, out var count);
            UnitySceneLoadCallCounts[sceneIdentify] = count + 1;

            // identity 別ゲートを優先し、無ければ全シーン共通ゲートを見る
            if (SceneLoadGates.TryGetValue(sceneIdentify, out var sceneGate))
            {
                await sceneGate.Task;
            }
            else if (UnitySceneLoadGate != null)
            {
                await UnitySceneLoadGate.Task;
            }

            // Addressables ロードをスキップ。RootObjects 空で SceneBase ライフサイクルのみ検証
            return (false, Array.Empty<GameObject>());
        }

        /// <summary>
        /// テスト用: 実際の SceneManager / AssetManagement 呼び出しを行わず、呼び出し回数のみ記録。
        /// </summary>
        protected override UniTask PerformUnitySceneUnload(
            string sceneIdentify, bool addressablesSceneLoaded)
        {
            UnloadCallCount++;
            return UniTask.CompletedTask;
        }
    }
}
