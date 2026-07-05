#nullable enable

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

        [SetUp]
        public void SetUp()
        {
            _uiCommonGo = new GameObject("UICommon_Test");
            _uiCommonGo.AddComponent<UICommon>();
        }

        [TearDown]
        public void TearDown()
        {
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
            Assert.That(blocker.style.position.value, Is.EqualTo(Position.Absolute));
            Assert.That(blocker.style.backgroundColor.value, Is.EqualTo(Color.clear));
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
    }
}
