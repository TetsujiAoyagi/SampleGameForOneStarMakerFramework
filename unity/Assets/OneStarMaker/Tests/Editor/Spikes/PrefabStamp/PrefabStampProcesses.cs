#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using OneStarMaker.Build.Selection;
using OneStarMaker.Editor.Build.Content;
using OneStarMaker.Editor.Build.Materialization;
using OneStarMaker.Runtime.BuildContent.Distribution;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using S = OneStarMaker.Tests.Editor.Spikes.PrefabStamp.PstSpec;

namespace OneStarMaker.Tests.Editor.Spikes.PrefabStamp
{
    /// <summary>
    /// build の process（HANDOFF CONTENT_PREFAB_STAMP §5 の判定必須 2）。fixture を作り、製品の coordinator（既定の adapter と
    /// 既定の印の記録の置き場）で B1〜B6 と B5' を build し、record と build-facts.json を書き、fixture を消す。
    /// 通常の回帰に混ぜないので [Explicit]。test が失敗するのは harness 自身の例外の時だけで、build の失敗や期待との違いは
    /// record の値にする。判定は判定の process（<see cref="PrefabStampVerdictProcess"/>）が record と build 履歴から計算し直す。
    /// </summary>
    public sealed class PrefabStampBuildProcess
    {
        [Test, Explicit, Timeout(3600000)]
        public void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 ||
                EditorUserBuildSettings.standaloneBuildSubtarget != StandaloneBuildSubtarget.Player)
                throw new InvalidOperationException("The harness requires the active StandaloneWindows64 Player target.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; the harness does not replace it.");
            var run = PstRun.Create();
            var facts = new PstBuildFacts { runId = run.RunId, head = run.Head };
            var setup = EditorSceneManager.GetSceneManagerSetup();
            // build の Error の log を test の失敗にすると途中で打ち切られ、finally（fixture の削除）が走らない（S2-1 の実測）。
            var ignoreBefore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            void OnLog(string message, string stack, LogType type) =>
                run.Write("unity-log", ("logType", type.ToString()), ("message", message));
            Application.logMessageReceived += OnLog;
            try
            {
                ResetArtifacts();
                run.Write("fixture", ("leftoverRemoved", PstFixture.RemoveLeftovers()));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PstFixture.Create();
                var coordinator = new BuildContentCoordinator();
                ContentOverlayBaseIdentity? baseIdentity = null;
                foreach (var step in S.Steps)
                {
                    var stepFacts = new PstStepFacts { step = step.Name, overlay = step.Overlay };
                    facts.steps.Add(stepFacts);
                    stepFacts.changedPrefabs = PstFixture.Apply(step.Versions);
                    if (step.CleanCache)
                    {
                        BuildPipeline.CleanBuildCache();
                        run.Write("clean-build-cache", ("step", step.Name));
                    }
                    var plan = Plan();
                    var snapshot = Snapshot(run, step.Name, facts.problems);
                    BuildContentRequest request;
                    if (step.Overlay)
                    {
                        var selection = ContentOverlaySelection.Select(plan, snapshot, S.OverlayEdits);
                        run.Write("selection", ("step", step.Name), ("required", selection.IsOverlayRequired),
                            ("roots", selection.Roots.Select(x => x.Root.StableKey + "=" + string.Join(";", x.Edits))));
                        if (!selection.IsOverlayRequired || baseIdentity == null)
                        {
                            facts.problems.Add(step.Name + ": no overlay selection or base");
                            break;
                        }
                        stepFacts.roots = selection.Roots.Select(x => x.Root.StableKey).ToList();
                        request = new BuildContentRequest(selection, baseIdentity, S.OverlaySet, PstRun.ArtifactsRoot, null, null);
                    }
                    else
                    {
                        stepFacts.roots = S.Scenes.ToList();
                        request = new BuildContentRequest(plan, snapshot, S.ContentSet, PstRun.ArtifactsRoot, null, null,
                            step.Full ? BuildContentMode.Full : BuildContentMode.Incremental);
                    }
                    stepFacts.mode = request.Mode.ToString();
                    // build の直前と coordinator.Build が戻った直後（harness が Refresh・ImportAsset を呼ぶ前）の source。
                    stepFacts.before = S.Scenes.Select(Observe).ToList();
                    var inventoryBefore = PstInventory.Take();
                    stepFacts.inventoryBefore = PstInventory.Write(inventoryBefore, Path.Combine(run.Directory, "inventory-" + step.Name + "-before.txt"));
                    var result = coordinator.Build(request);
                    stepFacts.after = S.Scenes.Select(Observe).ToList();
                    var inventoryAfter = PstInventory.Take();
                    stepFacts.inventoryAfter = PstInventory.Write(inventoryAfter, Path.Combine(run.Directory, "inventory-" + step.Name + "-after.txt"));
                    stepFacts.inventoryDiff = PstInventory.Diff(inventoryBefore, inventoryAfter);
                    stepFacts.generatedFolderAfter = Directory.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated"));
                    stepFacts.identity = result.Identity;
                    stepFacts.success = result.IsSuccess;
                    stepFacts.summary = result.Summary;
                    stepFacts.contentPath = result.ContentPath ?? "";
                    CopyHistory(run, stepFacts);
                    run.Write("build", ("step", step.Name), ("mode", stepFacts.mode), ("overlay", step.Overlay), ("roots", stepFacts.roots),
                        ("changedPrefabs", stepFacts.changedPrefabs), ("identity", result.Identity), ("success", result.IsSuccess),
                        ("summary", result.Summary), ("issues", result.Issues.Select(x => x.Code + "|" + x.Key + "|" + x.Detail)),
                        ("contentPath", stepFacts.contentPath), ("history", stepFacts.historyName),
                        ("before", stepFacts.before.Select(Describe)), ("after", stepFacts.after.Select(Describe)),
                        ("inventoryBefore", stepFacts.inventoryBefore), ("inventoryAfter", stepFacts.inventoryAfter),
                        ("inventoryDiff", stepFacts.inventoryDiff), ("generatedFolderAfter", stepFacts.generatedFolderAfter),
                        ("outputs", Outputs(stepFacts.contentPath)));
                    if (!result.IsSuccess)
                    {
                        facts.problems.Add(step.Name + " failed: " + result.Summary);
                        break;
                    }
                    if (step.Name == "B5") baseIdentity = Publish(run, result);
                }
                facts.finished = facts.problems.Count == 0 && facts.steps.Count == S.Steps.Length;
            }
            catch (Exception ex)
            {
                facts.problems.Add("harness exception: " + ex);
                throw;
            }
            finally
            {
                try { PstFixture.Delete(); }
                catch (Exception ex) { facts.problems.Add("fixture delete failed: " + ex.Message); }
                facts.fixtureDeleted = !PstFixture.Exists;
                if (setup.Length != 0 && setup.All(x => File.Exists(PstFixture.Disk(x.path))) && setup.Any(x => x.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Application.logMessageReceived -= OnLog;
                LogAssert.ignoreFailingMessages = ignoreBefore;
                if (facts.problems.Count != 0 || !facts.fixtureDeleted) facts.finished = false;
                File.WriteAllText(run.FactsPath, JsonUtility.ToJson(facts, true));
                File.WriteAllText(PstRun.BuildPointer, run.RunId);
                run.Write("process-end", ("finished", facts.finished), ("problems", facts.problems), ("fixtureDeleted", facts.fixtureDeleted));
            }
            Assert.That(File.Exists(run.RecordPath), Is.True, "record was not written");
        }

        private static BuildContentCandidate[] Candidates() => S.Scenes.Select(name =>
            new BuildContentCandidate(name, name, AssetDatabase.AssetPathToGUID(S.Scene(name)), new BuildProvenance("pst-fixture", name))).ToArray();

        private static BuildPlan Plan() =>
            new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), Candidates(), Array.Empty<BuildContentExclusion>());

        // 製品の AssetDependencySnapshotBuilder で閉包を作る（製品の呼び手と同じく、snapshot と coordinator の間に Refresh を呼ばない）。
        private static BuildMaterializationSnapshot Snapshot(PstRun run, string step, List<string> problems)
        {
            var builder = new AssetDependencySnapshotBuilder(new UnityAssetDatabaseGateway());
            var issues = new List<BuildMaterializationIssue>();
            var closures = new List<BuildDependencySnapshot>();
            foreach (var c in Candidates())
            {
                var closure = builder.Build(c.PhysicalKey, c.StableKey, issues);
                if (closure != null) closures.Add(closure);
                run.Write("closure", ("step", step), ("root", c.StableKey), ("entries", closure?.Entries.Select(x => x.Path)));
            }
            if (issues.Count != 0) problems.Add("materialization issues (" + step + "): " + string.Join("; ", issues.Select(x => x.Code + " " + x.Path)));
            return new BuildMaterializationSnapshot(Candidates(), new Dictionary<string, IReadOnlyList<BuildTag>>(),
                Array.Empty<BuildContentRequirement>(), closures);
        }

        private static PstSceneState Observe(string name)
        {
            var path = S.Scene(name);
            var disk = PstFixture.Disk(path);
            var importer = AssetImporter.GetAtPath(path);
            return new PstSceneState
            {
                scene = name,
                fileSha256 = File.Exists(disk) ? S.Sha256(File.ReadAllBytes(disk)) : "missing",
                fileTicks = File.Exists(disk) ? File.GetLastWriteTimeUtc(disk).Ticks : 0,
                metaSha256 = File.Exists(disk + ".meta") ? S.Sha256(File.ReadAllBytes(disk + ".meta")) : "missing",
                metaTicks = File.Exists(disk + ".meta") ? File.GetLastWriteTimeUtc(disk + ".meta").Ticks : 0,
                userData = importer == null ? "missing-importer" : importer.userData,
            };
        }

        private static string Describe(PstSceneState s) =>
            s.scene + " file=" + s.fileSha256 + "@" + s.fileTicks + " meta=" + s.metaSha256 + "@" + s.metaTicks + " userData=" + s.userData;

        private static IEnumerable<string> Outputs(string content) => !Directory.Exists(content) ? new[] { "missing" }
            : Directory.GetFiles(content, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal)
                .Select(f => Path.GetRelativePath(content, f).Replace('\\', '/') + " " + S.Sha256(File.ReadAllBytes(f)));

        // build 履歴は Unity の保持の規則で消えうるので、判定の process のために run の下へ写す。
        private static void CopyHistory(PstRun run, PstStepFacts step)
        {
            var source = PstHistory.Find(step.identity);
            if (source == null) return;
            step.historyName = Path.GetFileName(source);
            step.historyCopy = Path.Combine(run.Directory, "history", step.step);
            Directory.CreateDirectory(step.historyCopy);
            foreach (var file in PstHistory.Files)
                if (File.Exists(Path.Combine(source, file))) File.Copy(Path.Combine(source, file), Path.Combine(step.historyCopy, file), true);
        }

        // B5 を publish し、transport manifest から上書きの base の識別を作る（呼び手が、登録する base から読む形）。
        private static ContentOverlayBaseIdentity Publish(PstRun run, BuildContentResult result)
        {
            var published = ContentTransportPublisher.Publish(result, Path.Combine(PstRun.ArtifactsRoot, "transport", result.Identity));
            var manifestPath = Path.Combine(published, "transport.json");
            var manifest = JsonUtility.FromJson<ContentTransportManifest>(File.ReadAllText(manifestPath));
            var digest = S.Sha256(File.ReadAllBytes(manifestPath));
            run.Write("base-published", ("directory", published), ("contentSet", manifest.contentSet), ("revision", manifest.revision),
                ("target", manifest.target), ("manifestSha256", digest));
            return new ContentOverlayBaseIdentity(manifest.contentSet, manifest.revision, manifest.target, digest,
                string.IsNullOrEmpty(manifest.declaration.name) ? null : manifest.declaration.name);
        }

        private static void ResetArtifacts()
        {
            const string marker = "pst-artifacts.marker";
            var root = PstRun.ArtifactsRoot;
            if (Directory.Exists(root))
            {
                if (!File.Exists(Path.Combine(root, marker))) throw new InvalidOperationException(root + " exists without the harness marker; not touching it.");
                Directory.Delete(root, true);
            }
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, marker), "pst");
        }
    }

    /// <summary>
    /// 判定の process（HANDOFF CONTENT_PREFAB_STAMP §5 の判定必須 3）。build の process の build-facts.json と、その process が写した
    /// build 履歴（`Library/BuildHistory` に残っていれば同じ bytes かも記録する）と出力から、AC1〜AC6 を計算し直して record に書く。
    /// test が失敗するのは harness 自身の例外の時だけ。AC の合否は record の ac の行で、判定 C が読む。
    /// </summary>
    public sealed class PrefabStampVerdictProcess
    {
        private sealed class Observed
        {
            public readonly Dictionary<string, string> SceneFile = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> SceneSha = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, HashSet<string>> Triples = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            public readonly HashSet<string> ScenesWritten = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Outputs = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly List<string> StampInOutputs = new List<string>();
        }

        [Test, Explicit]
        public void Verdict()
        {
            var run = PstRun.Open("verdict");
            var facts = JsonUtility.FromJson<PstBuildFacts>(File.ReadAllText(run.FactsPath));
            run.Write("build-facts", ("finished", facts.finished), ("problems", facts.problems), ("steps", facts.steps.Select(x => x.step)));
            var observed = new Dictionary<string, Observed>(StringComparer.Ordinal);
            foreach (var step in facts.steps.Where(x => x.success))
            {
                var o = Observe(run, step);
                observed[step.step] = o;
                run.Write("step-observed", ("step", step.step), ("mode", step.mode),
                    ("sceneFiles", o.SceneFile.Select(x => x.Key + "=" + x.Value + " " + o.SceneSha[x.Key])),
                    ("triples", o.Triples.Select(x => x.Key + "=" + string.Join(";", x.Value.OrderBy(t => t, StringComparer.Ordinal)))),
                    ("scenesWritten", o.ScenesWritten.OrderBy(x => x, StringComparer.Ordinal)), ("stampInOutputs", o.StampInOutputs));
            }
            var complete = facts.finished && S.Steps.All(x => observed.ContainsKey(x.Name));
            Ac1(run, observed);
            Ac2(run, facts, observed);
            Ac3(run, observed);
            Ac4(run, facts);
            Ac5(run, facts);
            Ac6(run, observed);
            run.Write("process-end", ("finished", true), ("buildProcessFinished", facts.finished), ("allStepsObserved", complete));
            Assert.That(File.Exists(run.RecordPath), Is.True, "record was not written");
        }

        private static Observed Observe(PstRun run, PstStepFacts step)
        {
            var o = new Observed();
            var layoutPath = Path.Combine(step.historyCopy, "ContentLayout.json");
            var tepPath = Path.Combine(step.historyCopy, "BuildContentTEP.json");
            var library = string.IsNullOrEmpty(step.historyName) ? null : Path.Combine(PstHistory.Root, step.historyName);
            run.Write("history", ("step", step.step), ("copy", step.historyCopy), ("library", library),
                ("libraryMatchesCopy", library != null && PstHistory.Files.All(f =>
                    File.Exists(Path.Combine(library, f)) && File.Exists(Path.Combine(step.historyCopy, f)) &&
                    File.ReadAllBytes(Path.Combine(library, f)).SequenceEqual(File.ReadAllBytes(Path.Combine(step.historyCopy, f))))));
            if (!File.Exists(layoutPath) || !File.Exists(tepPath)) return o;
            var layout = JsonUtility.FromJson<PstLayout>(File.ReadAllText(layoutPath));
            var written = PstHistory.WrittenFileIds(File.ReadAllText(tepPath));
            foreach (var file in Directory.GetFiles(step.contentPath, "*", SearchOption.AllDirectories))
            {
                var bytes = File.ReadAllBytes(file);
                var name = Path.GetRelativePath(step.contentPath, file).Replace('\\', '/');
                o.Outputs[name] = S.Sha256(bytes);
                if (IndexOf(bytes, Encoding.ASCII.GetBytes(ScenePrefabStampRules.Prefix)) >= 0) o.StampInOutputs.Add(name);
            }
            foreach (var name in S.Scenes)
            {
                var id = layout.LoadableSceneIds.FirstOrDefault(x => x.Path == S.Scene(name));
                if (id == null) continue;
                var serialized = layout.SerializedFiles.FirstOrDefault(x => x.Index == id.SerializedFile);
                if (serialized == null) continue;
                if (written.Contains(serialized.ID)) o.ScenesWritten.Add(name);
                var cf = Path.Combine(step.contentPath, serialized.ContentHash + ".cf");
                o.SceneFile[name] = serialized.ContentHash + ".cf";
                var bytes = File.Exists(cf) ? File.ReadAllBytes(cf) : Array.Empty<byte>();
                o.SceneSha[name] = File.Exists(cf) ? S.Sha256(bytes) : "missing";
                o.Triples[name] = new HashSet<string>(S.Prefabs.SelectMany(p => new[] { S.V1, S.V2 }.Select(v => (p, v)))
                    .Where(x => IndexOf(bytes, S.FloatBytes(S.Position(x.p, x.v))) >= 0).Select(x => x.p + "." + x.v), StringComparer.Ordinal);
            }
            return o;
        }

        // AC1: 各回の出力に含まれる各 Scene の .cf が、その回の最終の版の列を全て持ち、前の版（もう一方の版）の列を持たない。
        private static void Ac1(PstRun run, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            foreach (var step in S.Steps)
            {
                if (!observed.TryGetValue(step.Name, out var o)) { failures.Add(step.Name + " not observed"); continue; }
                foreach (var scene in o.Triples.Keys)
                    foreach (var prefab in S.BakedPrefabs(scene))
                    {
                        var current = step.Versions[prefab];
                        var other = current == S.V1 ? S.V2 : S.V1;
                        if (!o.Triples[scene].Contains(prefab + "." + current)) failures.Add(step.Name + " " + scene + " lacks " + prefab + "." + current);
                        if (o.Triples[scene].Contains(prefab + "." + other)) failures.Add(step.Name + " " + scene + " has " + prefab + "." + other);
                    }
                if (!step.Overlay && o.Triples.Count != S.Scenes.Length) failures.Add(step.Name + " has " + o.Triples.Count + " scenes");
            }
            Ac(run, "AC1", failures);
        }

        // AC2: 上書き（B6）の SOuter が (Outer v1, Inner v2) の列を持ち、Outer v2 を持たず、file が B2 の SOuter と同じ sha256。
        private static void Ac2(PstRun run, PstBuildFacts facts, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            var b6 = facts.steps.FirstOrDefault(x => x.step == "B6");
            if (b6 == null || !observed.TryGetValue("B6", out var o) || !observed.TryGetValue("B2", out var b2)) { Ac(run, "AC2", new List<string> { "B2 or B6 not observed" }); return; }
            if (!b6.roots.SequenceEqual(new[] { S.SOuter })) failures.Add("B6 roots " + string.Join(";", b6.roots));
            if (!o.Triples.TryGetValue(S.SOuter, out var triples)) failures.Add("B6 has no SOuter");
            else
            {
                if (!triples.Contains("Outer.v1") || !triples.Contains("Inner.v2")) failures.Add("B6 SOuter lacks Outer.v1/Inner.v2");
                if (triples.Contains("Outer.v2")) failures.Add("B6 SOuter has Outer.v2");
                if (o.SceneSha[S.SOuter] != b2.SceneSha.GetValueOrDefault(S.SOuter)) failures.Add("B6 SOuter sha differs from B2");
            }
            Ac(run, "AC2", failures);
        }

        // AC3: Incremental の回だけを数える。B3 は Scene を 1 つも書かず、B2 は SOuter だけを書き、B4 の各 Scene が B1 と同じ sha256。
        private static void Ac3(PstRun run, Dictionary<string, Observed> observed)
        {
            var failures = new List<string>();
            if (!observed.TryGetValue("B1", out var b1) || !observed.TryGetValue("B2", out var b2) ||
                !observed.TryGetValue("B3", out var b3) || !observed.TryGetValue("B4", out var b4))
            { Ac(run, "AC3", new List<string> { "B1..B4 not observed" }); return; }
            if (b3.ScenesWritten.Count != 0) failures.Add("B3 wrote " + string.Join(";", b3.ScenesWritten));
            if (!b2.ScenesWritten.SetEquals(new[] { S.SOuter })) failures.Add("B2 wrote " + string.Join(";", b2.ScenesWritten));
            foreach (var scene in S.Scenes)
                if (b4.SceneSha.GetValueOrDefault(scene) != b1.SceneSha.GetValueOrDefault(scene)) failures.Add("B4 " + scene + " differs from B1");
            Ac(run, "AC3", failures, ("b4Written", string.Join(";", b4.ScenesWritten)));
        }

        // AC4: 対照の SPlain の file と .meta は、全ての build の前後で sha256 と更新時刻が変わらない。
        private static void Ac4(PstRun run, PstBuildFacts facts)
        {
            var states = facts.steps.SelectMany(x => x.before.Concat(x.after)).Where(x => x.scene == S.SPlain).ToList();
            var failures = new List<string>();
            if (states.Count == 0) failures.Add("no SPlain state");
            else if (states.Select(x => x.fileSha256 + x.fileTicks + x.metaSha256 + x.metaTicks).Distinct().Count() != 1) failures.Add("SPlain changed");
            Ac(run, "AC4", failures);
        }

        // AC5: 各 build の直後に、各 Scene の .meta の sha256 と userData が build の前と一致し、Scene の file が変わらず、
        // Assets/ と ProjectSettings/ の全 file が前と一致し、生成 folder が残っていない。
        private static void Ac5(PstRun run, PstBuildFacts facts)
        {
            var failures = new List<string>();
            foreach (var step in facts.steps)
            {
                foreach (var before in step.before)
                {
                    var after = step.after.FirstOrDefault(x => x.scene == before.scene);
                    if (after == null) { failures.Add(step.step + " " + before.scene + " missing after"); continue; }
                    if (after.metaSha256 != before.metaSha256) failures.Add(step.step + " " + before.scene + " .meta changed");
                    if (after.userData != before.userData) failures.Add(step.step + " " + before.scene + " userData " + after.userData);
                    if (after.fileSha256 != before.fileSha256 || after.fileTicks != before.fileTicks) failures.Add(step.step + " " + before.scene + " file changed");
                }
                if (step.inventoryDiff.Count != 0) failures.Add(step.step + " inventory: " + string.Join(";", step.inventoryDiff));
                if (step.generatedFolderAfter) failures.Add(step.step + " generated folder remains");
            }
            if (facts.steps.Count != S.Steps.Length) failures.Add("steps " + facts.steps.Count);
            Ac(run, "AC5", failures);
        }

        // AC6: 全ての出力に印の接頭辞が無い。B5 と B5'（CleanBuildCache の後）の各 Scene の file が同じ sha256。Scene 以外の差は記録だけ。
        private static void Ac6(PstRun run, Dictionary<string, Observed> observed)
        {
            var failures = observed.SelectMany(x => x.Value.StampInOutputs.Select(f => x.Key + " " + f + " has the stamp prefix")).ToList();
            if (!observed.TryGetValue("B5", out var b5) || !observed.TryGetValue("B5p", out var b5p)) failures.Add("B5 or B5p not observed");
            else
            {
                foreach (var scene in S.Scenes)
                    if (b5.SceneSha.GetValueOrDefault(scene) != b5p.SceneSha.GetValueOrDefault(scene)) failures.Add(scene + " differs between B5 and B5p");
                var sceneFiles = new HashSet<string>(b5.SceneFile.Values.Concat(b5p.SceneFile.Values), StringComparer.Ordinal);
                var others = b5.Outputs.Keys.Union(b5p.Outputs.Keys).Where(x => !sceneFiles.Contains(x))
                    .Where(x => b5.Outputs.GetValueOrDefault(x) != b5p.Outputs.GetValueOrDefault(x)).OrderBy(x => x, StringComparer.Ordinal);
                run.Write("ac6-non-scene-differences", ("files", others));
            }
            Ac(run, "AC6", failures);
        }

        private static void Ac(PstRun run, string ac, List<string> failures, params (string key, object? value)[] extra) =>
            run.Write("ac", new (string key, object? value)[] { ("ac", ac), ("pass", failures.Count == 0), ("failures", failures) }
                .Concat(extra).ToArray());

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            if (needle.Length == 0 || haystack.Length < needle.Length) return -1;
            for (var i = 0; i <= haystack.Length - needle.Length; i++)
            {
                var j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j]) j++;
                if (j == needle.Length) return i;
            }
            return -1;
        }
    }
}
