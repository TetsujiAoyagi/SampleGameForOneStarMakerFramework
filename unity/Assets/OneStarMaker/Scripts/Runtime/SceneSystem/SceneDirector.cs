#nullable enable

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.ContentResolution;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;
using R3;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// シーンの追加・アンロードを一元管理する。
    /// 親子ツリー構造に基づき、LoadType に応じたロード戦略を実行する。
    /// UICommon との仲介者として UIView のライフサイクルも管理する。
    /// ISceneQuery を実装し、SceneBase に読み取り専用アクセスを提供する。
    /// ISceneVolumeQuery を実装し、空間政策へ未ロード候補の体積を提供する（§34 §5）。
    ///
    /// partial 構成:
    ///   SceneDirector.cs             … フィールド, ctor, Dispose, ISceneQuery, ISceneVolumeQuery, テストアクセサ, ヘルパー
    ///   SceneDirector.Loading.cs     … AddScene, LoadSceneBase, LoadUnityScene, PerformUnitySceneLoad
    ///   SceneDirector.FailedLoadCleanup.cs … 通常例外で失敗した Add の newlyCreated 回収
    ///   SceneDirector.Unloading.cs   … UnloadScene, RemoveScene, 3-Phase, CleanupCanceledScene, PerformUnitySceneUnload
    ///   SceneDirector.Transitions.cs … SwitchScene, GoBack, ClearHistory, ExecuteTransitionPlan
    /// </summary>
    public partial class SceneDirector : IDisposable, ISceneQuery, ISceneVolumeQuery, ISceneController
    {
        /// <summary>シーンペア（SceneBase + Addressables ロード状態 + ロード用 CTS）。</summary>
        private class ScenePair
        {
            public SceneBase SceneBase { get; }

            /// <summary>
            /// Scene を AssetManagement の Scene の台帳経由で unload する組なら true（Addressables・Content Directory・Editor の Play の複製から読んだ Scene）。
            /// Phase 2/3 で AssetManagement 経由の Unload/Release に使用する。複製が宣言する identity は組を作る時点で true にする。
            /// Editor 既存シーン等で SceneManager から取得した場合は false。
            /// </summary>
            public bool BackendSceneLoaded { get; set; }

            /// <summary>
            /// AddScene のキャンセル窓内でのみ有効な CTS。
            /// UnloadScene がロード中シーンをキャンセルするために使う。
            /// ポイント・オブ・ノーリターン通過後は null にクリアされる。
            /// </summary>
            public System.Threading.CancellationTokenSource? LoadCts { get; set; }

            /// <summary>
            /// この組の Scene を要求時解決の loader で読んだ時の記録。loader で読んだ分岐でだけ入れ、それ以外（Addressables、
            /// SceneManager にあった Scene の再利用、payload の無い論理 node）では null のまま。
            /// 組と同じ寿命にすることで、失敗・取消・unload の後始末が組を外せば記録も消え、次に同じ名前で再利用された
            /// Scene に古い記録が付かない。消す code を別に持たない。
            /// </summary>
            public ContentLoadRecord? ContentRecord { get; set; }

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
        private readonly Dictionary<string, UniTaskCompletionSource> _inFlightUnloads = new(StringComparer.Ordinal);
        private readonly ISceneFactory _sceneFactory;
        private readonly UICommon _uiCommon;
        private readonly SceneResourceMap _sceneResourceMap;
        private readonly ILoadingDisplay _loadingDisplay;
        private readonly string _sceneVariant;
        private readonly bool _useContentDirectory;

        /// <summary>
        /// 要求時解決の loader。起動の配線が検証済みの directory の Play でだけ渡す。null なら content 入口を直接呼ぶ（今までの経路）。
        /// </summary>
        private readonly ContentRequestLoader? _contentLoader;

        /// <summary>loader の要求に明示する fallback の表現。起動時の表現が空でもこの値と同じでもない時だけ要求に付ける。</summary>
        private readonly string _contentFallbackRepresentation;

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
        /// <param name="sceneVariant">起動時に固定された Scene payload Variant。空文字は default。</param>
        public SceneDirector(
            ISceneFactory sceneFactory,
            UICommon uiCommon,
            SceneResourceMap sceneResourceMap,
            ILoadingDisplay loadingDisplay,
            IAssetManagement assetManagement,
            string sceneVariant = "")
            : this(sceneFactory, uiCommon, sceneResourceMap, loadingDisplay, assetManagement, sceneVariant, false)
        {
        }

        // 公開 constructor の署名を維持し、起動配線だけが mode を固定する。
        // この 7 引数の署名は変えない: 既存の test（VFX の Play 系）が引数の数で reflection してこの constructor を探し、
        // アプリの SceneDirector の欄を写した複製を作る。loader は下の constructor で受ける。
        internal SceneDirector(
            ISceneFactory sceneFactory,
            UICommon uiCommon,
            SceneResourceMap sceneResourceMap,
            ILoadingDisplay loadingDisplay,
            IAssetManagement assetManagement,
            string sceneVariant,
            bool useContentDirectory)
            : this(sceneFactory, uiCommon, sceneResourceMap, loadingDisplay, assetManagement, sceneVariant, useContentDirectory, null, "")
        {
        }

        // 起動配線が要求時解決の loader を固定する。
        // loader は content directory から読む時だけ意味を持つので、loader と useContentDirectory: false の組は拒む
        // （黙って Addressables の経路で読み、記録の無い Scene を作らないため）。
        internal SceneDirector(
            ISceneFactory sceneFactory,
            UICommon uiCommon,
            SceneResourceMap sceneResourceMap,
            ILoadingDisplay loadingDisplay,
            IAssetManagement assetManagement,
            string sceneVariant,
            bool useContentDirectory,
            ContentRequestLoader? contentLoader,
            string contentFallbackRepresentation)
        {
            if (contentLoader != null && !useContentDirectory)
                throw new ArgumentException("A content request loader needs the content directory mode.", nameof(contentLoader));

            _sceneFactory = sceneFactory ?? throw new ArgumentNullException(nameof(sceneFactory));
            _uiCommon = uiCommon ?? throw new ArgumentNullException(nameof(uiCommon));
            _sceneResourceMap = sceneResourceMap ?? throw new ArgumentNullException(nameof(sceneResourceMap));
            _loadingDisplay = loadingDisplay ?? throw new ArgumentNullException(nameof(loadingDisplay));
            _assetManagement = assetManagement ?? throw new ArgumentNullException(nameof(assetManagement));
            _sceneVariant = sceneVariant ?? throw new ArgumentNullException(nameof(sceneVariant));
            _useContentDirectory = useContentDirectory;
            _contentLoader = contentLoader;
            _contentFallbackRepresentation = contentFallbackRepresentation ?? throw new ArgumentNullException(nameof(contentFallbackRepresentation));
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

        /// <summary>
        /// 今 SceneDirector が持つその Scene を、要求時解決の loader で読んだ時の記録（どの directory のどの revision・表現から
        /// 読んだか）。loader を通らずに用意した Scene（Addressables、既に SceneManager にあった Scene、payload の無い論理 node）、
        /// 持っていない id、null、unload の後は null を返し、例外にしない。
        /// </summary>
        public ContentLoadRecord? GetContentLoadRecord(string sceneIdentity)
        {
            if (sceneIdentity == null)
            {
                return null;
            }

            return _currentScenes.TryGetValue(sceneIdentity, out var pair) ? pair.ContentRecord : null;
        }

        // ─── ISceneVolumeQuery 実装 ───

        /// <inheritdoc/>
        public bool TryGetSceneVolume(string identity, out Bounds volume)
        {
            volume = default;

            var resource = _sceneResourceMap.GetSceneResource(identity);
            // SceneResource は UnityEngine.Object。破棄済みを ?. / ?? で素通しさせない。
            if (resource == null)
            {
                return false;
            }

            if (!resource.StreamByDistance)
            {
                return false;
            }

            var candidate = resource.Volume;
            if (candidate.size == Vector3.zero)
            {
                return false;
            }

            volume = candidate;
            return true;
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
