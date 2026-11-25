#nullable enable

using System;
using System.IO;
using NUnit.Framework;
using SampleGame.DependOnAll.Editor.Build;

namespace SampleGame.Tests.Editor.Build
{
    /// <summary>入口が上書きを重ねる Player の決定（D6 の (1)）。規則は値の列で、folder の読みは一時 folder で確かめる。</summary>
    public sealed class PlayablePlayerLocatorTests
    {
        private const string Older = "20261124T010000000Z-aaaa";
        private const string Newer = "20261124T020000000Z-bbbb";

        private static PlayablePlayerCandidate Candidate(string name, bool compat = true, string? identity = null, string sha = "abc", string? failure = null) =>
            new(name, @"D:\players\" + name, failure == null ? identity ?? name : null, failure == null ? sha : null, compat, failure);

        [Test]
        public void NoPlayer_IsNotFound_WithTheBuildGuidance()
        {
            var choice = PlayablePlayerLocator.Choose(Array.Empty<PlayablePlayerCandidate>());
            Assert.That(choice.IsFound, Is.False);
            StringAssert.Contains(PlayablePlayerLocator.BuildGuidance, choice.Failure);
        }

        [Test]
        public void TwoPlayers_TheNewerIdentityIsChosen()
        {
            var choice = PlayablePlayerLocator.Choose(new[] { Candidate(Newer, sha: "new"), Candidate(Older, sha: "old") });
            Assert.That(choice.IsFound, Is.True, choice.Failure);
            Assert.That(choice.Identity, Is.EqualTo(Newer));
            Assert.That(choice.TransportManifestSha256, Is.EqualTo("new"));
            Assert.That(choice.Directory, Is.EqualTo(@"D:\players\" + Newer));
        }

        [Test]
        public void NewerWithoutCompatibility_StopsAndDoesNotFallBackToTheOlder()
        {
            var choice = PlayablePlayerLocator.Choose(new[] { Candidate(Older), Candidate(Newer, compat: false) });
            Assert.That(choice.IsFound, Is.False);
            StringAssert.Contains(Newer, choice.Failure);
            StringAssert.Contains(PlayerCompatibility.FileName, choice.Failure);
        }

        [Test]
        public void NewerWithAnUnreadableOrForeignReceipt_Stops()
        {
            Assert.That(PlayablePlayerLocator.Choose(new[] { Candidate(Older), Candidate(Newer, failure: "broken") }).IsFound, Is.False);
            Assert.That(PlayablePlayerLocator.Choose(new[] { Candidate(Older), Candidate(Newer, identity: Older) }).IsFound, Is.False);
            Assert.That(PlayablePlayerLocator.Choose(new[] { Candidate(Newer, sha: "") }).IsFound, Is.False);
        }

        [Test]
        public void Scan_ReadsReceiptsAndCompatibilityFromTheFolders()
        {
            var root = Path.Combine(Path.GetTempPath(), "osm-s42-loc-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Assert.That(PlayablePlayerLocator.Scan(Path.Combine(root, "missing")), Is.Empty);
                Write(root, Older, "{\"identity\":\"" + Older + "\",\"transportManifestSha256\":\"old\"}", compat: true);
                Write(root, Newer, "{\"identity\":\"" + Newer + "\",\"transportManifestSha256\":\"new\"}", compat: false);
                var choice = PlayablePlayerLocator.Locate(root);
                Assert.That(choice.IsFound, Is.False, "新しい方に compat が無い");
                File.WriteAllText(Path.Combine(root, Newer, PlayerCompatibility.FileName), "{}");
                choice = PlayablePlayerLocator.Locate(root);
                Assert.That(choice.Identity, Is.EqualTo(Newer));
                Assert.That(choice.TransportManifestSha256, Is.EqualTo("new"));

                File.WriteAllText(Path.Combine(root, Newer, PlayablePlayerLocator.ReceiptFileName), "not json");
                Assert.That(PlayablePlayerLocator.Locate(root).IsFound, Is.False, "読めない receipt で古い方へ戻らない");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void Write(string root, string name, string receipt, bool compat)
        {
            var directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, PlayablePlayerLocator.ReceiptFileName), receipt);
            if (compat) File.WriteAllText(Path.Combine(directory, PlayerCompatibility.FileName), "{}");
        }
    }
}
