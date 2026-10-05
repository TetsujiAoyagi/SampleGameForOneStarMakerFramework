#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime.UISystem;
using SampleGame.OutGame.ConfirmDialog;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SampleGame.Tests.Editor.OutGame
{
    /// <summary>
    /// ConfirmDialog のタブ順と、メッセージ / OK / Cancel のアクセシビリティ名を検証する。
    /// </summary>
    [TestFixture]
    public sealed class ConfirmDialogAccessibilityTests
    {
        private const string UxmlPath = "Assets/SampleGame/OutGame/ConfirmDialog/ConfirmDialog.uxml";

        [Test]
        public void Uxml_SetsTabOrderAndLeavesMessageOutOfTheRing()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(tree, Is.Not.Null);

            var root = tree.CloneTree();
            var message = root.Q<Label>("message-label");
            var ok = root.Q<Button>("ok-button");
            var cancel = root.Q<Button>("cancel-button");

            Assert.That(message, Is.Not.Null);
            Assert.That(ok, Is.Not.Null);
            Assert.That(cancel, Is.Not.Null);
            Assert.That(message!.focusable, Is.False);
            Assert.That(message.tabIndex, Is.EqualTo(-1));
            Assert.That(ok!.focusable, Is.True);
            Assert.That(ok.tabIndex, Is.EqualTo(1));
            Assert.That(cancel!.focusable, Is.True);
            Assert.That(cancel.tabIndex, Is.EqualTo(2));
            Assert.That(
                UIToolkitInitialFocus.ListTabStops(root),
                Is.EqualTo(new VisualElement[] { ok, cancel }));
            Assert.That(UIToolkitInitialFocus.Select(root, "ok-button"), Is.SameAs(ok));
        }

        [Test]
        public void View_AssignsAccessibilityTextAndClearsItOnDestroy()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var gameObject = new GameObject("ConfirmDialogAccessibility");
            var view = gameObject.AddComponent<ConfirmDialogView>();
            view.AssignVisualTreeAssetForEditor(tree);
            view.Initialize();

            var message = view.Root.Q<Label>("message-label")
                ?? throw new System.InvalidOperationException("message-label が見つかりません。");
            var ok = view.Root.Q<Button>("ok-button")
                ?? throw new System.InvalidOperationException("ok-button が見つかりません。");
            var cancel = view.Root.Q<Button>("cancel-button")
                ?? throw new System.InvalidOperationException("cancel-button が見つかりません。");

            Assert.That(UIAccessibilityText.TryGet(message, out var messageName, out var messageHint), Is.True);
            Assert.That(messageName, Is.EqualTo("HPを回復しますか？"));
            Assert.That(messageHint, Is.Null);
            Assert.That(UIAccessibilityText.TryGet(ok, out var okName, out var okHint), Is.True);
            Assert.That(okName, Is.EqualTo("OK"));
            Assert.That(okHint, Is.EqualTo("決定して閉じます"));
            Assert.That(UIAccessibilityText.TryGet(cancel, out var cancelName, out var cancelHint), Is.True);
            Assert.That(cancelName, Is.EqualTo("Cancel"));
            Assert.That(cancelHint, Is.EqualTo("取り消して閉じます"));

            Object.DestroyImmediate(gameObject);

            Assert.That(UIAccessibilityText.TryGet(message, out _, out _), Is.False);
            Assert.That(UIAccessibilityText.TryGet(ok, out _, out _), Is.False);
            Assert.That(UIAccessibilityText.TryGet(cancel, out _, out _), Is.False);
        }
    }
}
