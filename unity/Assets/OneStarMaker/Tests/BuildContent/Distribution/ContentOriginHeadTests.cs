#nullable enable

using System;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Tests.BuildContent.Distribution
{
    /// <summary>head の形の規則（作りと読みの往復、読みの規則の表）。</summary>
    public sealed class ContentOriginHeadTests
    {
        private const string Set = OriginFixture.Set;
        private const string Target = OriginFixture.Target;
        private static readonly string Digest = new('a', 64);

        private static ContentOriginHeadRead Read(string json) => ContentOriginHead.Read(Encoding.UTF8.GetBytes(json), Set, Target);

        private static string Head(string version = "1", string set = "\"set\"", string revision = "\"r1\"", string digest = "", string path = "\"r1\"") =>
            "{\"version\":" + version + ",\"contentSet\":" + set + ",\"target\":\"" + Target + "\",\"revision\":" + revision
            + ",\"manifestSha256\":" + (digest.Length == 0 ? "\"" + Digest + "\"" : digest) + ",\"path\":" + path + "}";

        [Test]
        public void CreateThenRead_RoundTripsTheManifestIdentityAndDigest()
        {
            var manifest = OriginFixture.ManifestBytes("r1", Encoding.UTF8.GetBytes("payload"));
            var draft = ContentOriginHead.Create(manifest, "r1");
            Assert.That(draft.RelativePath, Is.EqualTo(".origin/set/StandaloneWindows64-Player.json"));
            var read = ContentOriginHead.Read(draft.Bytes, Set, Target);
            Assert.That(read.State, Is.EqualTo(ContentOriginHeadState.Present), read.Reason);
            Assert.That(read.Head!.revision, Is.EqualTo("r1"));
            Assert.That(read.Head.manifestSha256, Is.EqualTo(ContentDeliveryFiles.Sha256(manifest)));
            Assert.That(read.Head.path, Is.EqualTo("r1"));
        }

        [Test]
        public void Create_RejectsAnInvalidManifestOrPath()
        {
            Assert.Throws<ContentDeliveryException>(() => ContentOriginHead.Create(Encoding.UTF8.GetBytes("{}"), "r1"));
            Assert.Throws<ArgumentException>(() => ContentOriginHead.Create(OriginFixture.ManifestBytes("r1", new byte[1]), "../r1"));
        }

        [TestCase("0", ContentOriginHeadState.None)]
        [TestCase("2", ContentOriginHeadState.Unsupported)]
        [TestCase("-1", ContentOriginHeadState.Invalid)]
        [TestCase("\"1\"", ContentOriginHeadState.Invalid)]
        public void Version(string version, ContentOriginHeadState expected) => Assert.That(Read(Head(version)).State, Is.EqualTo(expected));

        [Test]
        public void ValidHead_IsPresent() => Assert.That(Read(Head()).State, Is.EqualTo(ContentOriginHeadState.Present));

        [TestCase("revision is a number")]
        [TestCase("upper-case digest")]
        [TestCase("absolute path")]
        [TestCase("parent path")]
        [TestCase("other content set")]
        [TestCase("missing field")]
        [TestCase("not json")]
        [TestCase("duplicate key")]
        public void MalformedHead_IsInvalid(string shape)
        {
            var json = shape switch
            {
                "revision is a number" => Head(revision: "5"),
                "upper-case digest" => Head(digest: "\"" + new string('A', 64) + "\""),
                "absolute path" => Head(path: "\"C:/r1\""),
                "parent path" => Head(path: "\"../r1\""),
                "other content set" => Head(set: "\"other\""),
                "missing field" => "{\"version\":1,\"contentSet\":\"set\",\"target\":\"" + Target + "\",\"revision\":\"r1\",\"path\":\"r1\"}",
                "not json" => "{ broken",
                _ => "{\"version\":1,\"version\":1}",
            };
            Assert.That(Read(json).State, Is.EqualTo(ContentOriginHeadState.Invalid), shape);
        }

        [Test]
        public void HeadOverTheLimit_IsInvalid() =>
            Assert.That(ContentOriginHead.Read(new byte[ContentOriginHead.MaxBytes + 1], Set, Target).State, Is.EqualTo(ContentOriginHeadState.Invalid));
    }
}
