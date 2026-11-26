#nullable enable

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    /// <summary>directory の取得元の照会（head と revision の所在。temp の directory）。</summary>
    public sealed class IndexedContentOriginTests
    {
        private OriginFixture _fixture = null!;
        private string _root = "";
        private string _digest = "";

        [SetUp]
        public void SetUp()
        {
            _fixture = new OriginFixture();
            _root = _fixture.OriginRoot(1);
            _digest = OriginFixture.Publish(_root, "r1");
            OriginFixture.Publish(_root, "r0", "older");
        }

        [TearDown] public void TearDown() => _fixture.Dispose();

        private Task<ContentOriginAnswer> Find(string? revision = null, string? root = null) =>
            ContentOrigins.Directory("dir", root ?? _root).FindAsync(new ContentOriginQuery(OriginFixture.Set, OriginFixture.Target, revision), CancellationToken.None);

        [Test]
        public async Task HeadPresent_IsFoundWithTheDeclaredDigest()
        {
            OriginFixture.WriteHead(_root, "r1");
            var answer = await Find();
            Assert.That(answer.Kind, Is.EqualTo(ContentOriginAnswerKind.Found), answer.Reason);
            Assert.That(answer.Entry!.Revision, Is.EqualTo("r1"));
            Assert.That(answer.Entry.DeclaredManifestSha256, Is.EqualTo(_digest));
            Assert.That(answer.Entry.Location, Is.EqualTo("r1"));
        }

        [Test]
        public async Task NoHeadOrNoRoot_IsNotFound()
        {
            Assert.That((await Find()).Kind, Is.EqualTo(ContentOriginAnswerKind.NotFound));
            Assert.That((await Find(root: Path.Combine(_fixture.Root, "missing"))).Kind, Is.EqualTo(ContentOriginAnswerKind.NotFound));
        }

        [Test]
        public async Task UnsupportedOrBrokenHead_IsUnavailable()
        {
            OriginFixture.WriteHead(_root, "r1");
            var head = Path.Combine(_root, ".origin", "set", OriginFixture.Target + ".json");
            File.WriteAllText(head, File.ReadAllText(head).Replace("\"version\": 1", "\"version\": 2"));
            Assert.That((await Find()).Kind, Is.EqualTo(ContentOriginAnswerKind.Unavailable));
            File.WriteAllText(head, "{ broken");
            Assert.That((await Find()).Kind, Is.EqualTo(ContentOriginAnswerKind.Unavailable));
        }

        [Test]
        public async Task RevisionQuery_HeadRevisionCarriesTheDigest_OthersUseTheDirectoryConvention()
        {
            OriginFixture.WriteHead(_root, "r1");
            Assert.That((await Find("r1")).Entry!.DeclaredManifestSha256, Is.EqualTo(_digest));
            var older = await Find("r0");
            Assert.That(older.Kind, Is.EqualTo(ContentOriginAnswerKind.Found));
            Assert.That(older.Entry!.DeclaredManifestSha256, Is.Null);
            Assert.That(older.Entry.Location, Is.EqualTo("r0"));
            Assert.That((await Find("r9")).Kind, Is.EqualTo(ContentOriginAnswerKind.NotFound));
        }

        [Test]
        public async Task UnreadableLocation_IsUnavailable()
        {
            var origin = new IndexedContentOrigin("indexed", (_, _) => Task.FromResult(ContentOriginOpen.Unreadable("denied")),
                location => new LocalContentArtifactSource(Path.Combine(_root, location)));
            var answer = await origin.FindAsync(new ContentOriginQuery(OriginFixture.Set, OriginFixture.Target), CancellationToken.None);
            Assert.That(answer.Kind, Is.EqualTo(ContentOriginAnswerKind.Unavailable));
        }
    }
}
