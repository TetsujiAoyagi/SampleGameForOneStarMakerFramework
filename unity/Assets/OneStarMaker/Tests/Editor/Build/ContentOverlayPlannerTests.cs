#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent.Distribution;

namespace OneStarMaker.Tests.Editor.Build
{
    /// <summary>
    /// 手元の編集から上書きを決める計画の、編集の検出（受け入れ条件 AC2）と分類（AC4）。test は編集の一覧を渡さず、
    /// 偽の手元（<see cref="OverlayPlanWorld"/>）の file と GUID の対応だけを変える。base の manifest は bytes で組む。
    /// 世界: Scene A（Full と Whitebox）・B が texture T を、Scene C が material M（texture U を参照）を参照する。base は A の Full・B・C を選ぶ。
    /// </summary>
    public sealed class ContentOverlayPlannerTests
    {
        private OverlayPlanWorld _world = null!;
        private OverlayPlanWorld.RootDef _a = null!, _aw = null!, _b = null!, _c = null!;

        [SetUp]
        public void SetUp()
        {
            _world = new OverlayPlanWorld();
            foreach (var path in new[] { "Assets/F/T.png", "Assets/F/U.png", "Assets/F/M.mat", "Assets/F/A.unity", "Assets/F/AW.unity", "Assets/F/B.unity", "Assets/F/C.unity" })
                _world.Put(path);
            _a = _world.Root("A", "Full", "Assets/F/A.unity", "Assets/F/T.png");
            _aw = _world.Root("A", "Whitebox", "Assets/F/AW.unity", "Assets/F/T.png");
            _b = _world.Root("B", "Full", "Assets/F/B.unity", "Assets/F/T.png");
            _c = _world.Root("C", "Full", "Assets/F/C.unity", "Assets/F/M.mat", "Assets/F/U.png");
        }

        private OverlayPlanWorld.BaseManifest Base(int recordVersion = 1) => _world.Publish(new[] { _a, _b, _c }, recordVersion);
        private ContentOverlayPlan PlanLocal(OverlayPlanWorld.BaseManifest baseManifest, IEnumerable<string>? supplied = null)
            => _world.Plan(baseManifest, new[] { _a, _b, _c }, new[] { _aw }, supplied);

        private static string[] Edits(ContentOverlayPlan plan) => plan.Edits.Select(x => x.Kind + ":" + x.Path).ToArray();
        private static string[] Overlay(ContentOverlayPlan plan) => plan.OverlayRoots.Select(x => x.Root.LogicalKey + "/" + x.Root.StableKey.Split('-').Last()).ToArray();
        private static string[] Unbuildable(ContentOverlayPlan plan) => plan.Unbuildable.Select(x => x.Root.LogicalKey + ":" + x.Reason).ToArray();
        private static string[] Supplied(ContentOverlayPlan plan) => plan.Supplied.Select(x => x.Root.LogicalKey + "/" + x.Root.Representation).ToArray();

        // ─── AC2: 編集の検出 ───

        [Test]
        public void Detect_ChangedMetaOnlyAddedMovedAndMovedEdited_AllFromTheSameInput()
        {
            var baseManifest = Base();
            _world.Put("Assets/F/N.png");
            var localC = _world.Root("C", "Full", "Assets/F/C.unity", "Assets/F/M.mat", "Assets/F/U.png", "Assets/F/N.png");
            _world.Edit("Assets/F/A.unity", "v2");               // 内容の変更
            _world.EditMeta("Assets/F/U.png");                    // .meta だけの変更
            _world.Move("Assets/F/M.mat", "Assets/G/M.mat");      // 移動
            _world.Move("Assets/F/T.png", "Assets/G/T.png");      // 移動して編集
            _world.Edit("Assets/G/T.png", "v2");
            var plan = _world.Plan(baseManifest, new[] { _a, _b, localC }, new[] { _aw });
            Assert.That(Edits(plan), Is.EquivalentTo(new[]
            {
                "Changed:Assets/F/A.unity", "Changed:Assets/F/U.png", "Added:Assets/F/N.png",
                "Moved:Assets/G/M.mat", "Moved:Assets/G/T.png", "Changed:Assets/G/T.png"
            }));
            Assert.That(plan.Edits.Single(x => x.Kind == ContentOverlayEditKind.Moved && x.Path == "Assets/G/T.png").BasePath, Is.EqualTo("Assets/F/T.png"));
            Assert.That(plan.Edits.Where(x => x.Kind != ContentOverlayEditKind.Added).All(x => x.Guid != null), Is.True, "記録のある base は GUID で突き合わせる");
        }

