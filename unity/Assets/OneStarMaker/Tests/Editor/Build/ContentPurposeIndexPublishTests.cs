#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// publish が manifest に書く目的 build の索引。coordinator が projection の root から記録した entry を、生の表現のまま
    /// 決まった順で写す。記録の無い outcome は版 0（索引なし）で publish し、形の壊れた記録は publish を拒む。
    /// 偽 adapter と一時 root だけで確かめ、Unity の Content Directory build は起動しない。
    /// </summary>
    public sealed class ContentPurposeIndexPublishTests
    {
        private readonly List<string> _directories = new List<string>();

        [TearDown]
        public void TearDown()
        {
            foreach (var directory in _directories.Where(Directory.Exists)) Directory.Delete(directory, true);
            _directories.Clear();
        }

        [Test]
        public void Coordinator_RecordsRootEntries_AndPublishWritesThemSortedWithRawRepresentations()
        {
            var root = NewArtifactsRoot();
            // 並べ順を publish が決めることを見るため、plan の順は崩しておく。
            var result = new BuildContentCoordinator(new ContentOverlaySelectionTests.ManifestAdapter()).Build(Request(root,
                Root("shared-prefab", "SharedPrefab", "Assets/P.prefab", null),
                Root("shared-whitebox", "Shared", "Assets/SW.unity", "Whitebox"),
                Root("entry", "Entry", "Assets/E.unity", "Full"),
                Root("shared-object", "Shared", "Assets/SO.prefab", null),
                Root("shared-full", "Shared", "Assets/SF.unity", "Full")));
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            var outcome = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(result.OutcomePath));
            Assert.That(outcome.indexRecord, Is.EqualTo(1));
            Assert.That(outcome.indexEntries.Select(Text), Is.EquivalentTo(new[]
                { "SharedPrefab/Object/", "Shared/Scene/Whitebox", "Entry/Scene/Full", "Shared/Object/", "Shared/Scene/Full" }));
            // preflight は索引を記録しない（記録の印は outcome だけ）。
            Assert.That(JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(result.PreflightPath)).indexRecord, Is.Zero);

            var published = ContentTransportPublisher.Publish(result, Path.Combine(NewOutsideDirectory(), "published"));
            var manifest = ReadManifest(published);
            Assert.That(manifest.index.version, Is.EqualTo(1));
            Assert.That(manifest.index.entries.Select(Text), Is.EqualTo(new[]
                { "Entry/Scene/Full", "Shared/Object/", "Shared/Scene/Full", "Shared/Scene/Whitebox", "SharedPrefab/Object/" }));
            Assert.That(manifest.version, Is.EqualTo(1));
            Assert.That(manifest.rootSchemaVersion, Is.EqualTo(2));
            var index = ReadIndex(published);
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.Indexed));
            Assert.That(index.Entries.Select(Text), Is.EqualTo(manifest.index.entries.Select(Text)));
        }

        [Test]
        public void Publish_RecordedEmptyList_IsAVersionOneIndex_NotNoIndex()
        {
            var temp = NewOutsideDirectory();
            var result = SyntheticResult(temp, Array.Empty<ContentTransportIndexEntry>(), legacyOutcome: false);
            var published = ContentTransportPublisher.Publish(result, Path.Combine(temp, "published"));
            Assert.That(ReadManifest(published).index.version, Is.EqualTo(1));
            var index = ReadIndex(published);
            Assert.That(index.State, Is.EqualTo(ContentPurposeIndexState.Indexed));
            Assert.That(index.Entries, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task Publish_WithoutRecord_WritesVersionZero_ThatInstallsAndReadsAsNoIndex(bool legacyOutcome)
        {
            var temp = NewOutsideDirectory();
            var result = SyntheticResult(temp, null, legacyOutcome);
            var published = ContentTransportPublisher.Publish(result, Path.Combine(temp, "published"));
            var bytes = File.ReadAllBytes(Path.Combine(published, "transport.json"));
            // JsonUtility は入れ子の欄を省けないので、索引なしは版 0 の空の索引として書かれる。
            var manifest = ReadManifest(published);
            Assert.That(manifest.index.version, Is.Zero);
            Assert.That(manifest.index.entries, Is.Empty);
            Assert.That(ReadIndex(published).State, Is.EqualTo(ContentPurposeIndexState.NoIndex));

            var digest = ContentDeliveryFiles.Sha256(bytes);
            using var source = new LocalContentArtifactSource(published);
            var installed = await new ContentInstaller(new ContentCacheStore(Path.Combine(temp, "cache"))).InstallAsync(
                new ContentInstallRequest(digest, "synthetic", result.Identity, BuildContentCoordinator.TargetName,
                    Application.unityVersion, 2, long.MaxValue), source, CancellationToken.None);
            Assert.That(installed.ManifestSha256, Is.EqualTo(digest));
        }

        [Test]
        public void Publish_DuplicateEffectiveEntries_IsRejectedWithoutDestination()
        {
            AssertRejected(Entry("A", "Scene", ""), Entry("A", "Scene", "Full"));
        }

        [Test]
        public void Publish_BlankRepresentation_IsRejectedWithoutDestination()
        {
            AssertRejected(Entry("A", "Scene", " "));
        }

        private void AssertRejected(params ContentTransportIndexEntry[] entries)
        {
            var temp = NewOutsideDirectory();
            var result = SyntheticResult(temp, entries, legacyOutcome: false);
            var transport = Path.Combine(temp, "transport");
            var final = Path.Combine(transport, result.Identity);
            Assert.Throws<InvalidOperationException>(() => ContentTransportPublisher.Publish(result, final));
            Assert.That(Directory.Exists(final), Is.False);
            Assert.That(Directory.GetFileSystemEntries(Path.Combine(transport, ".staging")), Is.Empty);
        }

        private static string Text(ContentTransportIndexEntry x) => x.logicalKey + "/" + x.kind + "/" + x.representation;

        private static ContentTransportIndexEntry Entry(string key, string kind, string representation)
            => new ContentTransportIndexEntry { logicalKey = key, kind = kind, representation = representation };

        private static ContentTransportManifest ReadManifest(string published)
            => JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(published, "transport.json")));

        private static ContentPurposeIndex ReadIndex(string published)
        {
            var bytes = File.ReadAllBytes(Path.Combine(published, "transport.json"));
            return ContentPurposeIndex.Read(bytes, ContentDeliveryFiles.Sha256(bytes));
        }

        private readonly struct RootSpec
        {
            internal RootSpec(BuildContentCandidate candidate, string path, string? representation)
            { Candidate = candidate; Path = path; Representation = representation; }
            internal BuildContentCandidate Candidate { get; }
            internal string Path { get; }
            internal string? Representation { get; }
        }

        private static RootSpec Root(string stableKey, string logicalKey, string path, string? representation)
            => new RootSpec(new BuildContentCandidate(stableKey, logicalKey, "guid-" + stableKey, new BuildProvenance("fixture", stableKey)),
                path, representation);

        private static BuildContentRequest Request(string root, params RootSpec[] roots)
        {
            var candidates = roots.Select(x => x.Candidate).ToArray();
            var tags = roots.Where(x => x.Representation != null).ToDictionary(x => x.Candidate.StableKey,
                x => (IReadOnlyList<BuildTag>)new[] { new BuildTag("Representation", x.Representation!) });
            var snapshot = new BuildMaterializationSnapshot(candidates, tags, Array.Empty<BuildContentRequirement>(),
                roots.Select(x => new BuildDependencySnapshot(x.Candidate.PhysicalKey, x.Path,
                    new[] { new BuildDependencyEntry(x.Candidate.PhysicalKey, x.Path) })).ToArray());
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), candidates, Array.Empty<BuildContentExclusion>());
            return new BuildContentRequest(plan, snapshot, "purpose-index", root, null, null, BuildContentMode.Full);
        }

        // coordinator を通さずに作った成功 result。indexEntries が null なら記録なし。legacyOutcome は indexRecord の欄が無い旧 outcome。
        private static BuildContentResult SyntheticResult(string temp, IReadOnlyList<ContentTransportIndexEntry>? indexEntries, bool legacyOutcome)
        {
            var identity = "20261116T000000000Z-" + Guid.NewGuid().ToString("N");
            var content = Path.Combine(temp, "content", identity);
            Directory.CreateDirectory(content);
            File.WriteAllText(Path.Combine(content, "unity.cf"), "unity");
            var reports = Path.Combine(temp, "reports", identity);
            Directory.CreateDirectory(reports);
            var preflight = Path.Combine(reports, "preflight.json");
            var outcome = Path.Combine(reports, "outcome.json");
            File.WriteAllText(preflight, JsonUtility.ToJson(new ContentReportFile
            {
                identity = identity, target = BuildContentCoordinator.TargetName, contentSet = "synthetic",
            }));
            var result = new BuildContentResult(identity, preflight, outcome, content, Path.Combine(content, "unity.cf"),
                content, "ok", Array.Empty<BuildContentIssue>(), null, BuildContentMode.Full, indexEntries);
            if (legacyOutcome)
                File.WriteAllText(outcome, "{\"identity\":\"" + identity + "\",\"buildMode\":\"Full\"}");
            else
                BuildContentReport.WriteOutcome(outcome, result);
            return result;
        }

        private string NewArtifactsRoot()
        {
            // coordinator が許すテスト専用 root の下に、テストごとの root を作る。
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture",
                "purpose-index-" + Guid.NewGuid().ToString("N")));
            _directories.Add(root);
            return root;
        }

        private string NewOutsideDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "osm-purpose-index-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            _directories.Add(directory);
            return directory;
        }
    }
}
