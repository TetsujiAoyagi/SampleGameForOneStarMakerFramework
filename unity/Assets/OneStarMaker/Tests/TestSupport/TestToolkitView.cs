#nullable enable

using System;
using System.Reflection;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Runtime.UISystem.Mvvm;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.UISystem.TestDoubles
{
    /// <summary>
    /// VisualTreeAsset なしで Root を差し替え可能な UIToolkitView テスト用派生。
    /// エディタ専用アセンブリの MonoBehaviour は AddComponent できないため、
    /// UNITY_INCLUDE_TESTS 制約付きの非エディタ専用アセンブリ（TestSupport）に置く。
    /// </summary>
    public sealed class TestToolkitView : UIToolkitView
    {
        private static readonly FieldInfo RootField =
            typeof(UIToolkitView).GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly FieldInfo RootInitializedField =
            typeof(UIToolkitView).GetField("_rootInitialized", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private UIView.UILayer _layer = UIView.UILayer.Normal;

        public Action? ViewDestroyed { get; set; }

        /// <summary>テスト用 Root を注入する。</summary>
        /// <param name="root">差し替える VisualElement。</param>
        public void SetTestRoot(VisualElement root)
        {
            RootField.SetValue(this, root);
            RootInitializedField.SetValue(this, true);
        }

        /// <summary>GetUILayer の返却値を設定する。</summary>
        /// <param name="layer">レイヤー。</param>
        public void SetLayer(UIView.UILayer layer)
        {
            _layer = layer;
        }

        public T TrackForTest<T>(T disposable)
            where T : IDisposable
        {
            return Track(disposable);
        }

        public void SetViewModelForTest(ViewModelBase viewModel)
        {
            SetViewModel(viewModel);
        }

        /// <inheritdoc/>
        public override UIView.UILayer GetUILayer()
        {
            return _layer;
        }

        protected override void OnViewDestroy()
        {
            ViewDestroyed?.Invoke();
        }
    }
}
