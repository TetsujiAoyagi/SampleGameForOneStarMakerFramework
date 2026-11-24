#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Editor.Build.Content;
using SampleGame.DependOnAll.Editor.Build;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    public sealed class PlayerBuildIntegrationPolicyTests
    {
        private string _root = null!;

        [SetUp] public void SetUp() { _root = Path.Combine(Path.GetTempPath(), "osm-bs4-policy", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_root); }
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        [Test]
        public void InputValidator_MatchingReportsAndFiles_Accepts()
        {
            var result = Result("id");
            Assert.DoesNotThrow(() => PlayerBuildInputValidator.Validate(result, Report("id", result), Report("id", result)));
        }

        [TestCase("wrong", "StandaloneWindows64-Player")]
        [TestCase("id", "WrongTarget")]
        public void InputValidator_MismatchedIdentityOrTarget_Rejects(string identity, string target)
        {
            var result = Result("id");
            Assert.Throws<InvalidOperationException>(() => PlayerBuildInputValidator.Validate(result, Report(identity, result, target), Report(identity, result, target)));
        }

        [Test]
        public void PackedPolicy_RejectsSelectedRootAndBootstrapInTwoPacks()
        {
            var ok = new[] { new PlayerPackedAssetData("one", new[] { "generated", "generated" }) };
            Assert.Throws<InvalidOperationException>(() => PlayerBuildReportVerifier.VerifyPackedAssets(ok, new[] { "selected" }, "source", "missing"));
            Assert.Throws<InvalidOperationException>(() => PlayerBuildReportVerifier.VerifyPackedAssets(
                new[] { new PlayerPackedAssetData("one", new[] { "generated", "selected" }) }, new[] { "selected" }, "source", "generated"));
            Assert.Throws<InvalidOperationException>(() => PlayerBuildReportVerifier.VerifyPackedAssets(
                new[] { new PlayerPackedAssetData("one", new[] { "generated" }), new PlayerPackedAssetData("two", new[] { "generated" }) },
                new[] { "selected" }, "source", "generated"));
        }

        [Test]
        public void PackedPolicy_DuplicateRowsInOnePack_Accepts()
        {
            Assert.That(PlayerBuildReportVerifier.VerifyPackedAssets(
                new[] { new PlayerPackedAssetData("one", new[] { "generated", "generated" }) },
                new[] { "selected" }, "source", "generated"), Is.EqualTo(new[] { "selected" }));
        }

        // ─── 遊べる Player の pack の検査（bootstrap を写さない build） ───

        private const string MutationRoot = "Assets/Resources/BS4/id";

        private static PlayablePackChecks PlayableChecks(params string[] forbidden) =>
            new(forbidden, MutationRoot, "start");

        private static PlayerPackedAssetData Pack(string name, string[] guids, string[]? paths = null) =>
            new(name, guids, paths ?? guids.Select(g => "Assets/Other/" + g + ".asset"));

        [Test]
        public void PlayablePackedPolicy_ForbiddenGuidPacked_Rejects()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerBuildReportVerifier.VerifyPlayablePackedAssets(
                new[] { Pack("one", new[] { "start", "resource-b" }) }, new[] { "selected" }, "source", PlayableChecks("resource-a", "resource-b")));
            StringAssert.Contains("resource-b", ex!.Message);
        }

        [Test]
        public void PlayablePackedPolicy_AssetUnderMutationRootPacked_Rejects()
        {
            // 写しは新しい GUID を持つので、GUID の検査では見つからない。元の path で拒む。
            var ex = Assert.Throws<InvalidOperationException>(() => PlayerBuildReportVerifier.VerifyPlayablePackedAssets(
                new[] { Pack("one", new[] { "start", "copy" }, new[] { "Assets/PlayerStart/PlayablePlayerStart.unity", MutationRoot + "/SceneResourceMap.asset" }) },
                new[] { "selected" }, "source", PlayableChecks("resource-a")));
            StringAssert.Contains(MutationRoot + "/SceneResourceMap.asset", ex!.Message);
        }

        [Test]
        public void PlayablePackedPolicy_StartSceneInTwoPacks_Rejects()
        {
            Assert.Throws<InvalidOperationException>(() => PlayerBuildReportVerifier.VerifyPlayablePackedAssets(
                new[] { Pack("one", new[] { "start" }), Pack("two", new[] { "start" }) }, new[] { "selected" }, "source", PlayableChecks("resource-a")));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void PlayablePackedPolicy_StartSceneInZeroOrOnePack_Accepts(int startPacks)
        {
            var packs = startPacks == 0
                ? new[] { Pack("one", new[] { "other" }) }
                : new[] { Pack("one", new[] { "start", "start" }), Pack("two", new[] { "other" }) };
            var (checkedGuids, playable) = PlayerBuildReportVerifier.VerifyPlayablePackedAssets(
                packs, new[] { "selected" }, "source", PlayableChecks("resource-a", "resource-b"));
            Assert.That(checkedGuids, Is.EqualTo(new[] { "selected" }));
            Assert.That(playable.StartScenePackCount, Is.EqualTo(startPacks));
            Assert.That(playable.ForbiddenGuidCount, Is.EqualTo(2));
        }

        [Test]
        public void PlayablePackedPolicy_NoForbiddenGuids_RejectsTheEmptyCheck()
        {
            Assert.Throws<InvalidOperationException>(() => PlayerBuildReportVerifier.VerifyPlayablePackedAssets(
                new[] { Pack("one", new[] { "start" }) }, new[] { "selected" }, "source", PlayableChecks()));
        }

        [Test]
        public void PlayablePackedPolicy_KeepsTheCurrentChecks()
        {
            // selected の root の重複と、元の UICommon の Scene は遊べる Player でも拒む。
            Assert.Throws<InvalidOperationException>(() => PlayerBuildReportVerifier.VerifyPlayablePackedAssets(
                new[] { Pack("one", new[] { "selected" }) }, new[] { "selected" }, "source", PlayableChecks("resource-a")));
            Assert.Throws<InvalidOperationException>(() => PlayerBuildReportVerifier.VerifyPlayablePackedAssets(
                new[] { Pack("one", new[] { "source" }) }, new[] { "selected" }, "source", PlayableChecks("resource-a")));
        }

        [Test]
        public void SmokePackedPolicy_IgnoresSourcePaths()
        {
            // 足した入力を渡さない動作確認の Player の検査は、mutation の root の下の写し（生成した UICommon）を今までどおり受け入れる。
            Assert.That(PlayerBuildReportVerifier.VerifyPackedAssets(
                new[] { Pack("one", new[] { "generated", "resource-a" }, new[] { MutationRoot + "/UICommon.unity", MutationRoot + "/Title.asset" }) },
                new[] { "selected" }, "source", "generated"), Is.EqualTo(new[] { "selected" }));
        }

        [TestCase("SampleGame_BackUpThisFolder_ButDontShipItWithYourGame", true)]
        [TestCase("samplegame_backupthisfolder_butdontshipitwithyourgame", true)]
        [TestCase("SampleGame_Data", false)]
        public void PublisherBackupPredicate_IsExactSuffix(string name, bool expected) =>
            Assert.That(PlayerBuildPublisher.IsNonShippingUnityBackupDirectory(name), Is.EqualTo(expected));

        [Test]
        public void Publisher_ExcludesOnlyRootBackupDirectory()
        {
            var player = Path.Combine(_root, "work"); var content = Path.Combine(_root, "content-source");
            Directory.CreateDirectory(player); Directory.CreateDirectory(content);
            File.WriteAllText(Path.Combine(player, "Game.exe"), "x"); File.WriteAllText(Path.Combine(content, "catalog"), "x");
            Directory.CreateDirectory(Path.Combine(player, "Game_BackUpThisFolder_ButDontShipItWithYourGame"));
            var data = Path.Combine(player, "Game_Data"); Directory.CreateDirectory(data);
            Directory.CreateDirectory(Path.Combine(data, "Nested_BackUpThisFolder_ButDontShipItWithYourGame"));
            File.WriteAllText(Path.Combine(data, "Nested_BackUpThisFolder_ButDontShipItWithYourGame", "kept"), "x");

            var final = new PlayerBuildPublisher().Publish(Path.Combine(_root, "publish"), "id", player, content, "{}");
            Assert.That(Directory.Exists(Path.Combine(final, "Game_BackUpThisFolder_ButDontShipItWithYourGame")), Is.False);
            Assert.That(File.Exists(Path.Combine(final, "Game_Data", "Nested_BackUpThisFolder_ButDontShipItWithYourGame", "kept")), Is.True);
        }

        [TestCase(FailurePoint.Copy)]
        [TestCase(FailurePoint.Write)]
        [TestCase(FailurePoint.Move)]
        public void Publisher_Failure_CleansOnlyStaging(FailurePoint point)
        {
            var fs = new FailingFileSystem(point);
            Assert.Throws<IOException>(() => new PlayerBuildPublisher(fs).Publish("C:/publish", "new", "C:/work", "C:/content", "{}"));
            Assert.That(fs.Deleted, Is.EqualTo("C:\\publish\\staging\\new"));
            Assert.That(fs.DeletedPaths, Has.None.EqualTo("C:\\publish\\players\\existing"));
        }

        [Test]
        public void Publisher_ExistingFinal_IsRejectedWithoutMutation()
        {
            var fs = new FailingFileSystem(FailurePoint.None, "C:\\publish\\players\\existing");
            Assert.Throws<IOException>(() => new PlayerBuildPublisher(fs).Publish("C:/publish", "existing", "C:/work", "C:/content", "{}"));
            Assert.That(fs.DeletedPaths, Is.Empty);
        }

        private BuildContentResult Result(string identity)
        {
            var content = Path.Combine(_root, "content"); var metadata = Path.Combine(_root, "metadata"); Directory.CreateDirectory(content); Directory.CreateDirectory(metadata);
            var manifest = Path.Combine(content, "manifest"); File.WriteAllText(manifest, "x");
            // Player build は transport へ publish する result を受けるので Full。
            return new BuildContentResult(identity, "preflight", "outcome", content, manifest, metadata, "ok", Array.Empty<BuildContentIssue>(),
                null, BuildContentMode.Full);
        }

        private static string Report(string identity, BuildContentResult result, string target = "StandaloneWindows64-Player") => JsonUtility.ToJson(new PlayerContentReportData
        { identity = identity, target = target, contentPath = result.ContentPath!, manifestPointer = result.ManifestPointer!, metadataPath = result.MetadataPath! });

        public enum FailurePoint { None, Copy, Write, Move }

        private sealed class FailingFileSystem : IPlayerBuildFileSystem
        {
            private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
            private readonly FailurePoint _failurePoint;
            internal FailingFileSystem(FailurePoint failurePoint, params string[] existing)
            { _failurePoint = failurePoint; foreach (var path in existing) _directories.Add(path); }
            internal string? Deleted { get; private set; }
            internal List<string> DeletedPaths { get; } = new();
            public bool DirectoryExists(string path) => _directories.Contains(path);
            public void CreateDirectory(string path) => _directories.Add(path);
            public string[] GetFiles(string path) => path == "C:/work" ? new[] { "C:/work/Game.exe" } : Array.Empty<string>();
            public string[] GetDirectories(string path) => Array.Empty<string>();
            public void CopyFile(string source, string target) { if (_failurePoint == FailurePoint.Copy) throw new IOException("copy failed"); }
            public void CopyDirectory(string source, string target) { if (_failurePoint == FailurePoint.Copy) throw new IOException("copy failed"); }
            public void MoveDirectory(string source, string target) { if (_failurePoint == FailurePoint.Move) throw new IOException("move failed"); }
            public void DeleteDirectory(string path) { Deleted = path; DeletedPaths.Add(path); _directories.Remove(path); }
            public void WriteAllText(string path, string value) { if (_failurePoint == FailurePoint.Write) throw new IOException("write failed"); }
        }
    }
}