        [Test]
        public void Detect_MissingAssetDoesNotHideOtherEdits_AndIsNotAnEdit()
        {
            var baseManifest = Base();
            _world.Remove("Assets/F/C.unity");
            _world.Edit("Assets/F/T.png", "v2");
            Assert.That(Edits(PlanLocal(baseManifest)), Is.EqualTo(new[] { "Changed:Assets/F/T.png" }));
        }

        [TestCase(1)]
        [TestCase(0)]
        public void Detect_CaseOnlyPathDifference_IsNotAnEdit(int recordVersion)
        {
            var baseManifest = Base(recordVersion);
            _world.Move("Assets/F/T.png", "Assets/F/t.png");
            Assert.That(PlanLocal(baseManifest).Edits, Is.Empty);
        }

        [Test]
        public void Detect_WithoutRecord_ComparesByPath()
        {
            var baseManifest = Base(recordVersion: 0);
            _world.Put("Assets/F/N.png");
            var localC = _world.Root("C", "Full", "Assets/F/C.unity", "Assets/F/M.mat", "Assets/F/U.png", "Assets/F/N.png");
            _world.Edit("Assets/F/T.png", "v2");
            _world.EditMeta("Assets/F/U.png");
            _world.Move("Assets/F/M.mat", "Assets/G/M.mat");
            var plan = _world.Plan(baseManifest, new[] { _a, _b, localC }, new[] { _aw });
            Assert.That(Edits(plan), Is.EquivalentTo(new[]
                { "Changed:Assets/F/T.png", "Changed:Assets/F/U.png", "Added:Assets/F/N.png", "Added:Assets/G/M.mat" }), "移動は path の比較では追加に見える");
            Assert.That(plan.Edits.All(x => x.Guid == null), Is.True);
        }

