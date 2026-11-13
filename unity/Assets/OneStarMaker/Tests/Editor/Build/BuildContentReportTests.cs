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
    /// preflight.json の後方互換（D4）。到達経路 `requirements[]` は追加項目で、既存の項目の意味と形を変えず、
    /// 旧い形だけを知る読み手は未知の項目を無視して同じ値を読む。
    /// </summary>
    public sealed class BuildContentReportTests
    {
        // 旧 build の読み手が知っている形（Player build の検証・publisher と同じく JsonUtility で読む）。
        [Serializable] private sealed class LegacyPreflight
        {
            public string identity = "";
            public string target = "";
            public string contentSet = "";
            public string[] selected = Array.Empty<string>();
            public string[] excluded = Array.Empty<string>();
            public string[] roots = Array.Empty<string>();
            public string[] closure = Array.Empty<string>();
            public string[] issues = Array.Empty<string>();
        }

        [Test]
        public void Preflight_WithRequirements_KeepsLegacyFieldsAndSortsRequirements()
        {
            var candidate = new BuildContentCandidate("stable", "logical", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", new BuildProvenance("fixture", "logical"));
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { candidate }, Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { candidate },
                new Dictionary<string, IReadOnlyList<BuildTag>> { [candidate.StableKey] = new[] { new BuildTag("Representation", "Full") } },
                Array.Empty<BuildContentRequirement>(),
                new[] { new BuildDependencySnapshot(candidate.PhysicalKey, "Assets/A.unity", new[] { new BuildDependencyEntry(candidate.PhysicalKey, "Assets/A.unity") }) });
            var projection = BuildContentProjection.Create(plan, snapshot);
            var requirements = new[]
            {
                new BuildPurposeRequirement("zeta", "Full", new[] { "Root", "zeta" }),
                new BuildPurposeRequirement("alpha", "Whitebox", new[] { "Root", "alpha" }),
                new BuildPurposeRequirement("alpha", "Full", new[] { "Root", "alpha" }),
            };
            var path = Path.Combine(Path.GetTempPath(), "osm-bs5a-preflight-" + Guid.NewGuid().ToString("N"), "preflight.json");
            try
            {
                BuildContentReport.WritePreflight(path, "identity", "target", "set", plan, projection, requirements);
                var json = File.ReadAllText(path);

                var legacy = JsonUtility.FromJson<LegacyPreflight>(json);
                Assert.That(legacy.identity, Is.EqualTo("identity"));
                Assert.That(legacy.contentSet, Is.EqualTo("set"));
                Assert.That(legacy.selected, Is.EqualTo(new[] { "stable|logical|aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }));
                Assert.That(legacy.roots.Single(), Does.EndWith("|Assets/A.unity|Full"));
                Assert.That(legacy.issues, Is.Empty);

                var current = JsonUtility.FromJson<ContentReportFile>(json);
                Assert.That(current.requirements.Select(x => x.logicalKey + "/" + x.representation),
                    Is.EqualTo(new[] { "alpha/Full", "alpha/Whitebox", "zeta/Full" }));
                Assert.That(current.requirements[0].path, Is.EqualTo(new[] { "Root", "alpha" }));
            }
            finally
            {
                if (File.Exists(path)) Directory.Delete(Path.GetDirectoryName(path)!, true);
            }
        }

        [Test]
        public void Preflight_WithoutRequirements_ReadsBackAsEmptyArray()
        {
            var candidate = new BuildContentCandidate("stable", "logical", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", new BuildProvenance("fixture", "logical"));
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { candidate }, Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { candidate }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(),
                new[] { new BuildDependencySnapshot(candidate.PhysicalKey, "Assets/A.unity", new[] { new BuildDependencyEntry(candidate.PhysicalKey, "Assets/A.unity") }) });
            var path = Path.Combine(Path.GetTempPath(), "osm-bs5a-preflight-" + Guid.NewGuid().ToString("N"), "preflight.json");
            try
            {
                BuildContentReport.WritePreflight(path, "identity", "target", "set", plan, BuildContentProjection.Create(plan, snapshot));
                var current = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(path));
                Assert.That(current.requirements, Is.Empty);
            }
            finally
            {
                if (File.Exists(path)) Directory.Delete(Path.GetDirectoryName(path)!, true);
            }
        }

        [Test]
        public void NonOverlayBuild_LeavesOverlayFieldsAtDefaults_InPreflightAndOutcome()
        {
            // 上書きの情報を渡さない build（既存のコンストラクタ）は、上書きの欄が既定値（印が false、配列が空）である以外は従来と同じ。
            var candidate = new BuildContentCandidate("stable", "logical", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", new BuildProvenance("fixture", "logical"));
            var plan = new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), new[] { candidate }, Array.Empty<BuildContentExclusion>());
            var snapshot = new BuildMaterializationSnapshot(new[] { candidate }, new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(),
                new[] { new BuildDependencySnapshot(candidate.PhysicalKey, "Assets/A.unity", new[] { new BuildDependencyEntry(candidate.PhysicalKey, "Assets/A.unity") }) });
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "TestResults", "bs3-fixture",
                "ovlrep-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
            try
            {
                var request = new BuildContentRequest(plan, snapshot, "set", root, null, null, BuildContentMode.Full);
                Assert.That(request.Overlay, Is.Null);
                var result = new BuildContentCoordinator(new ContentOverlaySelectionTests.ManifestAdapter()).Build(request);
                Assert.That(result.IsSuccess, Is.True, result.Summary);
                foreach (var file in new[] { result.PreflightPath, result.OutcomePath })
                {
                    var report = JsonUtility.FromJson<ContentReportFile>(File.ReadAllText(file));
                    Assert.That(report.overlay, Is.False, file);
                    Assert.That(report.overlayBase.contentSet + report.overlayBase.revision + report.overlayBase.target
                                + report.overlayBase.manifestDigest + report.overlayBase.declarationName, Is.Empty, file);
                    Assert.That(report.overlayEdits, Is.Empty, file);
                    Assert.That(report.overlayRoots, Is.Empty, file);
                    Assert.That(report.overlayUnmatched, Is.Empty, file);
                    Assert.That(report.overlaySharedAssets, Is.Empty, file);
                }
                var preflight = JsonUtility.FromJson<LegacyPreflight>(File.ReadAllText(result.PreflightPath));
                Assert.That(preflight.selected, Is.EqualTo(new[] { "stable|logical|aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" }));
                Assert.That(preflight.excluded, Is.Empty);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }
    }
}
