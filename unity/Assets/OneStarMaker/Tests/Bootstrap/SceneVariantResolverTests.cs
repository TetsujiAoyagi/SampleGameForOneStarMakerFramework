#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime;

namespace OneStarMaker.Tests.Bootstrap
{
    [TestFixture]
    public sealed class SceneVariantResolverTests
    {
        [TestCase("", null, "")]
        [TestCase("Config", null, "Config")]
        [TestCase("Config", "", "")]
        [TestCase("Config", "Whitebox", "Whitebox")]
        [TestCase(" spaced ", null, " spaced ")]
        public void EditorResolution_DistinguishesNullAndEmpty(string configured, string? editor, string expected)
        {
            Assert.That(
                SceneVariantResolver.Resolve(configured, () => editor, useEditorResolver: true),
                Is.EqualTo(expected));
        }

        [Test]
        public void PlayerResolution_DoesNotInvokeEditorResolver()
        {
            var calls = 0;
            var actual = SceneVariantResolver.Resolve(
                "Whitebox",
                () => { calls++; return string.Empty; },
                useEditorResolver: false);

            Assert.That(actual, Is.EqualTo("Whitebox"));
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void EditorResolution_QueriesProfileDelegateExactlyOnce()
        {
            var calls = 0;

            var actual = SceneVariantResolver.Resolve(
                "Config",
                () => { calls++; return "Whitebox"; },
                useEditorResolver: true);

            Assert.That(actual, Is.EqualTo("Whitebox"));
            Assert.That(calls, Is.EqualTo(1));
        }
    }
}
