#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Tests.UISystem.TestDoubles;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.UISystem
{
    /// <summary>
    /// UICommon の UI Toolkit 経路（レイヤーコンテナ / Blocker / Insert）を検証する。
    /// </summary>
    [TestFixture]
    public class UICommonUIToolkitTests
    {
        private GameObject _uiCommonGo = null!;
        private GameObject? _documentGo;
        private GameObject? _viewGo;
        private PanelSettings? _panelSettings;

        [SetUp]
        public void SetUp()
        {
            _uiCommonGo = new GameObject("UICommon_Test");
            _uiCommonGo.AddComponent<UICommon>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_viewGo != null)
            {
                Object.DestroyImmediate(_viewGo);
            }
            if (_documentGo != null)
            {
                Object.DestroyImmediate(_documentGo);
            }
            if (_panelSettings != null)
            {
                Object.DestroyImmediate(_panelSettings);
            }
            if (_uiCommonGo != null)
            {
                Object.DestroyImmediate(_uiCommonGo);
            }
        }

        [Test]
        public void BuildLayerContainers_CreatesSixLayersInOrderWithIgnorePicking()
        {
            var uiCommon = _uiCommonGo.GetComponent<UICommon>();
            var root = new VisualElement();

            uiCommon.BuildLayerContainers(root);

            Assert.That(root.childCount, Is.EqualTo(6));
            Assert.That(root[0].name, Is.EqualTo("Layer-Background"));
            Assert.That(root[1].name, Is.EqualTo("Layer-Normal"));
            Assert.That(root[2].name, Is.EqualTo("Layer-Modal"));
            Assert.That(root[3].name, Is.EqualTo("Layer-Dialog"));
            Assert.That(root[4].name, Is.EqualTo("Layer-Loading"));
            Assert.That(root[5].name, Is.EqualTo("Layer-Debug"));

            for (var i = 0; i < root.childCount; i++)
            {
                var container = root[i];
                Assert.That(container.pickingMode, Is.EqualTo(PickingMode.Ignore));
                Assert.That(container.style.position.value, Is.EqualTo(Position.Absolute));
            }
        }

        [Test]
        public void CreateToolkitBlocker_SetsNamePickingModeAndFullscreenStyle()
        {
            var uiCommon = _uiCommonGo.GetComponent<UICommon>();

            var blocker = uiCommon.CreateToolkitBlocker("owner-1");

            Assert.That(blocker.name, Is.EqualTo("Blocker_owner-1"));
            Assert.That(blocker.pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(blocker.focusable, Is.False);
            Assert.That(blocker.tabIndex, Is.EqualTo(-1));
            Assert.That(blocker.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(blocker.style.backgroundColor.value, Is.EqualTo(Color.clear));

            var parent = new VisualElement();
            var button = new Button { name = "ok", focusable = true, tabIndex = 1 };
            parent.Add(blocker);
            parent.Add(button);
            Assert.That(UIToolkitInitialFocus.ListTabStops(parent), Is.EqualTo(new VisualElement[] { button }));
        }

        [Test]
        public void InsertUIToolkitViewCore_AddsBlockerBeforeRootForDialogLayer()
        {
            var uiCommon = _uiCommonGo.GetComponent<UICommon>();
            var documentRoot = new VisualElement();
            uiCommon.BuildLayerContainers(documentRoot);

            var viewGo = new GameObject("ToolkitView_Test");
            var view = viewGo.AddComponent<TestToolkitView>();
            var viewRoot = new VisualElement { name = "ViewRoot" };
            view.SetTestRoot(viewRoot);
            view.SetLayer(UIView.UILayer.Dialog);

            var entry = new UICommon.UIViewEntry("dialog-owner", view);
            uiCommon.InsertUIToolkitViewCore(view, "dialog-owner", entry);

            var dialogContainer = documentRoot[3];
            Assert.That(entry.VisualBlocker, Is.Not.Null);
            Assert.That(dialogContainer.Contains(entry.VisualBlocker), Is.True);
            Assert.That(dialogContainer.Contains(viewRoot), Is.True);
            Assert.That(
                dialogContainer.IndexOf(entry.VisualBlocker),
                Is.LessThan(dialogContainer.IndexOf(viewRoot)));

            Object.DestroyImmediate(viewGo);
        }

        [Test]
        public void InsertUIToolkitViewCore_DoesNotCreateBlockerForDebugLayer()
        {
            var uiCommon = _uiCommonGo.GetComponent<UICommon>();
            var documentRoot = new VisualElement();
            uiCommon.BuildLayerContainers(documentRoot);

            var viewGo = new GameObject("ToolkitView_Debug");
            var view = viewGo.AddComponent<TestToolkitView>();
            view.SetTestRoot(new VisualElement { name = "DebugRoot" });
            view.SetLayer(UIView.UILayer.Debug);

            var entry = new UICommon.UIViewEntry("debug-owner", view);
            uiCommon.InsertUIToolkitViewCore(view, "debug-owner", entry);

            Assert.That(entry.VisualBlocker, Is.Null);

            Object.DestroyImmediate(viewGo);
        }

        [Test]
        public void BuildLayerContainers_LaterLayerIsRenderedAboveEarlierLayer()
        {
            var uiCommon = _uiCommonGo.GetComponent<UICommon>();
            var root = new VisualElement();
            uiCommon.BuildLayerContainers(root);

            var normalContainer = root[1];
            var modalContainer = root[2];

            normalContainer.Add(new VisualElement { name = "NormalView" });
            modalContainer.Add(new VisualElement { name = "ModalView" });

            Assert.That(root.IndexOf(normalContainer), Is.LessThan(root.IndexOf(modalContainer)));
        }

        [Test]
        public async Task AddUIView_FocusesOnlyAfterViewInCompletes()
        {
            var (uiCommon, view, button, documentRoot) = CreatePanelFocusView();
            var completion = new UniTaskCompletionSource();
            view.ViewInCompletionForTest = completion;

            var add = uiCommon.AddUIView("dialog-owner", view, CancellationToken.None).AsTask();

            Assert.That(add.IsCompleted, Is.False);
            Assert.That(documentRoot.panel!.focusController.focusedElement, Is.Not.SameAs(button));

            completion.TrySetResult();
            await add;

            Assert.That(documentRoot.panel!.focusController.focusedElement, Is.SameAs(button));
        }

        [Test]
        public void AddUIView_DoesNotFocusWhenViewInFails()
        {
            var (uiCommon, view, button, documentRoot) = CreatePanelFocusView();
            var completion = new UniTaskCompletionSource();
            view.ViewInCompletionForTest = completion;
            var add = uiCommon.AddUIView("dialog-owner", view, CancellationToken.None).AsTask();

            completion.TrySetException(new System.InvalidOperationException("ViewIn failed"));

            Assert.ThrowsAsync<System.InvalidOperationException>(async () => await add);
            Assert.That(documentRoot.panel!.focusController.focusedElement, Is.Not.SameAs(button));
            Assert.That(uiCommon.GetUIView("dialog-owner"), Is.Null);
        }

        [Test]
        public void AddUIView_DoesNotFocusWhenViewInIsCancelled()
        {
            var (uiCommon, view, button, documentRoot) = CreatePanelFocusView();
            var completion = new UniTaskCompletionSource();
            view.ViewInCompletionForTest = completion;
            using var cancellation = new CancellationTokenSource();
            var add = uiCommon.AddUIView("dialog-owner", view, cancellation.Token).AsTask();

            cancellation.Cancel();

            Assert.CatchAsync<System.OperationCanceledException>(async () => await add);
            Assert.That(documentRoot.panel!.focusController.focusedElement, Is.Not.SameAs(button));
            Assert.That(uiCommon.GetUIView("dialog-owner"), Is.Null);
        }

        private (UICommon uiCommon, GatedFocusToolkitView view, Button button, VisualElement documentRoot) CreatePanelFocusView()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _documentGo = new GameObject("FocusDocument");
            var document = _documentGo.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            var documentRoot = document.rootVisualElement;
            Assert.That(documentRoot.panel, Is.Not.Null);

            var uiCommon = _uiCommonGo.GetComponent<UICommon>();
            var renderer = _uiCommonGo.AddComponent<PanelRenderer>();
            typeof(UICommon)
                .GetField("_panelRenderer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(uiCommon, renderer);
            uiCommon.BuildLayerContainers(documentRoot);

            _viewGo = new GameObject("FocusView");
            var view = _viewGo.AddComponent<GatedFocusToolkitView>();
            var viewRoot = new VisualElement();
            var button = new Button { name = "ok-button", focusable = true, tabIndex = 1 };
            viewRoot.Add(button);
            view.SetTestRoot(viewRoot);
            view.SetLayer(UIView.UILayer.Dialog);
            view.InitialFocusNameForTest = "ok-button";
            return (uiCommon, view, button, documentRoot);
        }

        // UniTask remains in the Editor test assembly; shared TestSupport stays dependency-free.
        private sealed class GatedFocusToolkitView : TestToolkitView
        {
            public UniTaskCompletionSource? ViewInCompletionForTest { get; set; }

            public override async UniTask ViewIn(CancellationToken ct)
            {
                if (ViewInCompletionForTest != null)
                {
                    await ViewInCompletionForTest.Task.AttachExternalCancellation(ct);
                }
            }
        }
    }
}
