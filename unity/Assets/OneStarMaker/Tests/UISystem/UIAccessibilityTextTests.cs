#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.UISystem;
using OneStarMaker.Tests.UISystem.TestDoubles;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.UISystem
{
    /// <summary>
    /// 要素ローカルのアクセシビリティ文言が、Track できる寿命で着脱できることを検証する。
    /// </summary>
    [TestFixture]
    public sealed class UIAccessibilityTextTests
    {
        [Test]
        public void Set_StoresNameAndHintUntilDisposed()
        {
            var element = new Button();

            var subscription = UIAccessibilityText.Set(element, "OK", "決定して閉じます");

            Assert.That(UIAccessibilityText.TryGet(element, out var name, out var hint), Is.True);
            Assert.That(name, Is.EqualTo("OK"));
            Assert.That(hint, Is.EqualTo("決定して閉じます"));

            subscription.Dispose();

            Assert.That(UIAccessibilityText.TryGet(element, out _, out _), Is.False);
        }

        [Test]
        public void Set_RemovingMiddleEntryKeepsTheNewerText()
        {
            var element = new Label();
            var first = UIAccessibilityText.Set(element, "先", "先のヒント");
            var second = UIAccessibilityText.Set(element, "後", null);

            first.Dispose();

            Assert.That(UIAccessibilityText.TryGet(element, out var name, out var hint), Is.True);
            Assert.That(name, Is.EqualTo("後"));
            Assert.That(hint, Is.Null);

            second.Dispose();

            Assert.That(UIAccessibilityText.TryGet(element, out _, out _), Is.False);
        }

        [Test]
        public void Bind_FollowsNameAndStopsAfterDispose()
        {
            var element = new Label();
            var name = new ReactiveProperty<string>("回復しますか？");
            var hint = new ReactiveProperty<string>("確認");

            var subscription = UIAccessibilityText.Bind(element, name, hint);

            Assert.That(UIAccessibilityText.TryGet(element, out var currentName, out var currentHint), Is.True);
            Assert.That(currentName, Is.EqualTo("回復しますか？"));
            Assert.That(currentHint, Is.EqualTo("確認"));

            name.Value = "中止しますか？";
            hint.Value = "再確認";

            Assert.That(UIAccessibilityText.TryGet(element, out currentName, out currentHint), Is.True);
            Assert.That(currentName, Is.EqualTo("中止しますか？"));
            Assert.That(currentHint, Is.EqualTo("再確認"));

            subscription.Dispose();
            name.Value = "破棄後";

            Assert.That(UIAccessibilityText.TryGet(element, out _, out _), Is.False);
            name.Dispose();
            hint.Dispose();
        }

        [Test]
        public void Track_ClearsTextWhenViewIsDestroyed()
        {
            var element = new Button();
            var gameObject = new GameObject("AccessibilityTrack");
            var view = gameObject.AddComponent<TestToolkitView>();
            view.TrackForTest(UIAccessibilityText.Set(element, "OK", "決定"));

            typeof(UIToolkitView)
                .GetMethod("OnDestroy", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(view, null);

            Assert.That(UIAccessibilityText.TryGet(element, out _, out _), Is.False);
            UnityEngine.Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Set_RejectsNullElementAndNullName()
        {
            var element = new Label();

            Assert.Throws<ArgumentNullException>(() => UIAccessibilityText.Set(null!, "名前"));
            Assert.Throws<ArgumentNullException>(() => UIAccessibilityText.Set(element, null!));
            Assert.Throws<ArgumentNullException>(() => UIAccessibilityText.TryGet(null!, out _, out _));
        }
    }
}
