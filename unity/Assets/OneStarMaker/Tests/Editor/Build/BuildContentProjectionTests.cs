#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using UnityEngine;

namespace OneStarMaker.Tests.Editor.Build
{
    // Unity build を起動せずに plan/snapshot の不整合を検証する。
    // internal constructor を使う fixture は本番選択経路で作れない不正入力も意図的に組み立てる。
    public sealed class BuildContentProjectionTests
    {
        private static BuildContentCandidate Candidate(string stable, string logical, string guid) =>
            new BuildContentCandidate(stable, logical, guid, new BuildProvenance("fixture", stable));
        private static BuildDependencySnapshot Closure(string guid, string path, string shared = "Assets/Shared.mat") =>
            new BuildDependencySnapshot(guid, path, new[] {
                new BuildDependencyEntry(guid, path), new BuildDependencyEntry("shared", shared) });

        [Test]
        public void SelectedRootsShareClosureButExcludeUnselectedVariant()
        {
            // 入力順を逆にしても root は stable key 順。未選択 Variant は閉包に入れず、
            // 複数 root が共有する Material は一度だけ残ることを同時に確認する。
            var a = Candidate("a", "scene", "a-guid");
            var b = Candidate("b", "object", "b-guid");
            var omitted = Candidate("c", "object", "c-guid");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { b, a },
                new[] { new BuildContentExclusion(omitted, BuildExclusionReasonCode.ValueNotSelected, "Representation", "Low") });
            var tags = new Dictionary<string, IReadOnlyList<BuildTag>> {
                [b.StableKey] = new[] { new BuildTag("Representation", "High") } };
            var snapshot = new BuildMaterializationSnapshot(new[] { omitted, b, a }, tags,
                Array.Empty<BuildContentRequirement>(), new[] {
                    Closure("c-guid", "Assets/Omitted.prefab"), Closure("b-guid", "Assets/B.prefab"),
                    Closure("a-guid", "Assets/A.unity") });
            var result = BuildContentProjection.Create(plan, snapshot);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Roots.Select(x => x.Path), Is.EqualTo(new[] { "Assets/A.unity", "Assets/B.prefab" }));
            Assert.That(result.Roots.Select(x => x.Representation), Is.EqualTo(new[] { "", "High" }));
            Assert.That(result.Files.Select(x => x.Path), Does.Not.Contain("Assets/Omitted.prefab"));
            Assert.That(result.Files.Count(x => x.Path == "Assets/Shared.mat"), Is.EqualTo(1));
        }

        [Test]
        public void StructuralMismatchAndPhysicalCollisionArePreflightIssues()
        {
            // 共有依存とは異なり、候補二つが同じ physical key を所有するのは入力衝突。
            var a = Candidate("a", "one", "same");
            var b = Candidate("b", "two", "same");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { a, b },
                Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { a, b },
                new Dictionary<string, IReadOnlyList<BuildTag>>(), Array.Empty<BuildContentRequirement>(),
                new[] { Closure("same", "Assets/A.prefab") });
            var result = BuildContentProjection.Create(plan, snapshot);
            Assert.That(result.Issues.Select(x => x.Code), Does.Contain(BuildContentIssueCode.PhysicalKeyCollision));
        }

        [Test]
        public void MissingAndInconsistentRootAreReportedWithoutAssetDatabase()
        {
            // 閉包 root の GUID/path 不整合と閉包欠損を、Unity I/O 前の issue として返す。
            var selected = Candidate("a", "one", "guid-a");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { selected },
                Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { selected },
                new Dictionary<string, IReadOnlyList<BuildTag>>(), Array.Empty<BuildContentRequirement>(),
                new[] { new BuildDependencySnapshot("guid-a", "Assets/A.prefab",
                    new[] { new BuildDependencyEntry("other", "Assets/A.prefab") }) });
            Assert.That(BuildContentProjection.Create(plan, snapshot).Issues.Select(x => x.Code),
                Does.Contain(BuildContentIssueCode.RootMismatch));
            var missing = new BuildMaterializationSnapshot(new[] { selected },
                new Dictionary<string, IReadOnlyList<BuildTag>>(), Array.Empty<BuildContentRequirement>(),
                Array.Empty<BuildDependencySnapshot>());
            Assert.That(BuildContentProjection.Create(plan, missing).Issues.Select(x => x.Code),
                Does.Contain(BuildContentIssueCode.MissingClosure));
        }

        [Test]
        public void AdapterFailureWritesPreflightAndOutcomeWithoutPublishing()
        {
            // fake port が build 中に失敗しても report を残し、content/{identity} は公開しない。
            var selected = Candidate("a", "one", "guid-a");
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { selected },
                Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { selected },
                new Dictionary<string, IReadOnlyList<BuildTag>>(), Array.Empty<BuildContentRequirement>(),
                new[] { Closure("guid-a", "Assets/A.prefab") });
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "bs2b"));
            var result = new BuildContentCoordinator(new FailingAdapter()).Build(
                new BuildContentRequest(plan, snapshot, "failure-fixture", root));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Issues.Select(x => x.Code), Does.Contain(BuildContentIssueCode.BuildFailure));
            Assert.That(File.Exists(result.PreflightPath), Is.True);
            Assert.That(File.Exists(result.OutcomePath), Is.True);
            Assert.That(Directory.Exists(Path.Combine(root, "content", result.Identity)), Is.False);
        }

        private sealed class FailingAdapter : IContentDirectoryAdapter
        {
            public void ValidateTarget() { }
            public ContentDirectoryBuild Build(BuildContentProjectionResult projection, string identity, string workspace) =>
                throw new IOException("fixture failure");
        }
    }
}
