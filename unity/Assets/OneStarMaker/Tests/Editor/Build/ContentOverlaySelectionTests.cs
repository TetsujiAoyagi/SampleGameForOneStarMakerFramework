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
    /// 上書きの逆引き（HANDOFF CONTENT_OVERLAY_BUILD の AC1）、plan の絞りと上書きの request（AC3）、
    /// 上書きの preflight と outcome の記録（AC9 の単体）。Unity の build は起動しない（AC9 は偽の adapter）。
    /// 閉包は実機の fixture と同じ形（6 root）を手で書く。実 AssetDatabase から作った閉包での確認は実機の AC2 が持つ。
    /// </summary>
    public sealed class ContentOverlaySelectionTests
    {
        internal const string Dir = "Assets/Fx/";
        internal const string EditedTex = Dir + "EditedTex.png";
        internal const string SharedTex = Dir + "SharedTex.png";
        internal const string UnlistedTex = Dir + "UnlistedTex.png";
        internal const string CleanOnlyTex = Dir + "CleanOnlyTex.png";
        internal const string CanaryTex = Dir + "CanaryTex.png";
        internal const string UnusedTex = Dir + "UnusedTex.png";
        internal const string EditedPrefab = Dir + "EditedPrefab.prefab";
        internal const string OuterPrefab = Dir + "OuterPrefab.prefab";
        internal const string InnerPrefab = Dir + "InnerPrefab.prefab";

        private static readonly string[] Edits = { EditedTex, EditedPrefab, InnerPrefab, UnusedTex };
        private static readonly string[] OverlayRoots = { "PrefabRoot", "SceneDirect", "SceneInstance", "SceneNested" };

        // ───── AC1: 逆引き ─────

        [Test]
        public void Select_PicksEveryRootWhoseClosureHasAnEdit_WithReasons()
        {
            var t = Topology.Create();
            var result = ContentOverlaySelection.Select(t.Plan, t.Snapshot, Edits);
            Assert.That(result.IsOverlayRequired, Is.True);
            Assert.That(result.Roots.Select(x => x.Root.StableKey), Is.EqualTo(OverlayRoots));
            Assert.That(Reasons(result), Is.EqualTo(new[]
            {
                "PrefabRoot=" + EditedTex,
                "SceneDirect=" + EditedTex,
                "SceneInstance=" + EditedPrefab,
                "SceneNested=" + InnerPrefab,
            }));
            Assert.That(result.Unmatched.Select(x => x.Path + "=" + x.Reason),
                Is.EqualTo(new[] { UnusedTex + "=" + ContentOverlayUnmatchedReason.NotInAnyClosure }));
            Assert.That(result.Edits, Is.EqualTo(Edits.OrderBy(x => x, StringComparer.Ordinal)));
            Assert.That(result.Plan!.SelectedContent.Select(x => x.StableKey), Is.EqualTo(OverlayRoots));
            Assert.That(result.Snapshot, Is.SameAs(t.Snapshot));
        }

        [Test]
        public void Select_RootItselfInEdits_IsItsOwnReason()
        {
            var t = Topology.Create();
            var result = ContentOverlaySelection.Select(t.Plan, t.Snapshot, new[] { Topology.Path("SceneClean") });
            Assert.That(Reasons(result), Is.EqualTo(new[] { "SceneClean=" + Topology.Path("SceneClean") }));
        }

        [Test]
        public void Select_UnmatchedPaths_CarryReasonCodes_WithoutThrowing()
        {
            var t = Topology.Create();
            var inputs = new[]
            {
                Dir + "Behaviour.cs",          // 閉包が除く拡張子
                Dir + "Missing.png",           // 存在しない
                "Assets/Fx",                   // folder
                UnusedTex,                     // どの root も使わない
                "Packages/com.example/Tex.png",
                "assets/Fx/EditedTex.png",     // 接頭辞は ordinal
            };
            var result = ContentOverlaySelection.Select(t.Plan, t.Snapshot, inputs);
            Assert.That(result.IsOverlayRequired, Is.False);
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Unmatched.Select(x => x.Path + "=" + x.Reason), Is.EqualTo(new[]
            {
                "Assets/Fx=NotInAnyClosure",
                Dir + "Behaviour.cs=NotInAnyClosure",
                Dir + "Missing.png=NotInAnyClosure",
                UnusedTex + "=NotInAnyClosure",
                "Packages/com.example/Tex.png=OutsideAssets",
                "assets/Fx/EditedTex.png=OutsideAssets",
            }));
            Assert.That(result.Roots, Is.Empty);
            Assert.That(result.SharedAssets, Is.Empty);
        }

        [Test]
        public void Select_NormalizesSeparatorsAndMeta_AndIgnoresCaseAfterTheAssetsPrefix()
        {
            var t = Topology.Create();
            var result = ContentOverlaySelection.Select(t.Plan, t.Snapshot, new[]
            {
                CleanOnlyTex, CleanOnlyTex + ".meta",          // 1 件にまとまる
                "Assets\\Fx\\canarytex.PNG",                  // \ 区切り、Assets/ の後ろの大小違い
            });
            Assert.That(result.Edits, Is.EqualTo(new[] { CleanOnlyTex, "Assets/Fx/canarytex.PNG" }));
            Assert.That(Reasons(result), Is.EqualTo(new[] { "SceneClean=" + CleanOnlyTex, "SceneUnlisted=Assets/Fx/canarytex.PNG" }));
            Assert.That(result.Unmatched, Is.Empty);
        }

        [Test]
        public void Select_NoEdits_NeedsNoOverlay()
        {
            var t = Topology.Create();
            var result = ContentOverlaySelection.Select(t.Plan, t.Snapshot, Array.Empty<string>());
            Assert.That(result.IsOverlayRequired, Is.False);
            Assert.That(result.Plan, Is.Null);
            Assert.That(result.Edits, Is.Empty);
            Assert.That(result.Unmatched, Is.Empty);
        }

        [Test]
        public void Select_SharedAssets_AreTheIntersectionOfOverlayAndRemainingClosures()
        {
            var t = Topology.Create();
            var result = ContentOverlaySelection.Select(t.Plan, t.Snapshot, Edits);
            Assert.That(result.SharedAssets, Does.Contain(SharedTex).And.Contains(UnlistedTex));
            Assert.That(result.SharedAssets, Does.Not.Contain(CleanOnlyTex).And.Not.Contains(CanaryTex).And.Not.Contains(EditedTex));
            Assert.That(result.SharedAssets, Is.Ordered.Using((IComparer<string>)StringComparer.Ordinal));
        }

        [Test]
        public void Select_SelectedRootWithoutExactlyOneClosure_Throws()
        {
            var t = Topology.Create();
            var missing = new BuildMaterializationSnapshot(t.Snapshot.Candidates, Topology.NoTags,
                Array.Empty<BuildContentRequirement>(), t.Snapshot.Dependencies.Where(x => x.RootGuid != Topology.Guid("SceneClean")));
            Assert.Throws<ArgumentException>(() => ContentOverlaySelection.Select(t.Plan, missing, Edits));
            var duplicate = new BuildMaterializationSnapshot(t.Snapshot.Candidates, Topology.NoTags,
                Array.Empty<BuildContentRequirement>(), t.Snapshot.Dependencies.Concat(new[] { Topology.Closure("SceneClean") }));
            Assert.Throws<ArgumentException>(() => ContentOverlaySelection.Select(t.Plan, duplicate, Array.Empty<string>()));
        }

        [Test]
        public void Select_NullPath_Throws()
        {
            var t = Topology.Create();
            Assert.Throws<ArgumentException>(() => ContentOverlaySelection.Select(t.Plan, t.Snapshot, new string[] { EditedTex, null! }));
        }

        // ───── AC3: plan の絞りと上書きの request ─────

        [Test]
        public void WithSelectedSubset_KeepsKeysAndExclusions_AndPairsWithTheOriginalSnapshot()
        {
            var t = Topology.Create();
            var narrowed = t.Plan.WithSelectedSubset(new[] { "SceneDirect", "PrefabRoot", "SceneDirect" });
            Assert.That(narrowed.SelectedContent.Select(x => x.StableKey + "|" + x.LogicalKey + "|" + x.PhysicalKey), Is.EqualTo(
                t.Plan.SelectedContent.Where(x => x.StableKey == "PrefabRoot" || x.StableKey == "SceneDirect")
                    .Select(x => x.StableKey + "|" + x.LogicalKey + "|" + x.PhysicalKey)));
            var reasons = narrowed.ExcludedContent.ToDictionary(x => x.Candidate.StableKey, x => x.ReasonCode);
            Assert.That(reasons[Topology.ExcludedKey], Is.EqualTo(BuildExclusionReasonCode.Unreachable));
            foreach (var key in new[] { "SceneClean", "SceneInstance", "SceneNested", "SceneUnlisted" })
                Assert.That(reasons[key], Is.EqualTo(BuildExclusionReasonCode.NotInSubset), key);
            Assert.That(narrowed.ExcludedContent.Count, Is.EqualTo(5));
            Assert.That(narrowed.Request.Selections, Is.EqualTo(t.Plan.Request.Selections));
            var projection = BuildContentProjection.Create(narrowed, t.Snapshot);
            Assert.That(projection.Issues, Is.Empty);
            Assert.That(projection.Roots.Select(x => x.Candidate.StableKey), Is.EqualTo(new[] { "PrefabRoot", "SceneDirect" }));
        }

        [Test]
        public void WithSelectedSubset_RejectsEmptyAndUnselectedKeys()
        {
            var t = Topology.Create();
            Assert.Throws<ArgumentException>(() => t.Plan.WithSelectedSubset(Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => t.Plan.WithSelectedSubset(new[] { "SceneDirect", "Nope" }));
            Assert.Throws<ArgumentException>(() => t.Plan.WithSelectedSubset(new[] { Topology.ExcludedKey }));
        }

        [Test]
        public void OverlayRequest_TakesPlanAndSnapshotFromTheSelection_AndIsFull()
        {
            var t = Topology.Create();
            var selection = ContentOverlaySelection.Select(t.Plan, t.Snapshot, Edits);
            var request = new BuildContentRequest(selection, BaseIdentity(), "overlay_a", "root", null, null);
            Assert.That(request.Plan, Is.SameAs(selection.Plan));
            Assert.That(request.Snapshot, Is.SameAs(selection.Snapshot));
            Assert.That(request.Mode, Is.EqualTo(BuildContentMode.Full));
            Assert.That(request.Overlay!.Selection, Is.SameAs(selection));
            // 上書きの request は、逆引きの結果と別の plan や snapshot を受ける引数を持たない（I-3）。
            var ctor = typeof(BuildContentRequest).GetConstructors()
                .Single(c => c.GetParameters().Any(p => p.ParameterType == typeof(ContentOverlaySelectionResult)));
            Assert.That(ctor.GetParameters().Select(p => p.ParameterType),
                Has.None.EqualTo(typeof(BuildPlan)).And.None.EqualTo(typeof(BuildMaterializationSnapshot)));
        }

        [Test]
        public void OverlayRequest_RejectsASelectionThatNeedsNoOverlay()
        {
            var t = Topology.Create();
            var none = ContentOverlaySelection.Select(t.Plan, t.Snapshot, new[] { UnusedTex });
            Assert.Throws<ArgumentException>(() => new BuildContentRequest(none, BaseIdentity(), "overlay_a", "root", null, null));
        }

        // ───── AC9 の単体: 上書きの preflight と outcome ─────

        [Test]
        public void OverlayBuild_WritesBaseIdentityEditsReasonsUnmatchedAndSharedToPreflight_AndMarksOutcome()
        {
            var t = Topology.Create();
            var selection = ContentOverlaySelection.Select(t.Plan, t.Snapshot, Edits);
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture",
                "ovlsel-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            try
            {
                var request = new BuildContentRequest(selection, BaseIdentity(), "overlay_a", root, null, null);
                var result = new BuildContentCoordinator(new ManifestAdapter()).Build(request);
                Assert.That(result.IsSuccess, Is.True, result.Summary);
                var preflight = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(result.PreflightPath));
                Assert.That(preflight.overlay, Is.True);
                Assert.That(preflight.overlayBase.contentSet, Is.EqualTo("base_a"));
                Assert.That(preflight.overlayBase.revision, Is.EqualTo("rev-1"));
                Assert.That(preflight.overlayBase.target, Is.EqualTo(BuildContentCoordinator.TargetName));
                Assert.That(preflight.overlayBase.manifestDigest, Is.EqualTo("digest-1"));
                Assert.That(preflight.overlayBase.declarationName, Is.Empty);
                Assert.That(preflight.overlayEdits, Is.EqualTo(Edits.OrderBy(x => x, StringComparer.Ordinal)));
                Assert.That(preflight.overlayRoots.Select(x => x.stableKey + "/" + x.logicalKey + "=" + string.Join(";", x.edits)), Is.EqualTo(new[]
                {
                    "PrefabRoot/PrefabRoot=" + EditedTex,
                    "SceneDirect/SceneDirect=" + EditedTex,
                    "SceneInstance/SceneInstance=" + EditedPrefab,
                    "SceneNested/SceneNested=" + InnerPrefab,
                }));
                Assert.That(preflight.overlayUnmatched.Select(x => x.path + "=" + x.reason), Is.EqualTo(new[] { UnusedTex + "=NotInAnyClosure" }));
                Assert.That(preflight.overlaySharedAssets, Is.EqualTo(selection.SharedAssets));
                Assert.That(preflight.selected.Select(x => x.Split('|')[0]), Is.EqualTo(OverlayRoots));
                Assert.That(preflight.buildMode, Is.Empty);
                var outcome = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(result.OutcomePath));
                Assert.That(outcome.overlay, Is.True);
                Assert.That(outcome.buildMode, Is.EqualTo("Full"));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        internal static ContentOverlayBaseIdentity BaseIdentity(string? declarationName = null) =>
            new ContentOverlayBaseIdentity("base_a", "rev-1", BuildContentCoordinator.TargetName, "digest-1", declarationName);

        private static IEnumerable<string> Reasons(ContentOverlaySelectionResult result) =>
            result.Roots.Select(x => x.Root.StableKey + "=" + string.Join(";", x.Edits));

        /// <summary>
        /// 実機の fixture と同じ形の 6 root と、除外済みの候補 1 つ。stable key と logical key は root の名前、physical key は名前から作る GUID。
        /// SceneDirect は別々の material で EditedTex・SharedTex・UnlistedTex を、PrefabRoot は EditedTex を直接参照する。
        /// </summary>
        internal sealed class Topology
        {
            public const string ExcludedKey = "Excluded";
            public static readonly IReadOnlyDictionary<string, IReadOnlyList<BuildTag>> NoTags = new Dictionary<string, IReadOnlyList<BuildTag>>();

            private static readonly Dictionary<string, string[]> Dependencies = new Dictionary<string, string[]>
            {
                ["SceneDirect"] = new[] { Dir + "EditedMat.mat", EditedTex, Dir + "SharedMat.mat", SharedTex, Dir + "UnlistedMat.mat", UnlistedTex, Dir + "Shader.shader" },
                ["SceneInstance"] = new[] { EditedPrefab },
                ["SceneNested"] = new[] { OuterPrefab, InnerPrefab },
                ["PrefabRoot"] = new[] { Dir + "EditedMat.mat", EditedTex, Dir + "Shader.shader" },
                ["SceneClean"] = new[] { Dir + "SharedMat.mat", SharedTex, Dir + "CleanMat.mat", CleanOnlyTex, Dir + "Shader.shader" },
                ["SceneUnlisted"] = new[] { Dir + "UnlistedMat.mat", UnlistedTex, Dir + "CanaryMat.mat", CanaryTex, Dir + "Shader.shader" },
                [ExcludedKey] = new[] { EditedTex },
            };

            public BuildPlan Plan { get; private set; } = null!;
            public BuildMaterializationSnapshot Snapshot { get; private set; } = null!;

            public static string Path(string root) => Dir + root + (root == "PrefabRoot" || root == ExcludedKey ? ".prefab" : ".unity");
            public static string Guid(string root) => (root.ToLowerInvariant() + new string('0', 32)).Substring(0, 32);

            public static BuildContentCandidate Candidate(string root) =>
                new BuildContentCandidate(root, root, Guid(root), new BuildProvenance("fixture", root));

            public static BuildDependencySnapshot Closure(string root) =>
                new BuildDependencySnapshot(Guid(root), Path(root), new[] { new BuildDependencyEntry(Guid(root), Path(root)) }
                    .Concat(Dependencies[root].Select(p => new BuildDependencyEntry("dep:" + p, p))));

            public static Topology Create()
            {
                var selected = Dependencies.Keys.Where(x => x != ExcludedKey).Select(Candidate).ToArray();
                var excluded = Candidate(ExcludedKey);
                return new Topology
                {
                    Plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), selected,
                        new[] { new BuildContentExclusion(excluded, BuildExclusionReasonCode.Unreachable, "LogicalKey", ExcludedKey) }),
                    Snapshot = new BuildMaterializationSnapshot(selected.Concat(new[] { excluded }), NoTags,
                        Array.Empty<BuildContentRequirement>(), Dependencies.Keys.Select(Closure)),
                };
            }
        }

        // Unity の代わりに workspace へ manifest を書く（BuildContentFullModeTests の偽 adapter と同じ形）。
        internal sealed class ManifestAdapter : IContentDirectoryAdapter
        {
            public BuildPurposeDeclaration? Declaration { get; private set; }
            public int Calls { get; private set; }
            public void ValidateTarget() { }
            public ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace,
                BuildPurposeDeclaration? declaration)
            {
                Calls++;
                Declaration = declaration;
                var manifest = System.IO.Path.Combine(workspace, "manifest.txt");
                File.WriteAllText(manifest, identity);
                return new ContentDirectoryBuild("ok", manifest, workspace);
            }
        }
    }
}
