#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// build mode と publish の受け付け、publish の staging の path。
    /// 偽 adapter と一時 root だけで確かめ、Unity の Content Directory build は起動しない。
    /// </summary>
    public sealed class BuildContentFullModeTests
    {
        private const string Sentinel = "bs5b-sentinel.txt";
        private readonly List<string> _junctions = new List<string>();
        private readonly List<string> _directories = new List<string>();

        [TearDown]
        public void TearDown()
        {
            // junction を先に外す。中を辿る再帰削除をリンクに向けない。
            foreach (var junction in _junctions.Where(Directory.Exists)) Directory.Delete(junction, false);
            foreach (var directory in _directories.Where(Directory.Exists)) Directory.Delete(directory, true);
            _junctions.Clear();
            _directories.Clear();
        }

        [Test]
        public void Full_ClearsWorkspaceBeforeAdapter_AndKeepsWorkspace()
        {
            var root = NewArtifactsRoot();
            var workspace = Workspace(root, "full");
            Directory.CreateDirectory(workspace);
            File.WriteAllText(Path.Combine(workspace, Sentinel), "stale");
            var adapter = new WritingAdapter();
            var result = new BuildContentCoordinator(adapter).Build(Request(root, "full", BuildContentMode.Full));
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(adapter.Calls, Is.EqualTo(1));
            Assert.That(adapter.SawSentinel, Is.False, "adapter が呼ばれた時点で workspace に前回の file が残っていた");
            Assert.That(File.Exists(Path.Combine(result.ContentPath!, Sentinel)), Is.False);
            Assert.That(File.Exists(Path.Combine(result.ContentPath!, WritingAdapter.ManifestName)), Is.True);
            Assert.That(Directory.Exists(workspace), Is.True);
            Assert.That(result.Mode, Is.EqualTo(BuildContentMode.Full));
            Assert.That(ReadOutcome(result).buildMode, Is.EqualTo("Full"));
        }

        [Test]
        public void Incremental_KeepsPreviousWorkspaceFiles()
        {
            // 現状の挙動の固定: Incremental は workspace を消さず、前回の file が成果物に入る（だから publish できない）。
            var root = NewArtifactsRoot();
            var workspace = Workspace(root, "incremental");
            Directory.CreateDirectory(workspace);
            File.WriteAllText(Path.Combine(workspace, Sentinel), "stale");
            var result = new BuildContentCoordinator(new WritingAdapter()).Build(Request(root, "incremental", BuildContentMode.Incremental));
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(File.Exists(Path.Combine(result.ContentPath!, Sentinel)), Is.True);
            Assert.That(ReadOutcome(result).buildMode, Is.EqualTo("Incremental"));
        }

        [Test]
        public void DefaultRequest_IsIncremental()
        {
            var request = new BuildContentRequest(Plan(), Snapshot(), "set", "root");
            Assert.That(request.Mode, Is.EqualTo(BuildContentMode.Incremental));
        }

        [Test]
        public void Full_JunctionInsideWorkspace_FailsWithoutDeletingAnything()
        {
            var root = NewArtifactsRoot();
            var workspace = Workspace(root, "inner-link");
            Directory.CreateDirectory(workspace);
            File.WriteAllText(Path.Combine(workspace, Sentinel), "own");
            var outside = NewOutsideDirectory();
            Junction(Path.Combine(workspace, "link"), outside);
            var adapter = new WritingAdapter();
            var result = new BuildContentCoordinator(adapter).Build(Request(root, "inner-link", BuildContentMode.Full));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Select(x => x.Code), Is.EqualTo(new[] { BuildContentIssueCode.BuildFailure }));
            Assert.That(adapter.Calls, Is.Zero, "削除に失敗したのに Unity の build を始めた");
            Assert.That(File.Exists(Path.Combine(outside, Sentinel)), Is.True);
            Assert.That(File.Exists(Path.Combine(workspace, Sentinel)), Is.True);
            Assert.That(ReadOutcome(result).buildMode, Is.EqualTo("Full"));
        }

        [Test]
        public void Full_WorkspaceItselfIsJunction_FailsWithoutDeletingAnything()
        {
            var root = NewArtifactsRoot();
            var workspace = Workspace(root, "self-link");
            Directory.CreateDirectory(Path.GetDirectoryName(workspace)!);
            var outside = NewOutsideDirectory();
            Junction(workspace, outside);
            var adapter = new WritingAdapter();
            var result = new BuildContentCoordinator(adapter).Build(Request(root, "self-link", BuildContentMode.Full));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(adapter.Calls, Is.Zero);
            Assert.That(File.Exists(Path.Combine(outside, Sentinel)), Is.True);
        }

        [Test]
        public void Full_RootOutsideApprovedRoots_IsRejectedWithoutDeleting()
        {
            var outside = NewOutsideDirectory();
            var workspace = Path.Combine(outside, "work", BuildContentCoordinator.TargetName, "outside");
            Directory.CreateDirectory(workspace);
            File.WriteAllText(Path.Combine(workspace, Sentinel), "outside");
            var adapter = new WritingAdapter();
            Assert.Throws<ArgumentException>(() =>
                new BuildContentCoordinator(adapter).Build(Request(outside, "outside", BuildContentMode.Full)));
            Assert.That(adapter.Calls, Is.Zero);
            Assert.That(File.Exists(Path.Combine(workspace, Sentinel)), Is.True);
        }

        [TestCase(BuildContentMode.Incremental)]
        [TestCase(null)]
        public void Publish_RejectsNonFullOutcome_WithoutCreatingDestinationOrStaging(BuildContentMode? mode)
        {
            var temp = NewOutsideDirectory();
            var result = SyntheticResult(temp, Identity(), mode);
            var transport = Path.Combine(temp, "transport");
            var final = Path.Combine(transport, result.Identity);
            Assert.Throws<InvalidOperationException>(() => ContentTransportPublisher.Publish(result, final));
            Assert.That(Directory.Exists(final), Is.False);
            Assert.That(Directory.Exists(Path.Combine(transport, ".staging")), Is.False);
        }

        [Test]
        public void Publish_FullOutcome_PublishesThroughSiblingStagingDirectory()
        {
            var temp = NewOutsideDirectory();
            var result = SyntheticResult(temp, Identity(), BuildContentMode.Full);
            var transport = Path.Combine(temp, "transport");
            var published = ContentTransportPublisher.Publish(result, Path.Combine(transport, result.Identity));
            Assert.That(File.Exists(Path.Combine(published, "transport.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(published, "content", UnityFileName)), Is.True);
            // staging は move で final になり、共有の `.staging` には何も残らない。
            Assert.That(Directory.GetFileSystemEntries(Path.Combine(transport, ".staging")), Is.Empty);
        }

        [Test]
        public void StagingPath_IsNotLongerThanFinal_AndFitsTheBudgetAtRoot109()
        {
            // 観測（2026-10-27）: 旧 staging の中の Unity の file は repo 相対 163 文字
            // = `artifacts/bs4/transport/`(24) + identity(52) + `.<guid32>.staging`(41) + `/content/`(9) + Unity の file(37)。
            // その最長の file を持つ合成の publish で、staging と final の全 path を repo 相対に読み替えて数える。
            const int checkoutRoot = 109; // = 259 - 150（tools/path-budget.json の上限）
            const string transportPrefix = "artifacts/bs4/transport";
            var temp = NewOutsideDirectory();
            var identity = Identity();
            Assert.That(identity.Length, Is.EqualTo(52));
            var result = SyntheticResult(temp, identity, BuildContentMode.Full);
            var transport = Path.Combine(temp, "transport");
            var final = ContentTransportPublisher.Publish(result, Path.Combine(transport, identity));
            var staging = ContentTransportPublisher.StagingPathFor(final);
            Assert.That(Path.GetDirectoryName(staging), Is.EqualTo(Path.Combine(transport, ".staging")));
            Assert.That(Path.GetFileName(staging), Has.Length.EqualTo(32));

            var files = Directory.GetFiles(final, "*", SearchOption.AllDirectories);
            Assert.That(files.Select(x => Path.GetRelativePath(final, x).Replace('\\', '/')),
                Does.Contain("content/" + UnityFileName));
            foreach (var file in files)
            {
                var relative = Path.GetRelativePath(final, file);
                var finalLength = RepoRelativeLength(transportPrefix, transport, file);
                var stagingLength = RepoRelativeLength(transportPrefix, transport, Path.Combine(staging, relative));
                Assert.That(stagingLength, Is.LessThanOrEqualTo(finalLength), relative);
                Assert.That(checkoutRoot + 1 + finalLength, Is.LessThanOrEqualTo(259), relative);
                Assert.That(checkoutRoot + 1 + stagingLength, Is.LessThanOrEqualTo(259), relative);
            }
        }

        // ───── 上書きの build（HANDOFF CONTENT_OVERLAY_BUILD の AC4） ─────

        [Test]
        public void OverlayBuild_PassesTheDeclarationToTheAdapter()
        {
            var declaration = Declaration("purpose_a");
            var adapter = new ContentOverlaySelectionTests.ManifestAdapter();
            var result = new BuildContentCoordinator(adapter).Build(OverlayRequest(NewArtifactsRoot(), "overlay_a", "purpose_a", declaration));
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(adapter.Declaration, Is.SameAs(declaration));
            Assert.That(result.Declaration, Is.SameAs(declaration));
            Assert.That(result.Mode, Is.EqualTo(BuildContentMode.Full));
        }

        [Test]
        public void Publish_RejectsOverlayBuild_WithoutCreatingDestinationOrStaging()
        {
            var result = new BuildContentCoordinator(new ContentOverlaySelectionTests.ManifestAdapter())
                .Build(OverlayRequest(NewArtifactsRoot(), "overlay_a", null, null));
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(ReadOutcome(result).overlay, Is.True);
            var transport = Path.Combine(NewOutsideDirectory(), "transport");
            var final = Path.Combine(transport, result.Identity);
            Assert.Throws<InvalidOperationException>(() => ContentTransportPublisher.Publish(result, final));
            Assert.That(Directory.Exists(final), Is.False);
            Assert.That(Directory.Exists(Path.Combine(transport, ".staging")), Is.False);
        }

        [Test]
        public void Publish_RejectsSubsetBuildWithoutOverlayMark()
        {
            // public な絞りで作った plan を、上書きの記録なしの既存のコンストラクタで Full に build した場合（印は無い）。
            var t = ContentOverlaySelectionTests.Topology.Create();
            var request = new BuildContentRequest(t.Plan.WithSelectedSubset(new[] { "SceneDirect" }), t.Snapshot, "subset", NewArtifactsRoot(),
                null, null, BuildContentMode.Full);
            var result = new BuildContentCoordinator(new ContentOverlaySelectionTests.ManifestAdapter()).Build(request);
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(ReadOutcome(result).overlay, Is.False);
            var transport = Path.Combine(NewOutsideDirectory(), "transport");
            var final = Path.Combine(transport, result.Identity);
            Assert.Throws<InvalidOperationException>(() => ContentTransportPublisher.Publish(result, final));
            Assert.That(Directory.Exists(final), Is.False);
            Assert.That(Directory.Exists(Path.Combine(transport, ".staging")), Is.False);
        }

        [TestCase("base_a")]
        [TestCase("BASE_A")]
        [TestCase("Base_a")]
        public void OverlayBuild_SameContentSetAsBase_IsRejectedBeforeReports(string contentSet)
        {
            var root = NewArtifactsRoot();
            var adapter = new ContentOverlaySelectionTests.ManifestAdapter();
            Assert.Throws<ArgumentException>(() => new BuildContentCoordinator(adapter).Build(OverlayRequest(root, contentSet, null, null)));
            Assert.That(adapter.Calls, Is.Zero);
            Assert.That(Directory.Exists(Path.Combine(root, "reports")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(root, "work")), Is.False);
        }

        [TestCase("", "rev", "target", "digest")]
        [TestCase("set", " ", "target", "digest")]
        [TestCase("set", "rev", "", "digest")]
        [TestCase("set", "rev", "target", null)]
        public void BaseIdentity_RejectsEmptyFields(string contentSet, string revision, string target, string? digest)
        {
            Assert.Throws<ArgumentException>(() => new ContentOverlayBaseIdentity(contentSet, revision, target, digest!, null));
        }

        [Test]
        public void BaseIdentity_RejectsAnEmptyDeclarationName()
        {
            Assert.Throws<ArgumentException>(() => new ContentOverlayBaseIdentity("set", "rev", "target", "digest", ""));
        }

        [TestCase("purpose_a", null)]
        [TestCase(null, "purpose_a")]
        [TestCase("purpose_a", "purpose_b")]
        public void OverlayBuild_DeclarationNameMismatch_IsRejectedBeforeReports(string? baseName, string? overlayName)
        {
            var root = NewArtifactsRoot();
            var adapter = new ContentOverlaySelectionTests.ManifestAdapter();
            var declaration = overlayName == null ? null : Declaration(overlayName);
            Assert.Throws<ArgumentException>(() => new BuildContentCoordinator(adapter).Build(OverlayRequest(root, "overlay_a", baseName, declaration)));
            Assert.That(adapter.Calls, Is.Zero);
            Assert.That(Directory.Exists(Path.Combine(root, "reports")), Is.False);
        }

        [Test]
        public void OverlayBuild_BaseTargetMismatch_IsRejectedBeforeReports()
        {
            var root = NewArtifactsRoot();
            var t = ContentOverlaySelectionTests.Topology.Create();
            var selection = ContentOverlaySelection.Select(t.Plan, t.Snapshot, new[] { ContentOverlaySelectionTests.EditedTex });
            var request = new BuildContentRequest(selection,
                new ContentOverlayBaseIdentity("base_a", "rev-1", "Android-Player", "digest-1", null), "overlay_a", root, null, null);
            var adapter = new ContentOverlaySelectionTests.ManifestAdapter();
            Assert.Throws<ArgumentException>(() => new BuildContentCoordinator(adapter).Build(request));
            Assert.That(adapter.Calls, Is.Zero);
            Assert.That(Directory.Exists(Path.Combine(root, "reports")), Is.False);
        }

        private static BuildContentRequest OverlayRequest(string root, string contentSet, string? baseDeclarationName, BuildPurposeDeclaration? declaration)
        {
            var t = ContentOverlaySelectionTests.Topology.Create();
            var selection = ContentOverlaySelection.Select(t.Plan, t.Snapshot, new[] { ContentOverlaySelectionTests.EditedTex });
            return new BuildContentRequest(selection, ContentOverlaySelectionTests.BaseIdentity(baseDeclarationName), contentSet, root, declaration, null);
        }

        private static BuildPurposeDeclaration Declaration(string name) =>
            new BuildPurposeDeclaration(name, new[] { "Root" }, new BuildRequest(Array.Empty<BuildTag>()),
                new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));

        // 観測の Unity の file 名の長さ（37 文字）に合わせた合成の名前。
        private static readonly string UnityFileName = new string('u', 33) + ".cf";

        private static int RepoRelativeLength(string transportPrefix, string transport, string path) =>
            transportPrefix.Length + 1 + Path.GetRelativePath(transport, path).Length;

        private static string Identity() => "20261102T000000000Z-" + Guid.NewGuid().ToString("N");

        private static BuildContentResult SyntheticResult(string temp, string identity, BuildContentMode? mode)
        {
            var content = Path.Combine(temp, "content", identity);
            Directory.CreateDirectory(content);
            File.WriteAllText(Path.Combine(content, UnityFileName), "unity");
            var reports = Path.Combine(temp, "reports", identity);
            Directory.CreateDirectory(reports);
            var preflight = Path.Combine(reports, "preflight.json");
            var outcome = Path.Combine(reports, "outcome.json");
            File.WriteAllText(preflight, JsonUtility.ToJson(new ContentReportFile
            {
                identity = identity, target = BuildContentCoordinator.TargetName, contentSet = "synthetic",
            }));
            var result = new BuildContentResult(identity, preflight, outcome, content, Path.Combine(content, UnityFileName),
                content, "ok", Array.Empty<BuildContentIssue>(), null, mode ?? BuildContentMode.Full);
            if (mode == null)
                // buildMode の欄が無い旧 outcome（result の欄が Full でも file の側を正とする）。
                File.WriteAllText(outcome, "{\"identity\":\"" + identity + "\"}");
            else
                BuildContentReport.WriteOutcome(outcome, result);
            return result;
        }

        private static ContentReportFile ReadOutcome(BuildContentResult result) =>
            JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(result.OutcomePath));

        private string NewArtifactsRoot()
        {
            // coordinator が許すテスト専用 root の下に、テストごとの root を作る。
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture",
                "bs5b1-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            _directories.Add(root);
            return root;
        }

        private string NewOutsideDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "osm-bs5b1-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, Sentinel), "outside");
            _directories.Add(directory);
            return directory;
        }

        private static string Workspace(string root, string contentSet) =>
            Path.Combine(root, "work", BuildContentCoordinator.TargetName, contentSet);

        private void Junction(string link, string target)
        {
            // junction は管理者権限なしで作れる（symbolic link と違い開発者モードも要らない）。
            using var process = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            })!;
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero, process.StandardError.ReadToEnd());
            _junctions.Add(link);
        }

        private static BuildContentRequest Request(string root, string contentSet, BuildContentMode mode) =>
            new BuildContentRequest(Plan(), Snapshot(), contentSet, root, null, null, mode);

        private static readonly BuildContentCandidate Selected =
            new BuildContentCandidate("a", "one", "guid-a", new BuildProvenance("fixture", "a"));

        private static BuildPlan Plan() =>
            new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { Selected }, Array.Empty<BuildContentExclusion>());

        private static BuildMaterializationSnapshot Snapshot() =>
            new BuildMaterializationSnapshot(new[] { Selected }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(),
                new[] { new BuildDependencySnapshot("guid-a", "Assets/A.prefab",
                    new[] { new BuildDependencyEntry("guid-a", "Assets/A.prefab") }) });

        // Unity の代わりに workspace へ manifest を書く。呼ばれた時点の workspace に前回の file があったかを記録する。
        private sealed class WritingAdapter : IContentDirectoryAdapter
        {
            public const string ManifestName = "manifest.txt";
            public int Calls { get; private set; }
            public bool SawSentinel { get; private set; }
            public void ValidateTarget() { }
            public ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace,
                BuildPurposeDeclaration? declaration)
            {
                Calls++;
                SawSentinel = File.Exists(Path.Combine(workspace, Sentinel));
                var manifest = Path.Combine(workspace, ManifestName);
                File.WriteAllText(manifest, identity);
                return new ContentDirectoryBuild("ok", manifest, workspace);
            }
        }
    }
}
