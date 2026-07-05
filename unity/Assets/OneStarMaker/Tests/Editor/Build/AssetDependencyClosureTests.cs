#nullable enable

using NUnit.Framework;
using OneStarMaker.Editor.Build;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class AssetDependencyClosureTests
    {
        [Test]
        public void ShouldTreatAsContent_AssetsPrefab_ReturnsTrue()
        {
            Assert.That(AssetDependencyClosure.ShouldTreatAsContent("Assets/Foo/Bar.prefab"), Is.True);
        }

        [Test]
        public void ShouldTreatAsContent_PackageAsset_ReturnsFalse()
        {
            Assert.That(
                AssetDependencyClosure.ShouldTreatAsContent("Packages/com.unity.foo/Bar.asset"),
                Is.False);
        }

        [Test]
        public void ShouldTreatAsContent_CsScript_ReturnsFalse()
        {
            Assert.That(AssetDependencyClosure.ShouldTreatAsContent("Assets/Scripts/Foo.cs"), Is.False);
        }

        [Test]
        public void ShouldTreatAsContent_BackslashPath_ReturnsTrue()
        {
            Assert.That(AssetDependencyClosure.ShouldTreatAsContent(@"Assets\Foo\Bar.png"), Is.True);
        }

        [Test]
        public void ShouldTreatAsContent_NullOrEmpty_ReturnsFalse()
        {
            Assert.That(AssetDependencyClosure.ShouldTreatAsContent(null!), Is.False);
            Assert.That(AssetDependencyClosure.ShouldTreatAsContent(string.Empty), Is.False);
        }
    }
}
