#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// シーンの基底クラス。
    /// 1シーン = 1 SceneBase サブクラス。
    /// 1シーン = 0 or 1 UIView（複数 UI が必要なら子シーンに分ける）。
    ///
    /// ライフサイクルフックは protected virtual メソッドとしてサブクラスに公開する。
    /// 状態管理は SceneLifecycleManager に委譲し、外部からの直接変更を禁止する。
    /// </summary>
    public class SceneBase : IDisposable
    {
        private readonly SceneResource _sceneResource;
        private readonly ISceneQuery _sceneQuery;
        private readonly ISceneController _sceneController;
        private readonly SceneLifecycleManager _lifecycle = new();

        /// <summary>
        /// SceneDirector から BindAssets で注入される Addressables 管理。
        /// OnPreLoadedImpl 以降で Assets プロパティ経由のアクセスが可能。
        /// </summary>
        private IAssetManagement _assets = null!;
        private readonly List<GameObject> _rootObjects = new();

        private UIView? _uiView;
        private SceneContext? _context;
        private bool _disposed;

        /// <summary>SceneBase を生成する。</summary>
        /// <param name="sceneResource">このシーンの定義情報。</param>
        /// <param name="sceneQuery">ロード済みシーンへの読み取り専用アクセス。</param>
        /// <param name="sceneController">シーン読み込みコントロール専用アクセス。</param>
        public SceneBase(SceneResource sceneResource, ISceneQuery sceneQuery, ISceneController sceneController)
        {
            _sceneResource = sceneResource ?? throw new ArgumentNullException(nameof(sceneResource));
            _sceneQuery = sceneQuery ?? throw new ArgumentNullException(nameof(sceneQuery));
            _sceneController = sceneController;
        }

        // ─── Public API ───

        /// <summary>シーン定義情報。</summary>
        public SceneResource SceneResource => _sceneResource;

        /// <summary>
        /// ロード済みシーンへの読み取り専用アクセス。
        /// 親シーンや兄弟シーンのサービスを取得するために使用する。
        /// </summary>
        protected ISceneQuery SceneQuery => _sceneQuery;

        /// <summary>
        /// シーン読み込みコントロール
        /// 他のシーンへの遷移や追加、Unloadなど行う
        /// </summary>
        protected ISceneController SceneController => _sceneController;

        /// <summary>
        /// Addressables Load / Release API。
        /// SceneBase.OnPreLoadedImpl 等から PreLoad アセットのロードに使用する。
        /// </summary>
        protected IAssetManagement Assets => _assets;

        /// <summary>
        /// このシーンを寿命とする AssetOwner。シーンアンロード時に紐付けたアセットが解放される。
        /// </summary>
        protected AssetOwner SceneAssetOwner => AssetOwner.Scene(SceneResource.Identity);

        /// <summary>
        /// このシーンスコープでアセットをロードする。手動で AssetOwner.Scene を渡す必要をなくしリーク誤りを防ぐ。
        /// </summary>
        protected UniTask<IAssetHandle<T>> LoadSceneScopedAssetAsync<T>(AssetKey key, CancellationToken ct = default)
            where T : UnityEngine.Object
            => Assets.LoadAssetAsync<T>(key, SceneAssetOwner, ct);

        /// <summary>
        /// SceneDirector から AssetManagement を注入する。
        /// LoadSceneBase 内で SceneBase 生成直後に 1 回だけ呼ばれる。
        /// 注入前に Assets へアクセスすると NullReferenceException になる。
        /// </summary>
        /// <param name="assets">アプリ全体で共有する AssetManagement インスタンス。</param>
        internal void BindAssets(IAssetManagement assets)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }

        /// <summary>ライフサイクルマネージャ（状態の読み取り用）。</summary>
        internal SceneLifecycleManager Lifecycle => _lifecycle;

        /// <summary>このシーンの UIView。なければ null。</summary>
        public UIView? UIView => _uiView;

        /// <summary>
        /// 遷移時に渡されたコンテキスト。AddScene の context 引数がそのまま入る。
        /// OnPreLoadedImpl 以降で参照可能。未指定の場合は null。
        /// </summary>
        protected SceneContext? Context => _context;

        /// <summary>SceneDirector からコンテキストをセットする。</summary>
        internal void SetContext(SceneContext? context) => _context = context;

        /// <summary>
        /// 宣言的な遷移プランを返す。
        /// ライフサイクルフック内から SceneDirector を直接呼ぶ代わりに、このメソッドで遷移を宣言する。
        /// </summary>
        public virtual SceneTransitionPlan? CreateTransitionPlan() => null;

        // ─── Internal lifecycle（SceneDirector から呼ばれる） ───

        /// <summary>
        /// Unity Scene ロード後に RootGameObjects を受け取り初期化する。
        /// </summary>
        internal void Initialize(GameObject[] roots)
        {
            _rootObjects.AddRange(roots);

            _uiView = SearchUIView();

            OnInitialize();
        }

        /// <summary>
        /// Initialize 時に RootObjects から UIView を自動検索する（1シーン = 0 or 1 UIView）。
        /// UI を構造的に持たないシーン（CellScene 等、R-2）は null 固定にオーバーライドする。
        /// </summary>
        protected virtual UIView? SearchUIView()
        {
            foreach (var rootObject in _rootObjects)
            {
                var uiView = rootObject.GetComponentInChildren<UIView>();
                if (uiView != null)
                {
                    return uiView;
                }
            }

            return null;
        }

        /// <summary>PreLoading → PreLoaded。Unity Scene ロード前の事前準備。</summary>
        internal async UniTask ExecutePreLoad(CancellationToken ct)
        {
            _lifecycle.TransitionTo(SceneState.PreLoading);
            await OnPreLoadedImpl(ct);
            _lifecycle.TransitionTo(SceneState.PreLoaded);
        }

        /// <summary>Unity Scene ロード完了後のコールバック。状態遷移は SceneDirector が管理する。</summary>
        internal async UniTask ExecuteLoaded(CancellationToken ct)
        {
            await OnLoadedImpl(ct);
        }
        
        internal async UniTask ExecuteStabled()
        {
            await OnStabledImpl();
        }

        /// <summary>PreUnloading → PreUnloaded。リソース解放の準備。キャンセル不可。</summary>
        internal async UniTask ExecutePreUnLoad()
        {
            _lifecycle.TransitionTo(SceneState.PreUnloading);
            _rootObjects.Clear();
            await OnPreUnLoadedImpl();
            _lifecycle.TransitionTo(SceneState.PreUnloaded);
        }

        /// <summary>Unloaded → AfterUnloading。最終クリーンアップ。キャンセル不可。</summary>
        internal async UniTask ExecuteAfterUnLoad()
        {
            _lifecycle.TransitionTo(SceneState.AfterUnloading);
            await OnAfterUnLoadedImpl();
        }

        // ─── Protected virtual hooks（サブクラスでオーバーライド） ───

        /// <summary>RootGameObjects 取得後の初期化。MonoBehaviour の参照取得等。</summary>
        protected virtual void OnInitialize() { }

        /// <summary>Unity Scene ロード前の事前リソース準備。</summary>
        protected virtual UniTask OnPreLoadedImpl(CancellationToken ct) => UniTask.CompletedTask;

        /// <summary>Unity Scene ロード後の Addressable アセットのロード等。</summary>
        protected virtual UniTask OnLoadedImpl(CancellationToken ct) => UniTask.CompletedTask;

        /// <summary>諸々の処理が終わった状態 </summary>
        protected virtual UniTask OnStabledImpl() => UniTask.CompletedTask;

        /// <summary>Unity Scene アンロード前のリソース解放準備。キャンセル不可。</summary>
        protected virtual UniTask OnPreUnLoadedImpl() => UniTask.CompletedTask;

        /// <summary>Unity Scene アンロード後の最終クリーンアップ。キャンセル不可。</summary>
        protected virtual UniTask OnAfterUnLoadedImpl() => UniTask.CompletedTask;

        // ─── Protected helpers ───

        /// <summary>RootObjects から指定コンポーネントを検索する。</summary>
        protected T? FindRootComponent<T>() where T : Component
        {
            foreach (var root in _rootObjects)
            {
                var component = root.GetComponentInChildren<T>();
                if (component != null)
                {
                    return component;
                }
            }
            return null;
        }

        /// <summary>RootObjects のリスト（読み取り用）。</summary>
        protected IReadOnlyList<GameObject> RootObjects => _rootObjects;

        // ─── IDisposable ───

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _rootObjects.Clear();
                    _uiView = null;
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
