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
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using S = OneStarMaker.Tests.Editor.Spikes.OverlayBuild.OverlaySpec;

namespace OneStarMaker.Tests.Editor.Spikes.OverlayBuild
{
    /// <summary>
    /// 一時的なスパイク（HANDOFF CONTENT_OVERLAY_BUILD §7「スパイク（F-1 の原因）」）。スパイクの終了時に消す。
    /// SplitBase: fixture v1 → base を build・publish → v2 にして、fixture を残したまま process を終える。
    /// SplitOverlay（別の Editor process）: 観察 1（上書きの build）→ 観察 3（同じ Library で base を Full で再 build）→
    /// BuildPipeline.CleanBuildCache → 観察 2（上書きの再 build）。各出力の file を v1 / v2 の float 列で走査する。
    /// 製品 code は変えない。出力は TestResults/ovb/spike（git 管理外）。
    /// </summary>
    public sealed class OverlayBuildSpike
    {
        private static string Dir => Path.Combine(OverlayRun.RunRoot, "spike");
        private static string LogPath => Path.Combine(Dir, "spike.log");
        private static string StatePath => Path.Combine(Dir, "base-state.txt");

        [Test, Explicit, Timeout(3600000)]
        public void SplitBase()
        {
            if (Directory.Exists(Dir)) Directory.Delete(Dir, true);
            Directory.CreateDirectory(Dir);
            Log("SplitBase start pid=" + System.Diagnostics.Process.GetCurrentProcess().Id + " unity=" + Application.unityVersion);
            OverlayBuildFixture.RemoveLeftovers();
            OverlayArtifacts.Reset();
            var scenes = SceneGuard.Begin();
            var problems = new List<string>();
            try
            {
                new OverlayBuildFixture().CreateV1();
                var v1 = OverlayBuildFixture.SourceHashes();
                LogPoints("points before base", OverlayBuildFixture.ReadPoints());
                LogDependencyHashes("before v2");

                var result = new BuildContentCoordinator().Build(new BuildContentRequest(Plan(), Snapshot(problems), S.BaseSet, OverlayArtifacts.Root,
                    Declaration(), null, BuildContentMode.Full));
                Log("base build success=" + result.IsSuccess + " identity=" + result.Identity + " summary=" + result.Summary);
                if (!result.IsSuccess) throw new InvalidOperationException("base build failed");
                Scan("base(v1)", result.ContentPath!);
                var published = ContentTransportPublisher.Publish(result, Path.Combine(OverlayArtifacts.Root, "transport", result.Identity));
                var manifestPath = Path.Combine(published, "transport.json");
                var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(manifestPath));
                File.WriteAllLines(StatePath, new[] { manifest.contentSet, manifest.revision, manifest.target,
                    S.Sha256(File.ReadAllBytes(manifestPath)), manifest.declaration.name ?? "", result.ContentPath! });

                new OverlayBuildFixture().ApplyV2();
                var v2 = OverlayBuildFixture.SourceHashes();
                Log("changedSources=" + string.Join(",", v2.Where(x => v1[x.Key] != x.Value).Select(x => x.Key).OrderBy(x => x, StringComparer.Ordinal)));
                LogPoints("points after v2", OverlayBuildFixture.ReadPoints());
                LogDependencyHashes("after v2");
            }
            finally
            {
                scenes.End(problems);
                Log("SplitBase end problems=" + string.Join(" | ", problems) + " fixtureKept=" + OverlayBuildFixture.Exists);
            }
        }

        [Test, Explicit, Timeout(3600000)]
        public void SplitOverlay()
        {
            Log("SplitOverlay start pid=" + System.Diagnostics.Process.GetCurrentProcess().Id);
            if (!OverlayBuildFixture.Exists || !File.Exists(StatePath)) throw new InvalidOperationException("SplitBase has not left its fixture/state.");
            var s = File.ReadAllLines(StatePath);
            var baseIdentity = new ContentOverlayBaseIdentity(s[0], s[1], s[2], s[3], s[4].Length == 0 ? null : s[4]);
            var ignoreBefore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true; // 環境由来の Burst の Error ログで test を落とさない（1 回目の実行で発生）
            var scenes = SceneGuard.Begin();
            var problems = new List<string>();
            try
            {
                LogPoints("points in overlay process", OverlayBuildFixture.ReadPoints());
                LogDependencyHashes("overlay process");
                var plan = Plan();
                var coordinator = new BuildContentCoordinator();

                Overlay("obs1 overlay (separate process)", coordinator, plan, baseIdentity, problems);

                var rebuilt = coordinator.Build(new BuildContentRequest(plan, Snapshot(problems), S.BaseSet, OverlayArtifacts.Root, Declaration(), null,
                    BuildContentMode.Full));
                Log("obs3 base rebuild success=" + rebuilt.IsSuccess + " identity=" + rebuilt.Identity + " summary=" + rebuilt.Summary);
                if (rebuilt.IsSuccess) Scan("obs3 base Full rebuild (same Library)", rebuilt.ContentPath!);

                BuildPipeline.CleanBuildCache();
                Log("BuildPipeline.CleanBuildCache called");
                Overlay("obs2 overlay after CleanBuildCache", coordinator, plan, baseIdentity, problems);
            }
            finally
            {
                OverlayBuildFixture.Delete();
                scenes.End(problems);
                LogAssert.ignoreFailingMessages = ignoreBefore;
                Log("SplitOverlay end problems=" + string.Join(" | ", problems) + " fixtureExists=" + OverlayBuildFixture.Exists);
            }
        }

