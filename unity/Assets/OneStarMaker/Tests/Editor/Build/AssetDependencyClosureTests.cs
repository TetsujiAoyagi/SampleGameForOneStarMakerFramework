#nullable enable

using NUnit.Framework;
using OneStarMaker.Editor.Build;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// ビルド時に「配信コンテンツとして扱うか」のパス判定を検証する。
    ///
    /// <para>
    /// Assets 配下の資産だけが対象で、Packages 由来とスクリプトは除外する。
    /// 区切り文字が円記号でも同じ判定になることを含む。
    /// </para>
    /// </summary>
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
