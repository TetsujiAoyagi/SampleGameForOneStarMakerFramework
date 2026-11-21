#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// build の前の hash と閉包の記録の publish の単体（受け入れ条件 AC7 の単体の部分）。偽の adapter を使い、閉包の file は Unity が import しない
    /// folder（<c>~</c>）に実際に書く。build の間に閉包の file か .meta が変わった build は上書きでなくても失敗し、publish の sourceFiles は
    /// build の前の hash、記録は hash が全て取れて索引が版 1 の時だけ版 1 で、上限を超える時は版 0 にして警告する。
    /// </summary>
    public sealed class SourceClosurePublishTests
    {
        private const int Dependencies = 20;
        private string _folder = "";
        private string _artifacts = "";
        private readonly List<string> _directories = new();

        [SetUp]
        public void SetUp()
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            _folder = "Assets/OneStarMakerGenerated/S34aUnit~/" + id;
            _artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture", "s34a-pub-" + id));
            _directories.Add(Path.Combine(OverlayPlanWorld.ProjectRoot, _folder));
            _directories.Add(_artifacts);
            Write("R.prefab", "root");
            for (var i = 0; i < Dependencies; i++) Write("D" + i + ".asset", "dep " + i);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var directory in _directories.Where(Directory.Exists)) Directory.Delete(directory, true);
            _directories.Clear();
            var parent = Path.Combine(OverlayPlanWorld.ProjectRoot, "Assets/OneStarMakerGenerated/S34aUnit~");
            if (Directory.Exists(parent) && Directory.GetFileSystemEntries(parent).Length == 0) Directory.Delete(parent);
        }

        private string P(string name) => _folder + "/" + name;
        private string Full(string name) => Path.Combine(OverlayPlanWorld.ProjectRoot, P(name));
        private void Write(string name, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Full(name))!);
            File.WriteAllText(Full(name), content);
            File.WriteAllText(Full(name) + ".meta", "guid: " + OverlayPlanWorld.Guid(P(name)));
        }

        private string Publish(BuildContentResult result)
        {
            var published = Path.Combine(Path.GetTempPath(), "osm-s34a-publish-" + Guid.NewGuid().ToString("N"));
            _directories.Add(published);
            return ContentTransportPublisher.Publish(result, published);
        }

        private static ContentTransportManifest Manifest(string published) =>
            JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(Path.Combine(published, "transport.json")));

        // 1 つの root（R.prefab）と依存 D0..Dn。missing に名前を入れた file は disk に無い path として閉包に入る。
        private BuildContentRequest Request(BuildPurposeDeclaration? declaration = null, params string[] missing)
        {
            var names = new[] { "R.prefab" }.Concat(Enumerable.Range(0, Dependencies).Select(i => "D" + i + ".asset")).Concat(missing).ToArray();
            var rootGuid = OverlayPlanWorld.Guid(P("R.prefab"));
            var candidate = new BuildContentCandidate("sk-R", "R", rootGuid, new BuildProvenance("fixture", "R"));
            var snapshot = new BuildMaterializationSnapshot(new[] { candidate },
                new Dictionary<string, IReadOnlyList<BuildTag>> { ["sk-R"] = new[] { new BuildTag("Representation", "Full") } },
                Array.Empty<BuildContentRequirement>(),
                new[] { new BuildDependencySnapshot(rootGuid, P("R.prefab"), names.Select(x => new BuildDependencyEntry(OverlayPlanWorld.Guid(P(x)), P(x)))) });
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { candidate }, Array.Empty<BuildContentExclusion>());
            return new BuildContentRequest(plan, snapshot, "s34a-pub", _artifacts, declaration, null, BuildContentMode.Full);
        }

        private BuildContentResult Build(IContentDirectoryAdapter? adapter = null, BuildPurposeDeclaration? declaration = null, params string[] missing)
            => new BuildContentCoordinator(adapter ?? new ContentOverlaySelectionTests.ManifestAdapter()).Build(Request(declaration, missing));

        [TestCase(false)]
        [TestCase(true)]
        public void SourceChangedDuringUnityBuild_FailsEvenWithoutOverlay_LeavesNoContent_AndCannotBePublished(bool metaOnly)
        {
            var target = Full("D3.asset") + (metaOnly ? ".meta" : "");
            var result = Build(new EditingAdapter(() => File.AppendAllText(target, " edited")));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Single().Code, Is.EqualTo(BuildContentIssueCode.SourceChangedDuringBuild));
            Assert.That(result.Issues.Single().Key, Is.EqualTo(P("D3.asset") + (metaOnly ? ".meta" : "")));
            Assert.That(Directory.Exists(Path.Combine(_artifacts, "content", result.Identity)), Is.False);
            Assert.Throws<InvalidOperationException>(() => Publish(result));
        }

        [Test]
        public void Publish_WritesRecordAndPreBuildHashes_EvenWhenSourceChangesAfterTheBuild()
        {
            var result = Build();
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            var before = OverlayPlanWorld.Sha("dep 3");
            File.WriteAllText(Full("D3.asset"), "edited after the build");
            var manifest = Manifest(Publish(result));
            Assert.That(manifest.sourceFiles.Single(x => x.path == P("D3.asset")).sha256, Is.EqualTo(before), "sourceFiles は build の前の値");
            Assert.That(manifest.sourceFiles.Length, Is.EqualTo((Dependencies + 1) * 2), "file と .meta");
            var record = ContentSourceClosureRecord.Read(manifest);
            Assert.That(record.State, Is.EqualTo(ContentSourceClosureState.Present), record.Detail);
            var root = record.Roots.Single();
            Assert.That(root.LogicalKey + "/" + root.Kind + "/" + root.Representation, Is.EqualTo("R/Object/Full"));
            Assert.That(root.Assets.Count, Is.EqualTo(Dependencies + 1));
            Assert.That(manifest.index.entries.Single().logicalKey, Is.EqualTo("R"));
        }

        [Test]
        public void ClosureFileAbsentAtHashTime_NonOverlayBuildSucceeds_AndPublishesWithoutRecord()
        {
            var result = Build(null, null, "Gone.asset");
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            var manifest = Manifest(Publish(result));
            Assert.That(manifest.sourceClosure.version, Is.Zero);
            Assert.That(manifest.sourceFiles.Any(x => x.path.Contains("Gone")), Is.False, "在る file だけを書く");
            Assert.That(manifest.index.version, Is.EqualTo(1));
        }

        [Test]
        public void IndexVersionZero_PublishesWithoutRecord()
        {
            var result = Build();
            var outcome = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(result.OutcomePath));
            outcome.indexRecord = 0;
            File.WriteAllText(result.OutcomePath, JsonUtility.ToJson(outcome));
            var manifest = Manifest(Publish(result));
            Assert.That(manifest.index.version, Is.Zero);
            Assert.That(manifest.sourceClosure.version, Is.Zero);
            Assert.That(manifest.sourceFiles.Length, Is.EqualTo((Dependencies + 1) * 2));
        }

        [Test]
        public void PreflightWithoutHashRecord_PublishesAsBefore_WithoutRecord()
        {
            var result = Build();
            var preflight = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(result.PreflightPath));
            preflight.sourceHashRecord = 0;
            preflight.sourceHashes = Array.Empty<ContentReportSourceHash>();
            File.WriteAllText(result.PreflightPath, JsonUtility.ToJson(preflight));
            File.WriteAllText(Full("D3.asset"), "edited after the build");
            var manifest = Manifest(Publish(result));
            Assert.That(manifest.sourceClosure.version, Is.Zero);
            Assert.That(manifest.sourceFiles.Single(x => x.path == P("D3.asset")).sha256, Is.EqualTo(OverlayPlanWorld.Sha("edited after the build")),
                "記録の無い旧 preflight は今までどおり publish の時点で hash する");
        }

        private static BuildPurposeDeclaration Padded(int padding) => new("s34a-pub", new[] { "R" }, new BuildRequest(Array.Empty<BuildTag>()),
            new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First),
            new Dictionary<string, string> { ["padding"] = padding == 0 ? "x" : new string('x', padding) });

        [Test]
        public void RecordThatExceedsTheManifestLimit_IsDroppedToVersionZero_WithOneWarning()
        {
            // 記録を除いた manifest と記録を含む manifest の大きさを測り、その間に上限（4 MiB）が来るよう宣言の値で埋める。
            var probe = Manifest(Publish(Build(declaration: Padded(0))));
            var with = new System.Text.UTF8Encoding(false).GetByteCount(JsonUtility.ToJson(probe, true));
            probe.sourceClosure = new ContentSourceClosure();
            var without = new System.Text.UTF8Encoding(false).GetByteCount(JsonUtility.ToJson(probe, true));
            Assert.That(with - without, Is.GreaterThan(200), "記録の分の大きさ");
            const int limit = 4 * 1024 * 1024;
            var padding = limit - (with + without) / 2 + 1;

            var result = Build(declaration: Padded(padding));
            LogAssert.Expect(LogType.Warning, new Regex("source closure record exceeds the manifest limit"));
            var published = Publish(result);
            Assert.That(new FileInfo(Path.Combine(published, "transport.json")).Length, Is.LessThanOrEqualTo(limit));
            var manifest = Manifest(published);
            Assert.That(manifest.sourceClosure.version, Is.Zero);
            Assert.That(manifest.index.version, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ManifestOverTheLimitEvenWithoutRecord_FailsToPublish()
        {
            var result = Build(declaration: Padded(4 * 1024 * 1024 + 1));
            LogAssert.Expect(LogType.Warning, new Regex("source closure record exceeds the manifest limit"));
            Assert.Throws<InvalidOperationException>(() => Publish(result));
        }

        // Unity の build の間に source を書き換える偽の adapter。
        private sealed class EditingAdapter : IContentDirectoryAdapter
        {
            private readonly Action _during;
            private readonly ContentOverlaySelectionTests.ManifestAdapter _inner = new();
            public EditingAdapter(Action during) => _during = during;
            public void ValidateTarget() { }
            public ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace,
                BuildPurposeDeclaration? declaration, ContentOverlayBaseIdentity? overlayBase, string contentSet)
            {
                _during();
                return _inner.Build(projection, identity, workspace, declaration, overlayBase, contentSet);
            }
        }
    }
}
