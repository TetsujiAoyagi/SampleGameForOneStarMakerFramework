#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Runtime.UISystem.Mvvm;
using OneStarMaker.Tests.UISystem.TestDoubles;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.UISystem
{
    /// <summary>
    /// View 破棄時の解体順序契約を検証する。
    ///
    /// <para>
    /// binding → ViewModel → root 除去後の hook、の順を配列比較で固定している。
    /// ViewModel を先に捨てると binding が破棄済み参照を触るため、順序自体が契約。
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class UIToolkitViewLifecycleTests
    {
        [Test]
        public void OnDestroy_DisposesViewBindingsBeforeViewModelAndRemovesRootBeforeHook()
        {
            var order = new List<string>();
            var gameObject = new GameObject("LifecycleView");
            var view = gameObject.AddComponent<TestToolkitView>();
            var host = new VisualElement();
            var root = new VisualElement();
            host.Add(root);
            view.SetTestRoot(root);
            view.TrackForTest(Disposable.Create(() => order.Add("binding")));
            view.SetViewModelForTest(new RecordingViewModel(order));
            view.ViewDestroyed = () =>
            {
                order.Add(root.parent == null ? "hook-after-root-removal" : "hook-before-root-removal");
            };

            typeof(UIToolkitView)
                .GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(view, null);

            Assert.That(
                order,
                Is.EqualTo(new[] { "binding", "view-model", "hook-after-root-removal" }));

            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        private sealed class RecordingViewModel : ViewModelBase
        {
            private readonly List<string> _order;

            public RecordingViewModel(List<string> order)
            {
                _order = order;
            }

            protected override void DisposeCore()
            {
                _order.Add("view-model");
            }
        }
    }
}