        private static void Overlay(string label, BuildContentCoordinator coordinator, BuildPlan plan, ContentOverlayBaseIdentity baseIdentity, List<string> problems)
        {
            var selection = ContentOverlaySelection.Select(plan, Snapshot(problems), S.Edits);
            Log(label + " roots=" + string.Join(",", selection.Roots.Select(x => x.Root.StableKey)));
            var result = coordinator.Build(new BuildContentRequest(selection, baseIdentity, S.OverlaySet, OverlayArtifacts.Root, Declaration(), null));
            Log(label + " success=" + result.IsSuccess + " identity=" + result.Identity + " summary=" + result.Summary);
            if (result.IsSuccess) Scan(label, result.ContentPath!);
        }

        // 子の位置の float 列（little endian の 3 つ組）。v1 = 1,2,3 / 7,8,9、v2 = 4,5,6 / 10,11,12。
        private static readonly (string name, float[] v)[] Patterns =
        {
            ("Probe.v1(1,2,3)", new[] { 1f, 2f, 3f }), ("Probe.v2(4,5,6)", new[] { 4f, 5f, 6f }),
            ("InnerProbe.v1(7,8,9)", new[] { 7f, 8f, 9f }), ("InnerProbe.v2(10,11,12)", new[] { 10f, 11f, 12f }),
        };

        private static void Scan(string label, string directory)
        {
            var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal).ToList();
            var total = Patterns.ToDictionary(p => p.name, _ => 0);
            foreach (var f in files)
            {
                var bytes = File.ReadAllBytes(f);
                var hits = Patterns.Where(p => Contains(bytes, p.v.SelectMany(x => BitConverter.GetBytes(x)).ToArray())).Select(p => p.name).ToList();
                foreach (var h in hits) total[h]++;
                if (hits.Count != 0) Log("  " + label + " file=" + Path.GetFileName(f) + " sha256=" + S.Sha256(bytes) + " hits=" + string.Join(",", hits));
            }
            Log(label + " files=" + files.Count + " filesContaining: " + string.Join(" ", total.Select(x => x.Key + "=" + x.Value)));
        }

        private static bool Contains(byte[] hay, byte[] needle)
        {
            for (var i = 0; i + needle.Length <= hay.Length; i++)
            {
                var j = 0;
                while (j < needle.Length && hay[i + j] == needle[j]) j++;
                if (j == needle.Length) return true;
            }
            return false;
        }

        private static void LogPoints(string label, IEnumerable<PointValue> points) =>
            Log(label + ": " + string.Join(" ; ", points.Select(p => p.Path + "=" + p.Value)));

        private static void LogDependencyHashes(string label) =>
            Log("dependencyHash " + label + ": " + string.Join(" ", new[] { S.SceneInstance, S.SceneNested, S.SceneDirect }
                .Select(r => r + "=" + AssetDatabase.GetAssetDependencyHash(S.RootPath(r)))));

        private static void Log(string line) =>
            File.AppendAllText(LogPath, DateTime.UtcNow.ToString("o") + " " + line + "\n");

        private static BuildPurposeDeclaration Declaration() =>
            new BuildPurposeDeclaration(S.DeclarationName, S.Roots, new BuildRequest(Array.Empty<BuildTag>()),
                new BuildRepresentationSelection(new[] { "Full" }, BuildRepresentationMode.First));

        private static BuildPlan Plan() =>
            new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), Candidates(), Array.Empty<BuildContentExclusion>());

        private static BuildContentCandidate[] Candidates() => S.Roots.Select(r =>
            new BuildContentCandidate(r, r, OverlayBuildFixture.FixedGuid(S.RootPath(r)), new BuildProvenance("ovb-fixture", r))).ToArray();

        private static BuildMaterializationSnapshot Snapshot(List<string> problems)
        {
            var builder = new AssetDependencySnapshotBuilder(new UnityAssetDatabaseGateway());
            var issues = new List<BuildMaterializationIssue>();
            var closures = Candidates().Select(c => builder.Build(c.PhysicalKey, c.StableKey, issues)).Where(x => x != null).Select(x => x!).ToList();
            if (issues.Count != 0) problems.Add("materialization issues: " + string.Join("; ", issues.Select(x => x.Code + " " + x.Path)));
            return new BuildMaterializationSnapshot(Candidates(), new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), closures);
        }
    }
}
