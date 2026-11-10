#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OneStarMaker.Editor.PlaySnapshot;

namespace OneStarMaker.Tests.Editor.PlaySnapshot
{
    /// <summary>置き場の folder の生死の判定（純粋な関数）と、一時 folder を使った回収。</summary>
    public sealed class SnapshotRootTests
    {
        private const int CurrentPid = 100;
        private const int OtherPid = 200;
        private const long OtherStart = 638000000000000000;
        private string _root = "";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "OsmPlaySnapshotTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
            if (File.Exists(_root + ".meta")) File.Delete(_root + ".meta");
        }

        private static readonly IReadOnlyDictionary<int, long> OtherAlive = new Dictionary<int, long> { [OtherPid] = OtherStart };

        // ═══ 判定 ═══

        [Test]
        public void LiveOtherProcessFolder_IsKept()
        {
            var name = SnapshotRoot.NewFolderName(OtherPid, OtherStart, "abcd1234");
            Assert.That(SnapshotRoot.Classify(name, OtherAlive, CurrentPid, null), Is.EqualTo(SnapshotRoot.Verdict.Keep));
        }

        [Test]
        public void DeadPidFolder_IsCollected()
        {
            var name = SnapshotRoot.NewFolderName(300, OtherStart, "abcd1234");
            Assert.That(SnapshotRoot.Classify(name, OtherAlive, CurrentPid, null), Is.EqualTo(SnapshotRoot.Verdict.Collect));
        }

        [Test]
        public void ReusedPidWithAnotherStartTime_IsCollected()
        {
            var name = SnapshotRoot.NewFolderName(OtherPid, OtherStart - 1, "abcd1234");
            Assert.That(SnapshotRoot.Classify(name, OtherAlive, CurrentPid, null), Is.EqualTo(SnapshotRoot.Verdict.Collect));
        }

        [Test]
        public void CurrentProcessFolder_IsCollectedUnlessActive()
        {
            var active = SnapshotRoot.NewFolderName(CurrentPid, 1, "aaaa");
            var old = SnapshotRoot.NewFolderName(CurrentPid, 1, "bbbb");
            Assert.That(SnapshotRoot.Classify(active, OtherAlive, CurrentPid, active), Is.EqualTo(SnapshotRoot.Verdict.Keep));
            Assert.That(SnapshotRoot.Classify(old, OtherAlive, CurrentPid, active), Is.EqualTo(SnapshotRoot.Verdict.Collect));
        }

        [TestCase("not-a-snapshot")]
        [TestCase("100_200")]
        [TestCase("x_200_ab")]
        [TestCase("100_200_zz")]
        [TestCase("0_200_ab")]
        public void MalformedName_IsMalformed(string name)
        {
            Assert.That(SnapshotRoot.Classify(name, OtherAlive, CurrentPid, null), Is.EqualTo(SnapshotRoot.Verdict.Malformed));
        }

        // ═══ 回収（一時 folder）═══

        [Test]
        public void Collect_RemovesDeadAndPartialFoldersWithMeta_KeepsLiveAndMalformed()
        {
            var dead = MakeFolder(SnapshotRoot.NewFolderName(300, OtherStart, "dead"), withScene: true);
            var empty = MakeFolder(SnapshotRoot.NewFolderName(301, OtherStart, "e0"), withScene: false);
            var live = MakeFolder(SnapshotRoot.NewFolderName(OtherPid, OtherStart, "11aa"), withScene: true);
            var malformed = MakeFolder("someone-elses-folder", withScene: true);
            var orphanMeta = Path.Combine(_root, SnapshotRoot.NewFolderName(302, OtherStart, "0a") + ".meta");
            File.WriteAllText(orphanMeta, "meta");

            var result = SnapshotRoot.Collect(_root, pid => pid == OtherPid ? OtherStart : null, CurrentPid, null);

            Assert.That(Directory.Exists(dead), Is.False);
            Assert.That(File.Exists(dead + ".meta"), Is.False);
            Assert.That(Directory.Exists(empty), Is.False);
            Assert.That(File.Exists(orphanMeta), Is.False);
            Assert.That(Directory.Exists(live), Is.True);
            Assert.That(Directory.Exists(malformed), Is.True, "名前の形が違う folder は触らない");
            Assert.That(result.Malformed, Does.Contain(malformed));
            Assert.That(result.Remaining, Is.Empty);
        }

        [Test]
        public void Collect_RemovesTheRootWhenNothingRemains()
        {
            MakeFolder(SnapshotRoot.NewFolderName(CurrentPid, 1, "ab"), withScene: true);
            File.WriteAllText(_root + ".meta", "meta");
            SnapshotRoot.Collect(_root, _ => null, CurrentPid, null);
            Assert.That(Directory.Exists(_root), Is.False);
            Assert.That(File.Exists(_root + ".meta"), Is.False);
        }

        [Test]
        public void Collect_DeletionFailure_IsReportedAndRetriedNextTime()
        {
            var dead = MakeFolder(SnapshotRoot.NewFolderName(300, OtherStart, "dead"), withScene: true);
            SnapshotRoot.CollectResult result;
            // 開いたままの file は Windows では消せない。消せなかった物は残りとして返り、次の回収で消える。
            using (File.Open(Path.Combine(dead, "Title.unity"), FileMode.Open, FileAccess.Read, FileShare.None))
                result = SnapshotRoot.Collect(_root, _ => null, CurrentPid, null);
            Assert.That(result.Remaining, Is.Not.Empty);

            var retry = SnapshotRoot.Collect(_root, _ => null, CurrentPid, null);
            Assert.That(retry.Remaining, Is.Empty);
            Assert.That(Directory.Exists(dead), Is.False);
        }

        private string MakeFolder(string name, bool withScene)
        {
            var path = Path.Combine(_root, name);
            Directory.CreateDirectory(path);
            File.WriteAllText(path + ".meta", "meta");
            if (withScene) File.WriteAllText(Path.Combine(path, "Title.unity"), "scene");
            return path;
        }
    }
}
