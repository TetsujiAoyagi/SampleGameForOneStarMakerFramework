#nullable enable

using System;
using NUnit.Framework;
using OneStarMaker.Runtime.BuildContent;

namespace OneStarMaker.Tests.BuildContent
{
    /// <summary>
    /// 上書きの root の base の記録と、その照合の純粋な関数（Unity の native も install も要らない）。
    /// 5 欄を 1 欄ずつ変えた記録、digest の大文字・前置き、宣言の片方だけ無い組を不一致と答え、違う欄の名前を返すことを見る。
    /// </summary>
    public sealed class BuildContentOverlayBaseTests
    {
        private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static BuildContentOverlayBase Record(string? declarationName = null, string digest = Digest)
            => new("set", "rev", "target", digest, declarationName, "overlay-set", "6000.6.9f1");

        [Test]
        public void SameValues_Match()
        {
            Assert.That(BuildContentOverlayBase.FindMismatch(Record(), "set", "rev", "target", Digest, null), Is.Null);
            Assert.That(BuildContentOverlayBase.FindMismatch(Record("purpose"), "set", "rev", "target", Digest, "purpose"), Is.Null);
        }

        [TestCase("SET", "rev", "target", Digest, null, "contentSet")]
        [TestCase("set", "rev2", "target", Digest, null, "revision")]
        [TestCase("set", "rev", "Target", Digest, null, "target")]
        [TestCase("set", "rev", "target", "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", null, "manifestSha256")]
        [TestCase("set", "rev", "target", "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF", null, "manifestSha256")]
        [TestCase("set", "rev", "target", "sha256:" + Digest, null, "manifestSha256")]
        [TestCase("set", "rev", "target", Digest, "purpose", "declarationName")]
        public void OneFieldDifferent_IsMismatch_NamingTheField(string set, string revision, string target, string digest, string? declaration, string field)
            => Assert.That(BuildContentOverlayBase.FindMismatch(Record(), set, revision, target, digest, declaration), Is.EqualTo(field));

        [Test]
        public void DeclarationOnOneSideOnly_IsMismatch()
        {
            Assert.That(BuildContentOverlayBase.FindMismatch(Record("purpose"), "set", "rev", "target", Digest, null), Is.EqualTo("declarationName"));
            Assert.That(BuildContentOverlayBase.FindMismatch(Record("purpose"), "set", "rev", "target", Digest, "other"), Is.EqualTo("declarationName"));
        }

        [TestCase("")]
        [TestCase("0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF")]
        [TestCase("sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
        [TestCase("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcde")]
        [TestCase("digest-1")]
        public void NonCanonicalDigest_IsRejectedByTheRecord(string digest)
        {
            Assert.That(BuildContentOverlayBase.IsManifestSha256(digest), Is.False);
            Assert.Throws<ArgumentException>(() => Record(digest: digest));
        }

        [Test]
        public void EmptyFields_AreRejected_AndTheSerializedDefaultIsNoRecord()
        {
            Assert.Throws<ArgumentException>(() => new BuildContentOverlayBase("", "rev", "target", Digest, null, "o", "u"));
            Assert.Throws<ArgumentException>(() => new BuildContentOverlayBase("set", "rev", "target", Digest, " ", "o", "u"));
            Assert.Throws<ArgumentException>(() => new BuildContentOverlayBase("set", "rev", "target", Digest, null, "", "u"));
            Assert.Throws<ArgumentException>(() => new BuildContentOverlayBase("set", "rev", "target", Digest, null, "o", ""));
            Assert.That(new BuildContentOverlayBase().IsRecorded, Is.False, "Unity の直列化の既定値は記録無し");
            Assert.That(Record().IsRecorded, Is.True);
            Assert.That(Record().BaseDeclarationName, Is.Null);
        }
    }
}