        [TestCase(1)]
        [TestCase(0)]
        public void Detect_FileOnlyInAnExcludedCandidatesClosure_IsNotAnEdit(int recordVersion)
        {
            var baseManifest = Base(recordVersion);
            _world.Edit("Assets/F/AW.unity", "v2");
            var plan = PlanLocal(baseManifest);
            Assert.That(plan.Edits, Is.Empty);
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.NotRequired));
        }

        // ─── AC4: 分類 ───

        [Test]
        public void Plan_SharedEdit_PutsEveryRootWhoseClosureHasItIntoTheOverlay()
        {
            var baseManifest = Base();
            _world.Edit("Assets/F/T.png", "v2");
            var plan = PlanLocal(baseManifest);
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.BuildRequired));
            Assert.That(Overlay(plan), Is.EqualTo(new[] { "A/Full", "B/Full" }));
            Assert.That(plan.OverlayRoots.All(x => x.Edits.SequenceEqual(new[] { "Assets/F/T.png" })), Is.True);
            Assert.That(plan.Unbuildable, Is.Empty);
            Assert.That(plan.Key, Is.Not.Null.And.Length.EqualTo(64));
            Assert.That(plan.BaseIdentity.ManifestDigest, Is.EqualTo(baseManifest.Digest));
        }

        [Test]
        public void Plan_RootFileAbsent_IsUnbuildableEvenWhenItsKeyIsSupplied()
        {
            var baseManifest = Base();
            _world.Remove("Assets/F/B.unity");
            _world.Edit("Assets/F/T.png", "v2");
            var plan = PlanLocal(baseManifest, supplied: new[] { "B" });
            Assert.That(Unbuildable(plan), Is.EqualTo(new[] { "B:SourceAbsent" }));
            Assert.That(plan.Supplied, Is.Empty);
            var b = plan.Unbuildable.Single();
            Assert.That(b.Edits, Is.EqualTo(new[] { "Assets/F/T.png" }));
            Assert.That(b.MissingFiles, Is.EqualTo(new[] { "Assets/F/B.unity" }), "root の GUID が解決しない時は記録の path で補う");
            Assert.That(b.Root.Kind, Is.EqualTo("Scene"), "kind は記録から取る");
            Assert.That(Overlay(plan), Is.EqualTo(new[] { "A/Full" }));
        }

        [Test]
        public void Plan_SuppliedKey_AppliesToEveryRepresentationOfTheSceneNode_ButNotToAttachedAssets()
        {
            _world.Put("Assets/F/P.prefab");
            var prop = _world.Root("A:prop", "Full", "Assets/F/P.prefab", "Assets/F/T.png");
            var baseManifest = _world.Publish(new[] { _a, _aw, _b, prop });
            _world.Edit("Assets/F/T.png", "v2");
            var plan = _world.Plan(baseManifest, new[] { _a, _aw, _b, prop }, null, supplied: new[] { "A" });
            Assert.That(Supplied(plan), Is.EqualTo(new[] { "A/Full", "A/Whitebox" }));
            Assert.That(Overlay(plan), Is.EqualTo(new[] { "A:prop/Full", "B/Full" }));
        }

        [Test]
        public void Plan_SuppliedSceneWithMissingDependency_IsSupplied_NotUnbuildable()
        {
            var baseManifest = Base();
            _world.Remove("Assets/F/M.mat");
            _world.Edit("Assets/F/U.png", "v2");
            var plan = PlanLocal(baseManifest, supplied: new[] { "C" });
            Assert.That(Supplied(plan), Is.EqualTo(new[] { "C/Full" }));
            Assert.That(plan.Unbuildable, Is.Empty);
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.NotRequired));
        }

        [Test]
        public void Plan_RecordAssetUnresolvedLocally_MakesTheRootSourceAbsent_EvenThoughUnityReturnsASmallerClosure()
        {
            // material M を退かすと Unity は C の依存に M を返さない（欠けの記録が出ず、手元の閉包が小さく見えるだけ）。記録の閉包で欠けを見つける。
            var baseManifest = Base();
            _world.Remove("Assets/F/M.mat");
            _world.Edit("Assets/F/U.png", "v2");
            var plan = PlanLocal(baseManifest);
            Assert.That(Unbuildable(plan), Is.EqualTo(new[] { "C:SourceAbsent" }));
            Assert.That(plan.Unbuildable.Single().Edits, Is.EqualTo(new[] { "Assets/F/U.png" }));
            Assert.That(plan.Unbuildable.Single().MissingFiles, Is.EqualTo(new[] { "Assets/F/M.mat" }));
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.UnbuildableOnly));
        }

        [Test]
        public void Plan_MovedAndEditedAsset_HitsTheRecordClosureOfASourceAbsentRoot()
        {
            var baseManifest = Base();
            _world.Remove("Assets/F/B.unity");
            _world.Move("Assets/F/T.png", "Assets/G/T.png");
            _world.Edit("Assets/G/T.png", "v2");
            var plan = PlanLocal(baseManifest);
            Assert.That(Unbuildable(plan), Is.EqualTo(new[] { "B:SourceAbsent" }));
            Assert.That(plan.Unbuildable.Single().Edits, Is.EqualTo(new[] { "Assets/G/T.png" }));
            Assert.That(Overlay(plan), Is.EqualTo(new[] { "A/Full" }));
        }

        [Test]
        public void Plan_RecordRootMissingFromLocalSelection_WithEdit_IsNotInLocalSelection()
        {
            var baseManifest = Base();
            _world.Edit("Assets/F/U.png", "v2");
            var plan = _world.Plan(baseManifest, new[] { _a, _b }, new[] { _aw });
            Assert.That(Unbuildable(plan), Is.EqualTo(new[] { "C:NotInLocalSelection" }));
            Assert.That(plan.Unbuildable.Single().Root.StableKey, Is.Null);
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.UnbuildableOnly));
        }

        [TestCase(0, ContentOverlayUnbuildableReason.RecordMissing)]
        [TestCase(2, ContentOverlayUnbuildableReason.RecordUnsupported)]
        [TestCase(-1, ContentOverlayUnbuildableReason.RecordInvalid)]
        public void Plan_WithoutUsableRecord_EveryAbsentSelectedRootIsUnbuildable_EvenWithNoEdits(int recordVersion, ContentOverlayUnbuildableReason reason)
        {
            var baseManifest = Base(recordVersion);
            _world.Remove("Assets/F/B.unity");
            var plan = PlanLocal(baseManifest);
            Assert.That(plan.Edits, Is.Empty);
            Assert.That(Unbuildable(plan), Is.EqualTo(new[] { "B:" + reason }));
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.UnbuildableOnly), "「上書き不要・作り直せない root なし」を返さない");
        }

        [Test]
        public void Plan_WithoutRecord_RootWhoseDependencyUnityReportsMissing_IsUnbuildable()
        {
            var baseManifest = Base(recordVersion: 0);
            _world.ReportMissingDependencies = true;
            _world.Remove("Assets/F/M.mat");
            var plan = PlanLocal(baseManifest);
            Assert.That(Unbuildable(plan), Is.EqualTo(new[] { "C:RecordMissing" }));
            Assert.That(plan.Unbuildable.Single().MissingFiles, Is.EqualTo(new[] { "Assets/F/M.mat" }));
        }

        [TestCase(1)]
        [TestCase(0)]
        [TestCase(2)]
        [TestCase(-1)]
        public void Plan_FullCheckout_SameAnswerWithOrWithoutUsableRecord(int recordVersion)
        {
            var baseManifest = Base(recordVersion);
            _world.Edit("Assets/F/T.png", "v2");
            var plan = PlanLocal(baseManifest);
            Assert.That(Overlay(plan), Is.EqualTo(new[] { "A/Full", "B/Full" }));
            Assert.That(plan.Unbuildable, Is.Empty);
        }

        [Test]
        public void Plan_RootAddedLocally_GoesToOverlayWithoutEdits_OrIsNotInBaseWhenItsSourceIsAbsent()
        {
            var baseManifest = Base();
            _world.Put("Assets/F/D.unity");
            var d = _world.Root("D", "Full", "Assets/F/D.unity", "Assets/F/T.png");
            var plan = _world.Plan(baseManifest, new[] { _a, _b, _c, d }, new[] { _aw });
            Assert.That(Overlay(plan), Is.EqualTo(new[] { "D/Full" }));
            Assert.That(plan.OverlayRoots.Single().NotInBase, Is.True);

            _world.Remove("Assets/F/D.unity");
            plan = _world.Plan(baseManifest, new[] { _a, _b, _c, d }, new[] { _aw });
            Assert.That(Unbuildable(plan), Is.EqualTo(new[] { "D:NotInBase" }));
        }

        [Test]
        public void Plan_WithoutRecord_RootAddedLocally_EntersOnlyThroughItsNewFiles()
        {
            var baseManifest = Base(recordVersion: 0);
            _world.Put("Assets/F/D.unity");
            var d = _world.Root("D", "Full", "Assets/F/D.unity", "Assets/F/T.png");
            var plan = _world.Plan(baseManifest, new[] { _a, _b, _c, d }, new[] { _aw });
            Assert.That(Overlay(plan), Is.EqualTo(new[] { "D/Full" }));
            Assert.That(plan.OverlayRoots.Single().NotInBase, Is.False);
            Assert.That(Edits(plan), Is.EqualTo(new[] { "Added:Assets/F/D.unity" }));
        }

        [Test]
        public void Plan_SameEditOnSuppliedSceneAndSourceAbsentRoot_IsUnbuildableOnly_AndKeepsTheSuppliedScene()
        {
            var baseManifest = Base();
            _world.Remove("Assets/F/B.unity");
            _world.Edit("Assets/F/T.png", "v2");
            var plan = PlanLocal(baseManifest, supplied: new[] { "A" });
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.UnbuildableOnly));
            Assert.That(Supplied(plan), Is.EqualTo(new[] { "A/Full" }));
            Assert.That(Unbuildable(plan), Is.EqualTo(new[] { "B:SourceAbsent" }));
            Assert.That(plan.Key, Is.Null);
        }

        [Test]
        public void Plan_AllEditedRootsSupplied_IsNotRequired_AndKeepsTheSuppliedRoots()
        {
            var baseManifest = Base();
            _world.Edit("Assets/F/T.png", "v2");
            var plan = PlanLocal(baseManifest, supplied: new[] { "A", "B" });
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.NotRequired));
            Assert.That(Supplied(plan), Is.EqualTo(new[] { "A/Full", "B/Full" }));
        }

        [Test]
        public void Plan_SourceAbsentRootWithoutEditsInItsRecordClosure_AppearsNowhere()
        {
            var baseManifest = Base();
            _world.Remove("Assets/F/B.unity");
            _world.Remove("Assets/F/M.mat");
            var plan = PlanLocal(baseManifest);
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.NotRequired));
            Assert.That(plan.Unbuildable, Is.Empty);
            Assert.That(plan.Supplied, Is.Empty);
        }

        [Test]
        public void Plan_SelectedRootWithNeitherClosureNorAbsence_Throws()
        {
            // 合成が欠けの記録を落とした snapshot（閉包の無い selected の root が黙って通らない）。
            var baseManifest = Base();
            _world.Remove("Assets/F/B.unity");
            var (plan, snapshot) = _world.Select(new[] { _a, _b, _c }, new[] { _aw });
            var dropped = new BuildMaterializationSnapshot(snapshot.Candidates,
                snapshot.Candidates.ToDictionary(x => x.StableKey, x => (IReadOnlyList<BuildTag>)snapshot.TagProviders.SelectMany(p => p.GetTags(x)).ToList()),
                snapshot.Requirements, snapshot.Dependencies);
            Assert.Throws<ArgumentException>(() => _world.PlanWith(baseManifest, plan, dropped));
        }

        [TestCase("TooLarge")]
        [TestCase("DigestMismatch")]
        [TestCase("NotJson")]
        [TestCase("ContentSetMismatch")]
        [TestCase("RevisionMismatch")]
        [TestCase("TargetMismatch")]
        public void Plan_BaseManifestThatDoesNotMatchTheInstall_ThrowsInsteadOfClassifying(string mismatch)
        {
            var baseManifest = Base();
            _world.Edit("Assets/F/T.png", "v2");
            switch (mismatch)
            {
                case "TooLarge": { var big = new byte[4 * 1024 * 1024 + 1]; baseManifest.SetBytes(big, OverlayPlanWorld.Sha(big)); break; }
                case "DigestMismatch": baseManifest.SetBytes(baseManifest.Bytes, new string('0', 64)); break;
                case "NotJson": { var bytes = System.Text.Encoding.UTF8.GetBytes("not json"); baseManifest.SetBytes(bytes, OverlayPlanWorld.Sha(bytes)); break; }
                case "ContentSetMismatch": baseManifest.Manifest.contentSet = "other"; baseManifest.Refresh(); break;
                case "RevisionMismatch": baseManifest.Manifest.revision = "other"; baseManifest.Refresh(); break;
                case "TargetMismatch": baseManifest.Manifest.target = "other"; baseManifest.Refresh(); break;
            }
            Assert.Throws<InvalidOperationException>(() => PlanLocal(baseManifest));
        }

        [Test]
        public void Plan_WritesNothing_AndNeverBuilds()
        {
            var baseManifest = Base();
            _world.Edit("Assets/F/T.png", "v2");
            var root = Path.Combine(Path.GetTempPath(), "osm-s34a-plan-" + Guid.NewGuid().ToString("N"));
            var plan = _world.Plan(baseManifest, new[] { _a, _b, _c }, new[] { _aw }, null, root);
            Assert.That(plan.State, Is.EqualTo(ContentOverlayPlanState.BuildRequired));
            Assert.That(Directory.Exists(root), Is.False, "計画は成果物の root を作らない");
        }
    }
}
