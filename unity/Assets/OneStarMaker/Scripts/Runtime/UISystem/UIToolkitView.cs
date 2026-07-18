#nullable enable

using System;
using OneStarMaker.Runtime.UISystem.Mvvm;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Runtime.UISystem
{
    /// <summary>
    /// UI Toolkit ベースの UIView 基底クラス。
    /// VisualTreeAsset から Root を生成し、ViewModel の寿命を管理する。
    /// 派生クラスは OnDestroy を定義せず、<see cref="OnViewDestroy"/> をオーバーライドすること
    /// （Unity のマジックメソッドは最派生のみが呼ばれ、基底の破棄処理が失われるため）。
    /// </summary>
    public abstract class UIToolkitView : UIView
    {
        [SerializeField]
        private VisualTreeAsset? _visualTreeAsset;

        private VisualElement? _root;
        private ViewModelBase? _viewModel;
        private bool _rootInitialized;
        private readonly CompositeDisposable _viewBindings = new();

        /// <summary>
        /// レイヤー全体を占める Framework 所有の UXML Host。
        /// UXML の内容はこの直下へ展開され、画面固有のレイアウトは UXML 側が担う。
        /// 初回アクセス時に生成される。
        /// </summary>
        public VisualElement Root
        {
            get
            {
                EnsureRootCreated();
                return _root!;
            }
        }

        /// <summary>
        /// Root を明示的に生成する。複数回呼んでも安全。
        /// </summary>
        public void Initialize()
        {
            EnsureRootCreated();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor のシーン生成ツールから UXML を割り当てる。
        /// Root 生成後の差し替えはライフサイクル不整合を起こすため拒否する。
        /// </summary>
        /// <param name="visualTreeAsset">割り当てる UXML アセット。</param>
        public void AssignVisualTreeAssetForEditor(VisualTreeAsset visualTreeAsset)
        {
            if (visualTreeAsset == null)
            {
                throw new ArgumentNullException(nameof(visualTreeAsset));
            }

            if (_rootInitialized)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} の Root 生成後に VisualTreeAsset は差し替えられません。");
            }

            _visualTreeAsset = visualTreeAsset;
        }
#endif

        /// <summary>
        /// Root 生成直後に呼ばれる。派生クラスで UXML クエリとバインドを行う。
        /// </summary>
        /// <param name="root">生成済み Root。</param>
        protected virtual void OnRootCreated(VisualElement root) { }

        /// <summary>
        /// ViewModel を紐付ける。GameObject 破棄時に自動 Dispose される。
        /// </summary>
        /// <param name="viewModel">紐付ける ViewModel。</param>
        protected void SetViewModel(ViewModelBase viewModel)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            if (ReferenceEquals(_viewModel, viewModel))
            {
                return;
            }

            _viewModel?.Dispose();
            _viewModel = viewModel;
        }

        /// <summary>
        /// View 寿命の購読・Runner を登録する。
        /// <see cref="OnViewDestroy"/> は追加クリーンアップ専用とし、
        /// VisualElement または BehaviorRunner に触れる IDisposable は必ずここへ集約する。
        /// </summary>
        /// <typeparam name="T">登録する Disposable の型。</typeparam>
        /// <param name="disposable">View の破棄時に解除する Disposable。</param>
        /// <returns>渡された <paramref name="disposable"/>。</returns>
        protected T Track<T>(T disposable)
            where T : IDisposable
        {
            if (disposable == null)
            {
                throw new ArgumentNullException(nameof(disposable));
            }

            _viewBindings.Add(disposable);
            return disposable;
        }

        /// <summary>
        /// VisualTreeAsset から Root を生成し、<see cref="OnRootCreated"/> を呼ぶ。
        /// VisualTreeAsset 未割り当ての場合は状態を変更せずに例外を投げる（再呼び出しで同じ例外が再現する）。
        /// </summary>
        protected void EnsureRootCreated()
        {
            if (_rootInitialized)
            {
                return;
            }

            if (_visualTreeAsset == null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name} に VisualTreeAsset が割り当てられていません。");
            }

            // CloneTree() の戻り値（TemplateContainer）を直接レイヤーへ追加すると、
            // UXML の先頭要素の外側にサイズ未決定の親が一枚増える。その結果、
            // UXML 側で height: 100% / flex-grow を指定しても親の高さ 0 に潰れ得る。
            //
            // Framework は Panel のレイヤー全体を占める Host だけを所有する。
            // UXML は Host の直下に展開し、Stretch / Content / HUD 等の画面固有レイアウトを
            // 最上位 UXML 要素で定義する。
            _root = CreateRootHost();
            _visualTreeAsset.CloneTree(_root);

            // OnRootCreated 内から Root へ再入アクセスされても安全なよう、
            // _root 代入とフラグ設定を完了させてから通知する。
            _rootInitialized = true;
            OnRootCreated(_root);
        }

        private static VisualElement CreateRootHost()
        {
            var host = new VisualElement
            {
                name = "UIToolkitViewHost",
                // 空の Host が背面 UI の入力を奪わず、UXML 内の子要素は通常どおり
                // picking 対象になるようにする。
                pickingMode = PickingMode.Ignore,
            };

            host.style.position = Position.Absolute;
            host.style.top = 0;
            host.style.right = 0;
            host.style.bottom = 0;
            host.style.left = 0;
            return host;
        }

        /// <summary>
        /// GameObject 破棄時の追加クリーンアップ。
        /// 基底の破棄処理（Root 除去・ViewModel Dispose）の後に呼ばれる。
        /// 派生クラスは OnDestroy を定義せず、こちらをオーバーライドすること。
        /// </summary>
        protected virtual void OnViewDestroy() { }

        private void OnDestroy()
        {
            // View が持つ購読・Runner を先に止める。これにより ViewModel の
            // ReactiveProperty を Dispose した後に、View 側コールバックが走らない。
            _viewBindings.Dispose();

            _viewModel?.Dispose();
            _viewModel = null;

            if (_root != null)
            {
                _root.RemoveFromHierarchy();
                _root = null;
            }

            OnViewDestroy();
        }
    }
}
