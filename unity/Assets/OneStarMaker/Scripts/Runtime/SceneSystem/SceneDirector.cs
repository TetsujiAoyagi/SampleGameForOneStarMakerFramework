#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.UISystem;
using R3;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// シーンの追加・アンロードを一元管理する。
    /// 親子ツリー構造に基づき、LoadType に応じたロード戦略を実行する。
    /// UICommon との仲介者として UIView のライフサイクルも管理する。
    /// ISceneQuery を実装し、SceneBase に読み取り専用アクセスを提供する。
    ///
    /// partial 構成:
    ///   SceneDirector.cs             … フィールド, ctor, Dispose, ISceneQuery, テストアクセサ, ヘルパー
    ///   SceneDirector.Loading.cs     … AddScene, LoadSceneBase, LoadUnityScene, PerformUnitySceneLoad
    ///   SceneDirector.Unloading.cs   … UnloadScene, RemoveScene, 3-Phase, CleanupCanceledScene, PerformUnitySceneUnload
    ///   SceneDirector.Transitions.cs … SwitchScene, GoBack, ClearHistory, ExecuteTransitionPlan
    /// </summary>
    public partial class SceneDirector : IDisposable, ISceneQuery, ISceneController
    {
        /// <summary>シーンペア（SceneBase + Addressables ロード状態 + ロード用 CTS）。</summary>
        private class ScenePair
        {
            public SceneBase SceneBase { get; }

            /// <summary>
            /// PerformUnitySceneLoad が Addressables 経由でロードした場合 true。
            /// Phase 2/3 で AssetManagement 経由の Unload/Release に使用する。
            /// Editor 既存シーン等で SceneManager から取得した場合は false。
            /// </summary>
            public bool AddressablesSceneLoaded { get; set; }

            /// <summary>
            /// AddScene のキャンセル窓内でのみ有効な CTS。
            /// UnloadScene がロード中シーンをキャンセルするために使う。
            /// ポイント・オブ・ノーリターン通過後は null にクリアされる。
            /// </summary>
            public System.Threading.CancellationTokenSource? LoadCts { get; set; }

            public ScenePair(SceneBase sceneBase)
            {
                SceneBase = sceneBase;
            }
        }

        /// <summary>シーン遷移履歴のエントリ。</summary>
        private readonly struct SceneHistoryEntry
        {
            public readonly string FromSceneId;
            public readonly string ToSceneId;

            public SceneHistoryEntry(string fromSceneId, string toSceneId)
            {
                FromSceneId = fromSceneId;
                ToSceneId = toSceneId;
            }
        }

        private readonly Dictionary<string, ScenePair> _currentScenes = new();
        private readonly HashSet<string> _pendingUnloads = new();
        private readonly Dictionary<string, UniTaskCompletionSource> _inFlightAddScenes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UniTaskCompletionSource> _inFlightSceneBaseLoads = new(StringComparer.Ordinal);
        private readonly Dictionary<string, UniTaskCompletionSource> _inFlightUnitySceneLoads = new(StringComparer.Ordinal);
        private readonly ISceneFactory _sceneFactory;
        private readonly UICommon _uiCommon;
        private readonly SceneResourceMap _sceneResourceMap;
        private readonly ILoadingDisplay _loadingDisplay;

        /// <summary>
        /// Addressables Load / Release 管理。PerformUnitySceneLoad/Unload および Phase 3 Release に使用。
        /// AbstractApplicationInitializer が生成したインスタンスを共有する。
        /// </summary>
        private readonly IAssetManagement _assetManagement;
        private readonly Subject<SceneEvent> _sceneEventSubject = new();
        private readonly Stack<SceneHistoryEntry> _sceneHistory = new();

        /// <summary>シーン遷移履歴の最大保持数。</summary>
        private const int MaxSceneHistoryCount = 8;

        /// <summary>
        /// シーンライフサイクルイベントの Observable。
        /// デバッグ UI、Analytics、ログ等の観測に使用する。
        /// Dispose 時に自動的に OnCompleted が発行される。
        /// </summary>
        public Observable<SceneEvent> OnSceneEvent => _sceneEventSubject;

        /// <summary>
        /// シーン履歴が存在し、<see cref="GoBack"/> が呼び出し可能かを返す。
        /// </summary>
        public bool CanGoBack => _sceneHistory.Count > 0;

        /// <summary>
        /// SceneDirector を生成する。
        /// </summary>
        /// <param name="sceneFactory">SceneBase ファクトリ。</param>
        /// <param name="uiCommon">共通 UI 管理。</param>
        /// <param name="sceneResourceMap">シーンリソースマップ。</param>
        /// <param name="loadingDisplay">ローディング表示の実装。</param>
        /// <param name="assetManagement">
        /// Addressables Load / Release 管理。
        /// シーンロード・アンロードの 3-Phase と PreLoad アセットの Release に使用する。
        /// </param>
        public SceneDirector(
            ISceneFactory sceneFactory,
            UICommon uiCommon,
            SceneResourceMap sceneResourceMap,
            ILoadingDisplay loadingDisplay,
            IAssetManagement assetManagement)
        {
            _sceneFactory = sceneFactory ?? throw new ArgumentNullException(nameof(sceneFactory));
            _uiCommon = uiCommon ?? throw new ArgumentNullException(nameof(uiCommon));
            _sceneResourceMap = sceneResourceMap ?? throw new ArgumentNullException(nameof(sceneResourceMap));
            _loadingDisplay = loadingDisplay ?? throw new ArgumentNullException(nameof(loadingDisplay));
            _assetManagement = assetManagement ?? throw new ArgumentNullException(nameof(assetManagement));
        }

        // ─── IDisposable ───

        public void Dispose()
        {
            Release();
        }

        // ─── ISceneQuery 実装 ───

        /// <inheritdoc/>
        public SceneBase? GetLoadedScene(string identity)
        {
            if (_currentScenes.TryGetValue(identity, out var pair)
                && !pair.SceneBase.Lifecycle.IsUnloadStarted
                && !pair.SceneBase.Lifecycle.IsLoadCanceled)
            {
                return pair.SceneBase;
            }
            return null;
        }

        /// <inheritdoc/>
        public bool IsSceneLoaded(string identity)
        {
            return _currentScenes.TryGetValue(identity, out var pair)
                   && !pair.SceneBase.Lifecycle.IsUnloadStarted
                   && !pair.SceneBase.Lifecycle.IsLoadCanceled;
        }

        /// <inheritdoc/>
        public bool IsSceneStable(string identity)
        {
            return _currentScenes.TryGetValue(identity, out var pair)
                   && pair.SceneBase.Lifecycle.State == SceneState.Stable
                   && !pair.SceneBase.Lifecycle.IsUnloadStarted;
        }

        // ─── Internal: テストアクセサ ───

        /// <summary>テスト用: 指定シーンが管理下にあるか。</summary>
        internal bool ContainsScene(string sceneIdentify) => _currentScenes.ContainsKey(sceneIdentify);

        /// <summary>テスト用: 指定シーンの状態を取得する。</summary>
        internal SceneState GetSceneState(string sceneIdentify)
            => _currentScenes.TryGetValue(sceneIdentify, out var p)
                ? p.SceneBase.Lifecycle.State
                : throw new KeyNotFoundException($"Scene not found: {sceneIdentify}");

        /// <summary>テスト用: ペンディングアンロードが登録されているか。</summary>
        internal bool HasPendingUnload(string sceneIdentify) => _pendingUnloads.Contains(sceneIdentify);

        /// <summary>テスト用: シーン遷移履歴のエントリ数。</summary>
        internal int SceneHistoryCount => _sceneHistory.Count;

        // ─── Private: Helpers ───

        /// <summary>
        /// 親シーンを再帰的に収集する（ルート方向から順に並ぶ）。
        /// AddSceneCore では収集した各祖先を isLoadChildren / isLoadChildScene: true でロードし、
        /// NecessaryAlways / IncrementalAlways / OnDemand の再帰規則をターゲットと同一に適用する。
        /// </summary>
        private static void CollectNecessaryScenes(SceneResource sceneResource, List<SceneResource> list)
        {
            if (sceneResource.Parent != null)
            {
                CollectNecessaryScenes(sceneResource.Parent, list);
            }
            list.Add(sceneResource);
        }

        /// <summary>
        /// SceneDirector 自身の台帳と SceneBase だけを破棄する（teardown / Dispose 専用）。
        /// AssetManagement の Scene Unload や所有アセット解放はここでは行わない。
        /// Play Mode 終了時に Addressables.UnloadSceneAsync を誘発すると
        /// Unity 解体済み Scene に対する「Cannot find handle」になるため、
        /// 実リソース解放は直後の <c>AssetManagement.ReleaseAll()</c>（Shutdown 契約）に一元化する。
        /// </summary>
        private void Release()
        {
            foreach (var kvp in _currentScenes)
            {
                kvp.Value.SceneBase.Dispose();
            }

            _currentScenes.Clear();
            _sceneHistory.Clear();
            _pendingUnloads.Clear();
            _sceneEventSubject.Dispose();
        }

        /// <summary>
        /// SceneState 遷移と StateChanged 通知を一体化する。
        /// 外部購読者はこのイベントを見て activation 解禁可否を判定するため、
        /// SceneDirector 側で遷移通知漏れを作らないことが重要。
        /// </summary>
        private void TransitionSceneState(string sceneIdentify, SceneBase sceneBase, SceneState newState)
        {
            sceneBase.Lifecycle.TransitionTo(newState);
            _sceneEventSubject.OnNext(new SceneEvent(
                SceneEventType.StateChanged,
                sceneIdentify,
                newState,
                sceneBase.Lifecycle.LastPhaseElapsedMs));
        }
    }
}
