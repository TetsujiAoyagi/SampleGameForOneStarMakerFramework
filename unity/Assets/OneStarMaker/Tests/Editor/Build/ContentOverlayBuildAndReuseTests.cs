#nullable enable

using System;
using System.Collections.Generic;
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
    /// 計画から上書きを得る口（受け入れ条件 AC5）: 作り直せない root の集合を認める口、計画から作る build の request と coordinator の照合、
    /// 前回の上書きの再利用。偽の adapter（Unity を呼ばない）を使い、閉包の file は Unity が import しない folder（<c>~</c>）に実際に書く
    /// （coordinator は build の前後に disk の file を hash するので）。
    /// </summary>
    public sealed class ContentOverlayBuildAndReuseTests
    {
        private const string GeneratedFolder = "Assets/OneStarMakerGenerated";
        private bool _generatedExisted;
        private string _folder = "";
        private string _artifacts = "";
        private OverlayPlanWorld _world = null!;
        private OverlayPlanWorld.RootDef _a = null!, _aw = null!, _b = null!, _c = null!;
        private OverlayPlanWorld.BaseManifest _base = null!;

        private string P(string name) => _folder + "/" + name;

        [SetUp]
        public void SetUp()
        {
            var id = Guid.NewGuid().ToString("N").Substring(0, 8);
            _generatedExisted = Directory.Exists(Path.Combine(OverlayPlanWorld.ProjectRoot, GeneratedFolder));
            _folder = GeneratedFolder + "/S34aUnit~/" + id;
            _artifacts = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture", "s34a-" + id));
            _world = new OverlayPlanWorld { Mirror = true };
            foreach (var name in new[] { "T.png", "U.png", "M.mat", "A.unity", "AW.unity", "B.unity", "C.unity" }) _world.Put(P(name));
            _a = _world.Root("A", "Full", P("A.unity"), P("T.png"));
            _aw = _world.Root("A", "Whitebox", P("AW.unity"), P("T.png"));
            _b = _world.Root("B", "Full", P("B.unity"), P("T.png"));
            _c = _world.Root("C", "Full", P("C.unity"), P("M.mat"), P("U.png"));
            _base = _world.Publish(new[] { _a, _b, _c });
        }

        [TearDown]
        public void TearDown()
        {
            var folder = Path.Combine(OverlayPlanWorld.ProjectRoot, _folder);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            var parent = Path.GetDirectoryName(folder)!;
            if (Directory.Exists(parent) && Directory.GetFileSystemEntries(parent).Length == 0) Directory.Delete(parent);
            // S34aUnit~ の下は import されないので、この test が作った OneStarMakerGenerated は .meta も AssetDatabase の entry も持たない。
            // disk に残すと、後の test と製品の adapter の IsValidFolder が false を返し、CreateFolder が「OneStarMakerGenerated 1」を作って
            // 元の path への書き込みが失敗する。SetUp の前から在った folder（他の test の片付け前のもの）には触れない。
            var generated = Path.Combine(OverlayPlanWorld.ProjectRoot, GeneratedFolder);
            if (!_generatedExisted && Directory.Exists(generated) && Directory.GetFileSystemEntries(generated).Length == 0)
            {
                if (UnityEditor.AssetDatabase.IsValidFolder(GeneratedFolder)) UnityEditor.AssetDatabase.DeleteAsset(GeneratedFolder);
                else Directory.Delete(generated);
            }
            if (Directory.Exists(_artifacts)) Directory.Delete(_artifacts, true);
        }

        private ContentOverlayPlan Plan(IEnumerable<OverlayPlanWorld.RootDef>? selected = null)
            => _world.Plan(_base, selected ?? new[] { _a, _b, _c }, new[] { _aw }, null, _artifacts);

        private int Reports() => Directory.Exists(Path.Combine(_artifacts, "reports")) ? Directory.GetDirectories(Path.Combine(_artifacts, "reports")).Length : 0;

        private static ContentReportFile Read(string path) => JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(path));

        // ─── 認める口 ───

        [Test]
        public void UnbuildableRoots_WithoutAcknowledgement_RejectRequestAndReuse_BeforeAnyReport()
        {
            _world.Remove(P("B.unity"));
            _world.Edit(P("T.png"), "v2");
            var plan = Plan();
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.BuildRequired));
            Assert.That(plan.Unbuildable.Count, Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => _ = new BuildContentRequest(plan));
            Assert.Throws<InvalidOperationException>(() => plan.GetReusableResult());
            Assert.That(Reports(), Is.Zero, "report の directory は増えない");
        }

        [Test]
        public void Acknowledge_MustMatchTheUnbuildableSetExactly()
        {
            _world.Remove(P("B.unity"));
            _world.Edit(P("T.png"), "v2");
            var plan = Plan();
            Assert.Throws<ArgumentException>(() => plan.Acknowledge(Array.Empty<ContentOverlayRootIdentity>()));
            var extra = plan.Unbuildable.Select(x => x.Root).Concat(new[] { new ContentOverlayRootIdentity("A", "Scene", "Full", _a.RootGuid, null) });
            Assert.Throws<ArgumentException>(() => plan.Acknowledge(extra));
            Assert.That(plan.Acknowledge(plan.Unbuildable.Select(x => x.Root)).IsAcknowledged, Is.True);

            // 作り直せない root が増えた計画に、前の計画の集合は流用できない。
            _world.Remove(P("M.mat"));
            _world.Edit(P("U.png"), "v2");
            var grown = Plan();
            Assert.That(grown.Unbuildable.Count, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => grown.Acknowledge(plan.Unbuildable.Select(x => x.Root)));
        }

        [Test]
        public void PlansThatNeedNoBuild_RejectRequestAndReuse()
        {
            var none = Plan();
            Assert.That(none.State, Is.EqualTo(ContentOverlayPlanState.NotRequired));
            Assert.Throws<InvalidOperationException>(() => _ = new BuildContentRequest(none));
            Assert.Throws<InvalidOperationException>(() => none.GetReusableResult());

            _world.Remove(P("M.mat"));
            _world.Edit(P("U.png"), "v2");
            var unbuildableOnly = Plan();
            Assert.That(unbuildableOnly.State, Is.EqualTo(ContentOverlayPlanState.UnbuildableOnly));
            var acknowledged = unbuildableOnly.Acknowledge(unbuildableOnly.Unbuildable.Select(x => x.Root));
            Assert.Throws<InvalidOperationException>(() => _ = new BuildContentRequest(acknowledged));
            Assert.Throws<InvalidOperationException>(() => acknowledged.GetReusableResult());
        }

        [Test]
        public void AcknowledgedBuild_RecordsUnbuildableSuppliedAndKey_InThePreflight()
        {
            _world.Remove(P("B.unity"));
            _world.Edit(P("T.png"), "v2");
            _world.Edit(P("C.unity"), "v2");
            var plan = _world.Plan(_base, new[] { _a, _b, _c }, new[] { _aw }, new[] { "C" }, _artifacts);
            plan = plan.Acknowledge(plan.Unbuildable.Select(x => x.Root));
            var adapter = new ContentOverlaySelectionTests.ManifestAdapter();
            var result = new BuildContentCoordinator(adapter).Build(new BuildContentRequest(plan));
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            Assert.That(result.OverlayKey, Is.EqualTo(plan.Key));
            var preflight = Read(result.PreflightPath);
            Assert.That(preflight.overlay, Is.True);
            var unbuildable = preflight.overlayUnbuildable.Single();
            Assert.That(unbuildable.logicalKey + "|" + unbuildable.kind + "|" + unbuildable.reason, Is.EqualTo("B|Scene|SourceAbsent"));
            Assert.That(unbuildable.edits, Is.EqualTo(new[] { P("T.png") }));
            Assert.That(unbuildable.missingFiles, Is.EqualTo(new[] { P("B.unity") }));
            Assert.That(preflight.overlaySupplied.Single().logicalKey, Is.EqualTo("C"));
            Assert.That(preflight.overlayKey, Is.EqualTo(plan.Key));
            Assert.That(Read(result.OutcomePath).overlayKey, Is.EqualTo(plan.Key));
            Assert.That(preflight.selected.Single(), Does.StartWith("sk-A-Full|"));
        }

        // ─── coordinator の照合 ───

        [Test]
        public void ClosureFileMissingBeforeUnity_FailsWithoutCallingUnity()
        {
            _world.Edit(P("T.png"), "v2");
            var plan = Plan();
            _world.Remove(P("T.png"));
            var adapter = new ContentOverlaySelectionTests.ManifestAdapter();
            var result = new BuildContentCoordinator(adapter).Build(new BuildContentRequest(plan));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Single().Code, Is.EqualTo(BuildContentIssueCode.SourceFileMissing));
            Assert.That(adapter.Calls, Is.Zero, "手元に無い source を Unity の build に渡さない");
        }

        [Test]
        public void SourceChangedBetweenPlanAndBuild_FailsAfterUnity_AndWritesNoReuseRecord()
        {
            _world.Edit(P("T.png"), "v2");
            var plan = Plan();
            _world.Edit(P("T.png"), "v3");
            var result = new BuildContentCoordinator(new ContentOverlaySelectionTests.ManifestAdapter()).Build(new BuildContentRequest(plan));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Single().Code, Is.EqualTo(BuildContentIssueCode.SourceChangedDuringBuild));
            Assert.That(Directory.Exists(Path.Combine(_artifacts, "content", result.Identity)), Is.False);
            Assert.That(File.Exists(ContentOverlayReuse.RecordPath(_artifacts, OverlayPlanWorld.OverlaySet)), Is.False);
        }

        [Test]
        public void SourceChangedDuringUnity_FailsAndWritesNoReuseRecord()
        {
            _world.Edit(P("T.png"), "v2");
            var plan = Plan();
            var adapter = new EditingAdapter(() => _world.EditMeta(P("T.png")));
            var result = new BuildContentCoordinator(adapter).Build(new BuildContentRequest(plan));
            Assert.That(result.Issues.Single().Code, Is.EqualTo(BuildContentIssueCode.SourceChangedDuringBuild));
            Assert.That(File.Exists(ContentOverlayReuse.RecordPath(_artifacts, OverlayPlanWorld.OverlaySet)), Is.False);
        }

        // ─── 再利用 ───

        private BuildContentResult BuildOnce(ContentOverlayPlan plan)
        {
            var result = new BuildContentCoordinator(new ContentOverlaySelectionTests.ManifestAdapter()).Build(new BuildContentRequest(plan));
            Assert.That(result.IsSuccess, Is.True, result.Summary);
            return result;
        }

        [Test]
        public void SameKey_ReturnsThePreviousResult_WithoutBuilding()
        {
            _world.Edit(P("T.png"), "v2");
            var built = BuildOnce(Plan());
            var reports = Reports();
            var again = Plan();
            Assert.That(again.Key, Is.EqualTo(built.OverlayKey));
            var reused = again.GetReusableResult();
            Assert.That(reused, Is.Not.Null);
            Assert.That(reused!.Identity, Is.EqualTo(built.Identity));
            Assert.That(reused.ContentPath, Is.EqualTo(built.ContentPath));
            Assert.That(reused.ManifestPointer, Is.EqualTo(built.ManifestPointer));
            Assert.That(reused.IsSuccess, Is.True);
            Assert.That(Reports(), Is.EqualTo(reports), "再利用は build しない");
            var record = ContentOverlayReuse.RecordPath(_artifacts, OverlayPlanWorld.OverlaySet);
            Assert.That(record, Is.EqualTo(Path.Combine(_artifacts, "overlay", OverlayPlanWorld.OverlaySet + ".json")));
            foreach (var forbidden in new[] { "reports", "work", "staging", "content" })
                Assert.That(record.StartsWith(Path.Combine(_artifacts, forbidden), StringComparison.OrdinalIgnoreCase), Is.False, forbidden);
        }

        [TestCase("EditClosureFile")]
        [TestCase("EditClosureMeta")]
        [TestCase("DeleteContentDirectory")]
        [TestCase("DeleteOneContentFile")]
        [TestCase("RewriteOneContentFile")]
        [TestCase("OutcomeFailed")]
        [TestCase("OutcomeUnreadable")]
        [TestCase("RecordUnreadable")]
        public void AnyChangeToTheKeyOrTheArtifacts_DisablesReuse_AndTheNextBuildReplacesTheRecord(string change)
        {
            _world.Edit(P("T.png"), "v2");
            var built = BuildOnce(Plan());
            var content = built.ContentPath!;
            switch (change)
            {
                case "EditClosureFile": _world.Edit(P("A.unity"), "v2"); break;
                case "EditClosureMeta": _world.EditMeta(P("T.png")); break;
                case "DeleteContentDirectory": Directory.Delete(content, true); break;
                case "DeleteOneContentFile": File.Delete(Directory.GetFiles(content, "*", SearchOption.AllDirectories).First()); break;
                case "RewriteOneContentFile": File.AppendAllText(Directory.GetFiles(content, "*", SearchOption.AllDirectories).First(), "x"); break;
                case "OutcomeFailed":
                    var outcome = Read(built.OutcomePath);
                    outcome.issues = new[] { "BuildFailure|x|y" };
                    File.WriteAllText(built.OutcomePath, JsonUtility.ToJson(outcome));
                    break;
                case "OutcomeUnreadable": File.WriteAllText(built.OutcomePath, "{ not json"); break;
                case "RecordUnreadable": File.WriteAllText(ContentOverlayReuse.RecordPath(_artifacts, OverlayPlanWorld.OverlaySet), "{ not json"); break;
            }
            var plan = Plan();
            Assert.That(plan.GetReusableResult(), Is.Null, change);
            var rebuilt = BuildOnce(plan);
            Assert.That(rebuilt.Identity, Is.Not.EqualTo(built.Identity));
            Assert.That(Plan().GetReusableResult()!.Identity, Is.EqualTo(rebuilt.Identity), "記録は新しい成果物を指す");
        }

        [Test]
        public void ExtraFileInTheArtifacts_DoesNotDisableReuse()
        {
            _world.Edit(P("T.png"), "v2");
            var built = BuildOnce(Plan());
            File.WriteAllText(Path.Combine(built.ContentPath!, "added-by-someone.txt"), "x");
            Assert.That(Plan().GetReusableResult()!.Identity, Is.EqualTo(built.Identity));
        }

        [Test]
        public void ReusedResult_CarriesThePreviousPreflight_WhileThisPlanOwnsTodaysReasons()
        {
            _world.Remove(P("B.unity"));
            _world.Edit(P("T.png"), "v2");
            var first = Plan();
            var built = BuildOnce(first.Acknowledge(first.Unbuildable.Select(x => x.Root)));

            // B が手元の選択から外れても、上書きの閉包（A と T）は同じなので鍵は同じ。理由は今回の計画が持つ。
            var second = Plan(new[] { _a, _c });
            Assert.That(second.Key, Is.EqualTo(first.Key));
            Assert.That(second.Unbuildable.Single().Reason, Is.EqualTo(ContentOverlayUnbuildableReason.NotInLocalSelection));
            Assert.Throws<InvalidOperationException>(() => second.GetReusableResult(), "認めは受け取りのたびに求める");
            var reused = second.Acknowledge(second.Unbuildable.Select(x => x.Root)).GetReusableResult();
            Assert.That(reused!.Identity, Is.EqualTo(built.Identity));
            Assert.That(Read(reused.PreflightPath).overlayUnbuildable.Single().reason, Is.EqualTo("SourceAbsent"), "preflight は前回の build の記録");
        }

        [Test]
        public void Key_ChangesWithEachInput()
        {
            var baseIdentity = new ContentOverlayBaseIdentity("set", "rev", "target", new string('a', 64), "decl");
            var candidate = new BuildContentCandidate("sk", "A", new string('1', 32), new BuildProvenance("fixture", "A"));
            var closure = new[] { new BuildDependencyEntry(new string('1', 32), "Assets/A.unity"), new BuildDependencyEntry(new string('2', 32), "Assets/T.png") };
            var hashes = new Dictionary<string, string?> { ["Assets/A.unity"] = "h1", ["Assets/A.unity.meta"] = "m1", ["Assets/T.png"] = "h2", ["Assets/T.png.meta"] = "m2" };
            BuildPurposeDeclaration Decl(string representation, string value) => new("decl", new[] { "Root" }, new BuildRequest(Array.Empty<BuildTag>()),
                new BuildRepresentationSelection(new[] { representation }, BuildRepresentationMode.First), new Dictionary<string, string> { ["k"] = value });
            var declaration = Decl("Full", "v");
            string Key(ContentOverlayBaseIdentity? b = null, string set = "ovl", string target = "t", string unity = "u", BuildPurposeDeclaration? d = null,
                BuildContentCandidate? c = null, string rep = "Full", string path = "Assets/A.unity", BuildDependencyEntry[]? entries = null,
                Dictionary<string, string?>? h = null)
                => ContentOverlayReuse.ComputeKey(b ?? baseIdentity, set, target, unity, d ?? declaration, new[] { (c ?? candidate, rep, path) },
                    entries ?? closure, h ?? hashes);
            var reference = Key();
            Assert.That(Key(), Is.EqualTo(reference), "同じ入力から同じ鍵");
            var variants = new Dictionary<string, string>
            {
                ["base.contentSet"] = Key(b: new ContentOverlayBaseIdentity("set2", "rev", "target", new string('a', 64), "decl")),
                ["base.revision"] = Key(b: new ContentOverlayBaseIdentity("set", "rev2", "target", new string('a', 64), "decl")),
                ["base.target"] = Key(b: new ContentOverlayBaseIdentity("set", "rev", "target2", new string('a', 64), "decl")),
                ["base.digest"] = Key(b: new ContentOverlayBaseIdentity("set", "rev", "target", new string('b', 64), "decl")),
                ["base.declaration"] = Key(b: new ContentOverlayBaseIdentity("set", "rev", "target", new string('a', 64), null)),
                ["contentSet"] = Key(set: "ovl2"),
                ["target"] = Key(target: "t2"),
                ["unityVersion"] = Key(unity: "u2"),
                ["declaration.default"] = Key(d: Decl("Full", "v2")),
                ["declaration.representation"] = Key(d: Decl("Whitebox", "v")),
                ["root.stableKey"] = Key(c: new BuildContentCandidate("sk2", "A", new string('1', 32), new BuildProvenance("fixture", "A"))),
                ["root.logicalKey"] = Key(c: new BuildContentCandidate("sk", "A2", new string('1', 32), new BuildProvenance("fixture", "A"))),
                ["root.guid"] = Key(c: new BuildContentCandidate("sk", "A", new string('3', 32), new BuildProvenance("fixture", "A"))),
                ["root.representation"] = Key(rep: "Whitebox"),
                ["root.path"] = Key(path: "Assets/A2.unity"),
                ["file.guid"] = Key(entries: new[] { closure[0], new BuildDependencyEntry(new string('4', 32), "Assets/T.png") }),
                ["file.path"] = Key(entries: new[] { closure[0], new BuildDependencyEntry(new string('2', 32), "Assets/T2.png") }),
                ["file.sha"] = Key(h: new Dictionary<string, string?>(hashes) { ["Assets/T.png"] = "h3" }),
                ["file.meta"] = Key(h: new Dictionary<string, string?>(hashes) { ["Assets/T.png.meta"] = "m3" }),
            };
            foreach (var pair in variants) Assert.That(pair.Value, Is.Not.EqualTo(reference), pair.Key);
            Assert.That(variants.Values.Distinct().Count(), Is.EqualTo(variants.Count));
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
