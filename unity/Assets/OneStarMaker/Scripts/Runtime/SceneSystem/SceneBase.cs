#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using OneStarMaker.Runtime.AssetDescriptions;
using OneStarMaker.Runtime.AssetManagement;
using OneStarMaker.Runtime.UISystem;
using UnityEngine;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// SceneDirector が SceneBase の生成直後に注入する、content の読み口の固定値。
    /// 起動時に決まった mode（Content Directory か Addressables か）と Scene payload の Variant、
    /// そして自分と祖先の付随 asset の宣言だけを持つ。SceneResourceMap 全体は渡さない。
    /// 読む Scene が到達するなら祖先も到達するので、祖先の宣言だけを引けるようにすれば
    /// 「必要な付随 asset が build に入っていない」経路を構造で塞げる。
    /// </summary>
    internal readonly struct SceneContentBinding
    {
        private readonly IReadOnlyList<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>>? _lineage;

        public SceneContentBinding(
            bool useContentDirectory,
            string sceneVariant,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>> lineage)
        {
            UseContentDirectory = useContentDirectory;
            SceneVariant = sceneVariant ?? throw new ArgumentNullException(nameof(sceneVariant));
            _lineage = lineage ?? throw new ArgumentNullException(nameof(lineage));
        }

        /// <summary>true なら directory の読み口（logical key）、false なら Addressables の読み口（AssetKey）で読む。</summary>
        public bool UseContentDirectory { get; }

        /// <summary>起動時に固定した Scene payload の Variant。付随 asset も同じ Variant で引く（Full fallback は読み口の側）。</summary>
        public string SceneVariant { get; }

        /// <summary>自分、親、…、root の順の (identity, 付随 asset の宣言)。</summary>
        public IReadOnlyList<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>> Lineage
            => _lineage ?? Array.Empty<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>>();

        /// <summary>
        /// SceneResource の親の鎖から binding を作る。親子の鎖はロード済み graph の検証を通っている前提だが、
        /// 万一の循環で止まらないよう、同じ identity を 2 度は辿らない。
        /// </summary>
        public static SceneContentBinding FromResource(SceneResource resource, bool useContentDirectory, string sceneVariant)
        {
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            var lineage = new List<KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            // SceneResource は UnityEngine.Object。破棄済みを ?. で素通しさせない。
            for (var cursor = resource; cursor != null && visited.Add(cursor.Identity); cursor = cursor.Parent)
            {
                lineage.Add(new KeyValuePair<string, IReadOnlyList<AttachedAssetDescription>>(cursor.Identity, cursor.AttachedAssets));
            }

            return new SceneContentBinding(useContentDirectory, sceneVariant, lineage);
        }

        /// <summary>所有 node（自分か祖先）の identity と名前で宣言を引く。無ければ null。</summary>
        public AttachedAssetDescription? Find(string ownerIdentity, string name)
        {
            foreach (var owner in Lineage)
            {
                if (!string.Equals(owner.Key, ownerIdentity, StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var attached in owner.Value)
                {
                    if (attached != null && string.Equals(attached.Name, name, StringComparison.Ordinal))
                    {
                        return attached;
                    }
                }

                return null;
            }

            return null;
        }
    }

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
        private SceneContentBinding _content;
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
        /// この Scene の SceneResource が宣言した付随 asset を、名前でこの Scene 所有（<c>Scene(自分の identity)</c>）でロードする。
        /// Content Directory の Play では directory の entry から、それ以外では宣言の参照から Addressables で読む。
        /// 呼ぶ側は address・path・mode を知らない。
        /// </summary>
        protected UniTask<IAssetHandle<T>> LoadAttachedAssetAsync<T>(string name, CancellationToken ct = default)
            where T : UnityEngine.Object
            => LoadAttachedAssetAsync<T>(SceneResource.Identity, name, ct);

        /// <summary>
        /// 自分か祖先の node（<paramref name="ownerIdentity"/>）が宣言した付随 asset を、名前でこの Scene 所有でロードする。
        /// 所有者は宣言した node ではなく常に読む Scene なので、共有 asset は AssetManagement の canonical key の共有と
        /// 所有者ごとの参照で従来どおり数えられる（1 つの Scene を unload しても他の Scene の handle は有効）。
        /// 祖先でない node や未宣言の名前は、設定の誤りとして <see cref="ArgumentException"/> にする。
        /// </summary>
        protected UniTask<IAssetHandle<T>> LoadAttachedAssetAsync<T>(string ownerIdentity, string name, CancellationToken ct = default)
            where T : UnityEngine.Object
        {
            if (string.IsNullOrWhiteSpace(ownerIdentity))
            {
                throw new ArgumentException("Owner identity is required.", nameof(ownerIdentity));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Attached asset name is required.", nameof(name));
            }

            var description = _content.Find(ownerIdentity, name)
                ?? throw new ArgumentException(
                    $"Attached asset '{AttachedAssetDescription.LogicalKey(ownerIdentity, name)}' is not declared by scene '{SceneResource.Identity}' or one of its ancestors.",
                    nameof(name));

            // directory では logical key と起動時の Variant で引く（Full fallback は index の側）。
            // Addressables では宣言の参照から AssetKey を作る。どちらも所有者は読む Scene。
            return _content.UseContentDirectory
                ? Assets.LoadContentAssetAsync<T>(AttachedAssetDescription.LogicalKey(ownerIdentity, name), _content.SceneVariant, SceneAssetOwner, ct)
                : Assets.LoadAssetAsync<T>(AssetKey.FromDescription(description, _content.SceneVariant), SceneAssetOwner, ct);
        }

        /// <summary>
        /// SceneDirector から AssetManagement を注入する。
        /// LoadSceneBase 内で SceneBase 生成直後に 1 回だけ呼ばれる。
        /// 注入前に Assets へアクセスすると NullReferenceException になる。
        /// この overload は Addressables の読み口（Variant 無し）として binding を作る。テストの偽 Director が使う。
        /// </summary>
        /// <param name="assets">アプリ全体で共有する AssetManagement インスタンス。</param>
        internal void BindAssets(IAssetManagement assets)
            => BindAssets(assets, SceneContentBinding.FromResource(_sceneResource, false, string.Empty));

        /// <summary>
        /// AssetManagement と content の読み口（mode・Variant・自分と祖先の付随 asset の宣言）を同じ時点で注入する。
        /// </summary>
        internal void BindAssets(IAssetManagement assets, SceneContentBinding content)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _content = content;
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
            await ContinuePreUnLoadFromCurrent();
        }

        /// <summary>
        /// すでに PreUnloading にいる個体の残り（フック → PreUnloaded）。
        /// 途中失敗した RemoveScene を再開するとき、TransitionTo(PreUnloading) を再実行しない。
        /// </summary>
        internal async UniTask ContinuePreUnLoadFromCurrent()
        {
            if (_lifecycle.State != SceneState.PreUnloading)
            {
                throw new InvalidOperationException(
                    $"ContinuePreUnLoadFromCurrent requires PreUnloading, but was {_lifecycle.State}");
            }

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

        /// <summary>
        /// すでに AfterUnloading にいる個体の残りフック。
        /// ExecuteAfterUnLoad は TransitionTo(AfterUnloading) から入るため、再開時は呼べない。
        /// </summary>
        internal async UniTask ContinueAfterUnLoadFromCurrent()
        {
            if (_lifecycle.State != SceneState.AfterUnloading)
            {
                throw new InvalidOperationException(
                    $"ContinueAfterUnLoadFromCurrent requires AfterUnloading, but was {_lifecycle.State}");
            }

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
