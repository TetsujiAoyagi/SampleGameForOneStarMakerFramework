#nullable enable

using NUnit.Framework;
using OneStarMaker.Runtime;

namespace OneStarMaker.Tests.Bootstrap
{
    /// <summary>
    /// 旧リモートカタログ経路が build-info.json を探す URL の規則（catalog と同じディレクトリ）を固定する。
    /// 経路自体は通常起動から外れているが、退役まで規則を黙って変えないためのテスト。
    /// </summary>
    [TestFixture]
    public sealed class LegacyRemoteCatalogProbeTests
    {
        // 末尾が / の URL（ディレクトリ）と、catalog の file 名で終わる URL の両方で、最後の / までを残す。
        [TestCase("https://cdn.example/content/", "https://cdn.example/content/build-info.json")]
        [TestCase("https://cdn.example/content/catalog_1.json", "https://cdn.example/content/build-info.json")]
        public void DeriveBuildInfoUrl_KeepsEverythingUpToTheLastSlash(string catalogUrl, string expected)
        {
            Assert.That(LegacyRemoteCatalogProbe.DeriveBuildInfoUrl(catalogUrl), Is.EqualTo(expected));
        }

        [Test]
        public void DeriveBuildInfoUrl_ReturnsEmptyWhenTheUrlHasNoSlash()
        {
            Assert.That(LegacyRemoteCatalogProbe.DeriveBuildInfoUrl("catalog_1.json"), Is.Empty);
        }

        [Test]
        public void DeriveBuildInfoUrl_ReturnsEmptyForAnEmptyUrl()
        {
            Assert.That(LegacyRemoteCatalogProbe.DeriveBuildInfoUrl(string.Empty), Is.Empty);
        }
    }
}
