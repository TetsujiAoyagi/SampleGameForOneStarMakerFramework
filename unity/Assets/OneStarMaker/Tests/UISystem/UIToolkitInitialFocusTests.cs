#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Tests.UISystem.TestDoubles;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.UISystem
{
    /// <summary>
    /// Dialog / Modal の初期フォーカスが、名前付き既定かタブ順の先頭になることを検証する。
    /// </summary>
    [TestFixture]
    public sealed class UIToolkitInitialFocusTests
    {
        [Test]
        public void ShouldApply_IsOnlyModalAndDialog()
        {
            Assert.That(UIToolkitInitialFocus.ShouldApply(UIView.UILayer.Background), Is.False);
            Assert.That(UIToolkitInitialFocus.ShouldApply(UIView.UILayer.Normal), Is.False);
            Assert.That(UIToolkitInitialFocus.ShouldApply(UIView.UILayer.Modal), Is.True);
            Assert.That(UIToolkitInitialFocus.ShouldApply(UIView.UILayer.Dialog), Is.True);
            Assert.That(UIToolkitInitialFocus.ShouldApply(UIView.UILayer.Loading), Is.False);
            Assert.That(UIToolkitInitialFocus.ShouldApply(UIView.UILayer.Debug), Is.False);
        }

        [Test]
        public void ListTabStops_OrdersPositiveTabIndexBeforeZeroAndSkipsNegative()
        {
            var root = new VisualElement();
            var zero = Button("zero", 0);
            var second = Button("second", 2);
            var first = Button("first", 1);
            var skipped = Button("skipped", -1);
            var inert = new Label { name = "message", focusable = false, tabIndex = 0 };
            root.Add(zero);
            root.Add(second);
            root.Add(first);
            root.Add(skipped);
            root.Add(inert);

            var order = UIToolkitInitialFocus.ListTabStops(root);

            Assert.That(order, Is.EqualTo(new VisualElement[] { first, second, zero }));
        }

        [Test]
        public void Select_UsesNamedDefaultWhenItIsATabStop()
        {
            var root = new VisualElement();
            var ok = Button("ok-button", 1);
            var cancel = Button("cancel-button", 2);
            root.Add(ok);
            root.Add(cancel);

            Assert.That(UIToolkitInitialFocus.Select(root, "cancel-button"), Is.SameAs(cancel));
        }

        [Test]
        public void Select_FallsBackWhenNamedElementIsNotATabStop()
        {
            var root = new VisualElement();
            var message = new Label { name = "message-label", focusable = true, tabIndex = -1 };
            var ok = Button("ok-button", 1);
            root.Add(message);
            root.Add(ok);

            Assert.That(UIToolkitInitialFocus.Select(root, "message-label"), Is.SameAs(ok));
            Assert.That(UIToolkitInitialFocus.Select(root, "missing"), Is.SameAs(ok));
        }

        [Test]
        public void ListTabStops_SkipsHiddenAndDisabledBranches()
        {
            var root = new VisualElement();
            var hidden = new VisualElement();
            hidden.style.display = DisplayStyle.None;
            hidden.Add(Button("hidden-ok", 1));
            var disabled = new VisualElement();
            disabled.SetEnabled(false);
            disabled.Add(Button("disabled-ok", 1));
            var visible = Button("visible-ok", 1);
            root.Add(hidden);
            root.Add(disabled);
            root.Add(visible);

            Assert.That(UIToolkitInitialFocus.ListTabStops(root), Is.EqualTo(new VisualElement[] { visible }));
        }

        [Test]
        public void TryApply_ReturnsFalseWhenTheTargetHasNoPanel()
        {
            var root = new VisualElement();
            root.Add(Button("ok-button", 1));

            Assert.That(UIToolkitInitialFocus.TryApply(root, "ok-button"), Is.False);
        }

        [Test]
        public void ApplyInitialFocus_DoesNotThrowForDialogWithoutPanel()
        {
            var gameObject = new GameObject("FocusView");
            var view = gameObject.AddComponent<TestToolkitView>();
            var root = new VisualElement();
            root.Add(Button("ok-button", 1));
            view.SetTestRoot(root);
            view.SetLayer(UIView.UILayer.Dialog);
            view.InitialFocusNameForTest = "ok-button";

            Assert.DoesNotThrow(() => view.ApplyInitialFocus());

            view.SetLayer(UIView.UILayer.Normal);
            Assert.DoesNotThrow(() => view.ApplyInitialFocus());
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void TryApply_FocusesNamedButtonWhenDocumentPanelExists()
        {
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var gameObject = new GameObject("FocusDocument");
            try
            {
                var document = gameObject.AddComponent<UIDocument>();
                document.panelSettings = settings;
                var root = document.rootVisualElement;
                Assert.That(root, Is.Not.Null);
                Assert.That(root!.panel, Is.Not.Null);

                var button = Button("ok-button", 1);
                root.Add(button);

                Assert.That(UIToolkitInitialFocus.TryApply(root, "ok-button"), Is.True);
                Assert.That(root.panel!.focusController.focusedElement, Is.SameAs(button));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
                Object.DestroyImmediate(settings);
            }
        }

        private static Button Button(string name, int tabIndex)
        {
            return new Button
            {
                name = name,
                focusable = true,
                tabIndex = tabIndex,
            };
        }
    }
}
