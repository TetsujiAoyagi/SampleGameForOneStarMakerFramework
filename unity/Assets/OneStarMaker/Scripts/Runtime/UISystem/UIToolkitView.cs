#nullable enable

using System;
using OneStarMaker.Runtime.UISystem.Mvvm;
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

        /// <summary>
        /// CloneTree で生成された UI ルート。
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

            // OnRootCreated 内から Root へ再入アクセスされても安全なよう、
            // _root 代入とフラグ設定を完了させてから通知する。
            _root = _visualTreeAsset.CloneTree();
            _rootInitialized = true;
            OnRootCreated(_root);
        }

        /// <summary>
        /// GameObject 破棄時の追加クリーンアップ。
        /// 基底の破棄処理（Root 除去・ViewModel Dispose）の後に呼ばれる。
        /// 派生クラスは OnDestroy を定義せず、こちらをオーバーライドすること。
        /// </summary>
        protected virtual void OnViewDestroy() { }

        private void OnDestroy()
        {
            if (_root != null)
            {
                _root.RemoveFromHierarchy();
                _root = null;
            }

            _viewModel?.Dispose();
            _viewModel = null;

            OnViewDestroy();
        }
    }
}
