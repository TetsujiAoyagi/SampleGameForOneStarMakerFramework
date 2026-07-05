#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.UISystem.Mvvm;
using R3;
using UnityEngine.UIElements;

namespace OneStarMaker.Tests.UISystem
{
    /// <summary>
    /// <see cref="BindingExtensions"/> の EditMode バインディング挙動を検証する。
    /// </summary>
    [TestFixture]
    public class BindingExtensionsTests
    {
        [Test]
        public void BindText_UpdatesLabelOnValueChangeAndSkipsSameValue()
        {
            var label = new Label();
            var source = new ReactiveProperty<string>("hello");

            using (var binding = label.BindText(source))
            {
                Assert.That(label.text, Is.EqualTo("hello"));

                source.Value = "world";
                Assert.That(label.text, Is.EqualTo("world"));

                source.Value = "world";
                Assert.That(label.text, Is.EqualTo("world"));
            }

            source.Value = "after-dispose";
            Assert.That(label.text, Is.EqualTo("world"));
            source.Dispose();
        }

        [Test]
        public void BindText_WithFormatter_SkipsSameSourceValue()
        {
            var label = new Label();
            var source = new ReactiveProperty<int>(1);

            using (label.BindText(source, static value => $"HP:{value}"))
            {
                Assert.That(label.text, Is.EqualTo("HP:1"));

                source.Value = 2;
                Assert.That(label.text, Is.EqualTo("HP:2"));

                source.Value = 2;
                Assert.That(label.text, Is.EqualTo("HP:2"));
            }

            source.Dispose();
        }

        [Test]
        public void BindText_DoesNotUpdateAfterDispose()
        {
            var label = new Label();
            var source = new ReactiveProperty<string>("initial");
            var binding = label.BindText(source);

            binding.Dispose();
            source.Value = "changed";

            Assert.That(label.text, Is.EqualTo("initial"));
            source.Dispose();
        }

        [Test]
        public void BindVisible_TogglesDisplayStyle()
        {
            var element = new VisualElement();
            var source = new ReactiveProperty<bool>(true);

            using (var binding = element.BindVisible(source))
            {
                Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));

                source.Value = false;
                Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));

                source.Value = false;
                Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            }

            source.Value = true;
            Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            source.Dispose();
        }

        [Test]
        public void BindVisible_RebindDoesNotDuplicateUpdates()
        {
            var element = new VisualElement();
            var source = new ReactiveProperty<bool>(true);

            var firstBinding = element.BindVisible(source);
            firstBinding.Dispose();

            using (element.BindVisible(source))
            {
                source.Value = false;
                Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));

                source.Value = false;
                Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
            }

            source.Dispose();
        }

        [Test]
        public void BindText_RebindDoesNotDuplicateUpdates()
        {
            var label = new Label { text = string.Empty };
            var source = new ReactiveProperty<string>("v1");

            var firstBinding = label.BindText(source);
            Assert.That(label.text, Is.EqualTo("v1"));
            firstBinding.Dispose();

            using (label.BindText(source))
            {
                Assert.That(label.text, Is.EqualTo("v1"));

                source.Value = "v2";
                Assert.That(label.text, Is.EqualTo("v2"));

                source.Value = "v2";
                Assert.That(label.text, Is.EqualTo("v2"));
            }

            source.Dispose();
        }

        // BindClick: Button.clicked は C# event のためテスト側から invoke できない。
        // NavigationSubmitEvent / ClickEvent による合成も EditMode では不安定なため省略する。
    }
}
