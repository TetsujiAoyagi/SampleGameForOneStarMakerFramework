#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent;
using UnityEditor;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 上書きの build が directory の root に base の記録（5 欄）・上書きの contentSet・Unity の版を書くこと（Editor の adapter）。
    /// Unity の Content Directory build は adapter の継ぎ目で差し替え、build の時点で生成された root を AssetDatabase から読み戻して見る。
    /// coordinator が書式の違う digest を report の前に拒むこと、記録の無い root の索引の作成が変わらないことも見る。
    /// </summary>
    public sealed class OverlayRootRecordTests
    {
        private const string ObjectPath = "Assets/TutorialInfo/Icons/URP.png";
        private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private string _workspace = "";

        [SetUp]
        public void SetUp()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                Assert.Ignore("The adapter tests require the active StandaloneWindows64 Player target.");
            _workspace = Path.Combine(Path.GetTempPath(), "osm-s32-root-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_workspace);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_workspace)) Directory.Delete(_workspace, true);
        }

        [TestCase("purpose_a")]
        [TestCase(null)]
        public void OverlayBuild_WritesTheRecordToTheRoot_AndItReadsBack(string? declarationName)
        {
            var baseIdentity = new ContentOverlayBaseIdentity("base_a", "rev-1", BuildContentCoordinator.TargetName, Digest, declarationName);
            var declaration = declarationName == null ? null : Declaration(declarationName);
            BuildContentOverlayBase? readBack = null;
            BuildContentDeclaration? readDeclaration = null;
            var adapter = new UnityContentDirectoryAdapter(p => Capture(p, out readBack, out readDeclaration));

            adapter.Build(Projection(), "overlay-build-1", _workspace, declaration, baseIdentity, "overlay_a");

            Assert.That(readBack, Is.Not.Null, "AssetDatabase で読み戻した root が記録を持つ");
            Assert.That(readBack!.BaseContentSet, Is.EqualTo("base_a"));
            Assert.That(readBack.BaseRevision, Is.EqualTo("rev-1"));
            Assert.That(readBack.BaseTarget, Is.EqualTo(BuildContentCoordinator.TargetName));
            Assert.That(readBack.BaseManifestSha256, Is.EqualTo(Digest));
            Assert.That(readBack.BaseDeclarationName, Is.EqualTo(declarationName));
            Assert.That(readBack.OverlayContentSet, Is.EqualTo("overlay_a"), "上書き自身の contentSet");
            Assert.That(readBack.UnityVersion, Is.EqualTo(Application.unityVersion), "build した Editor の版");
            // Editor の base の識別からの変換が往復で一致する（照合の関数が同じ値に一致と答える）。
            Assert.That(BuildContentOverlayBase.FindMismatch(readBack, baseIdentity.ContentSet, baseIdentity.Revision, baseIdentity.Target,
                baseIdentity.ManifestDigest, baseIdentity.DeclarationName), Is.Null);
            Assert.That(readDeclaration?.Name, Is.EqualTo(declarationName));
        }

        [Test]
        public void NonOverlayBuild_ReadsBackAsNoRecord()
        {
            BuildContentOverlayBase? readBack = null;
            var adapter = new UnityContentDirectoryAdapter(p => Capture(p, out readBack, out _));
            adapter.Build(Projection(), "plain-build-1", _workspace, null, null, "plain");
            Assert.That(readBack, Is.Null, "Unity の直列化は class の欄を null にしないが、必須の欄が空なら記録無しと読む");
        }

        [Test]
        public void Coordinator_PassesTheBaseAndContentSetToTheAdapter_AndRejectsANonCanonicalDigestBeforeReports()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture",
                "ovlrec-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            try
            {
                var t = ContentOverlaySelectionTests.Topology.Create();
                var selection = ContentOverlaySelection.Select(t.Plan, t.Snapshot, new[] { ContentOverlaySelectionTests.EditedTex });
                var adapter = new RecordingAdapter();
                var ok = new BuildContentCoordinator(adapter).Build(new BuildContentRequest(selection,
                    ContentOverlaySelectionTests.BaseIdentity(), "overlay_a", root, null, null));
                Assert.That(ok.IsSuccess, Is.True, ok.Summary);
                Assert.That(adapter.OverlayBase!.ManifestDigest, Is.EqualTo(ContentOverlaySelectionTests.BaseDigest));
                Assert.That(adapter.ContentSet, Is.EqualTo("overlay_a"));

                foreach (var digest in new[] { "digest-1", Digest.ToUpperInvariant(), "sha256:" + Digest, Digest.Substring(1) })
                {
                    var rejected = new RecordingAdapter();
                    var reports = Path.Combine(root, "rejected-" + rejected.GetHashCode());
                    var request = new BuildContentRequest(selection,
                        new ContentOverlayBaseIdentity("base_a", "rev-1", BuildContentCoordinator.TargetName, digest, null), "overlay_a", reports, null, null);
                    Assert.Throws<ArgumentException>(() => new BuildContentCoordinator(rejected).Build(request), digest);
                    Assert.That(rejected.Calls, Is.Zero, digest);
                    Assert.That(Directory.Exists(Path.Combine(reports, "reports")), Is.False, "report の前に拒む: " + digest);
                }
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void RootWithoutRecord_IndexesAsBefore()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(ObjectPath);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId), Is.True);
            var root = ScriptableObject.CreateInstance<BuildContentRoot>();
            try
            {
                root.Initialize("build", "target", new[]
                {
                    new BuildContentEntry("Prop", "prop", "Full",
                        new Unity.Loading.Loadable<UnityEngine.Object>(LoadableObjectIdEditorUtility.CreateLoadableObjectId(asset)), guid, localId),
                });
                Assert.That(root.OverlayBase, Is.Null);
                Assert.That(ContentDirectoryIndex.TryCreate(root, "build", "target", out var index, out _), Is.True);
                Assert.That(index!.TryResolveObject("Prop", "Full", out var entry, out _), Is.True);
                Assert.That(entry!.LogicalKey, Is.EqualTo("Prop"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private UnityContentBuildResult Capture(BuildContentDirectoryParameters parameters,
            out BuildContentOverlayBase? overlayBase, out BuildContentDeclaration? declaration)
        {
            // build の時点（生成 root の削除の前）に、AssetDatabase から root を読み戻す。
            var loaded = AssetDatabase.LoadAssetAtPath<BuildContentRoot>(parameters.rootAssetPaths.Single());
            Assert.That(loaded, Is.Not.Null);
            overlayBase = loaded.OverlayBase;
            declaration = loaded.Declaration;
            File.WriteAllText(Path.Combine(parameters.outputPath, "BuildManifestHash.txt"), "fake");
            return new UnityContentBuildResult(true, "fake");
        }

        private static BuildContentProjectionResult Projection()
        {
            var guid = AssetDatabase.AssetPathToGUID(ObjectPath);
            var candidate = new BuildContentCandidate("Prop", "Prop", guid, new BuildProvenance("overlay-root-test", ObjectPath));
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { candidate }, Array.Empty<BuildContentExclusion>());
            var issues = new List<BuildMaterializationIssue>();
            var closure = new AssetDependencySnapshotBuilder(new UnityAssetDatabaseGateway()).Build(guid, ObjectPath, issues);
            Assert.That(closure, Is.Not.Null, string.Join("; ", issues.Select(x => x.Code)));
            var snapshot = new BuildMaterializationSnapshot(new[] { candidate }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), new[] { closure! });
            var projection = BuildContentProjection.Create(plan, snapshot);
            Assert.That(projection.IsValid, Is.True, string.Join("; ", projection.Issues.Select(x => x.Code)));
            return projection;
        }

        private static BuildPurposeDeclaration Declaration(string name) =>
            new(name, new[] { "Prop" }, new BuildRequest(Array.Empty<BuildTag>()),
                new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));

        private sealed class RecordingAdapter : IContentDirectoryAdapter
        {
            public int Calls { get; private set; }
            public ContentOverlayBaseIdentity? OverlayBase { get; private set; }
            public string? ContentSet { get; private set; }
            public void ValidateTarget() { }
            public ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace,
                BuildPurposeDeclaration? declaration, ContentOverlayBaseIdentity? overlayBase, string contentSet)
            {
                Calls++;
                OverlayBase = overlayBase;
                ContentSet = contentSet;
                var manifest = Path.Combine(workspace, "manifest.txt");
                File.WriteAllText(manifest, identity);
                return new ContentDirectoryBuild("ok", manifest, workspace);
            }
        }
    }
}
