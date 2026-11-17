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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using S = OneStarMaker.Tests.Editor.Spikes.NoStamp.NstSpec;

namespace OneStarMaker.Tests.Editor.Spikes.NoStamp
{
    /// <summary>
    /// build の process（HANDOFF CONTENT_PREFAB_STAMP_REMOVAL §5 の判定必須 2）。fixture を作り、印を外した製品の coordinator（既定の adapter）で
    /// B1〜B3・B3o・B4・B4b・B5・B6・B5' を build し、record と build-facts.json を書き、fixture を消す。
    /// 通常の回帰に混ぜないので [Explicit]。test が失敗するのは harness 自身の例外の時だけで、build の失敗や期待との違いは
    /// record の値にする。判定は判定の process（<see cref="NoStampVerdictProcess"/>）が record と build 履歴から計算し直す。
    /// </summary>
    public sealed class NoStampBuildProcess
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
            var run = NstRun.Create();
            var facts = new NstBuildFacts { runId = run.RunId, head = run.Head, unity = Application.unityVersion,
                stampDirectoryStart = NstInventory.StampDirectoryState() };
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
                run.Write("fixture", ("leftoverRemoved", NstFixture.RemoveLeftovers()), ("stampDirectoryStart", facts.stampDirectoryStart));
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                NstFixture.Create();
                var coordinator = new BuildContentCoordinator();
                ContentOverlayBaseIdentity? baseIdentity = null;
                foreach (var step in S.Steps)
                {
                    var stepFacts = new NstStepFacts { step = step.Name, overlay = step.Overlay };
                    facts.steps.Add(stepFacts);
                    stepFacts.changedAssets = NstFixture.Apply(step.Versions);
                    stepFacts.writtenAssets = stepFacts.changedAssets.Select(NstInventory.Asset).ToList();
                    if (step.CleanCache)
                    {
                        BuildPipeline.CleanBuildCache();
                        run.Write("clean-build-cache", ("step", step.Name));
                    }
                    var plan = Plan();
                    var snapshot = Snapshot(run, step.Name, facts.problems);
                    stepFacts.assets = S.AllAssets.Select(NstInventory.Asset).ToList();
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
                        request = new BuildContentRequest(selection, baseIdentity, S.OverlaySet, NstRun.ArtifactsRoot, null, null);
                    }
                    else
                    {
                        stepFacts.roots = S.Scenes.ToList();
                        request = new BuildContentRequest(plan, snapshot, S.ContentSet, NstRun.ArtifactsRoot, null, null,
                            step.Full ? BuildContentMode.Full : BuildContentMode.Incremental);
                    }
                    stepFacts.mode = request.Mode.ToString();
                    if (step.OpenUnsaved) OpenUnsaved(stepFacts);
                    // build の直前と coordinator.Build が戻った直後（harness が Refresh・ImportAsset を呼ぶ前）の source。
                    stepFacts.before = S.Scenes.Select(Observe).ToList();
                    var inventoryBefore = NstInventory.Take();
                    stepFacts.inventoryBefore = NstInventory.Write(inventoryBefore, Path.Combine(run.Directory, "inventory-" + step.Name + "-before.txt"));
                    var result = coordinator.Build(request);
                    stepFacts.after = S.Scenes.Select(Observe).ToList();
                    var inventoryAfter = NstInventory.Take();
                    stepFacts.inventoryAfter = NstInventory.Write(inventoryAfter, Path.Combine(run.Directory, "inventory-" + step.Name + "-after.txt"));
                    stepFacts.inventoryDiff = NstInventory.Diff(inventoryBefore, inventoryAfter);
                    stepFacts.generatedFolderAfter = Directory.Exists(Path.Combine(Application.dataPath, "OneStarMakerGenerated"));
                    stepFacts.stampDirectoryAfter = NstInventory.StampDirectoryState();
                    if (step.OpenUnsaved) ObserveAndDiscardUnsaved(stepFacts);
                    stepFacts.identity = result.Identity;
                    stepFacts.success = result.IsSuccess;
                    stepFacts.summary = result.Summary;
                    stepFacts.contentPath = result.ContentPath ?? "";
                    CopyHistory(run, stepFacts);
                    run.Write("build", ("step", step.Name), ("mode", stepFacts.mode), ("overlay", step.Overlay), ("roots", stepFacts.roots),
                        ("changedAssets", stepFacts.changedAssets), ("writtenAssets", stepFacts.writtenAssets.Select(Describe)),
                        ("assets", stepFacts.assets.Select(Describe)), ("identity", result.Identity), ("success", result.IsSuccess),
                        ("summary", result.Summary), ("issues", result.Issues.Select(x => x.Code + "|" + x.Key + "|" + x.Detail)),
                        ("contentPath", stepFacts.contentPath), ("history", stepFacts.historyName),
                        ("before", stepFacts.before.Select(Describe)), ("after", stepFacts.after.Select(Describe)),
                        ("inventoryBefore", stepFacts.inventoryBefore), ("inventoryAfter", stepFacts.inventoryAfter),
                        ("inventoryDiff", stepFacts.inventoryDiff), ("generatedFolderAfter", stepFacts.generatedFolderAfter),
                        ("stampDirectoryAfter", stepFacts.stampDirectoryAfter),
                        ("unsaved", step.OpenUnsaved ? new[] { "opened=" + stepFacts.unsavedOpened, "loadedAfter=" + stepFacts.unsavedLoadedAfter,
                            "dirtyAfter=" + stepFacts.unsavedDirtyAfter, "rootNameAfter=" + stepFacts.unsavedRootNameAfter } : Array.Empty<string>()),
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
                try { NstFixture.Delete(); }
                catch (Exception ex) { facts.problems.Add("fixture delete failed: " + ex.Message); }
                facts.fixtureDeleted = !NstFixture.Exists;
                if (setup.Length != 0 && setup.All(x => File.Exists(NstFixture.Disk(x.path))) && setup.Any(x => x.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                Application.logMessageReceived -= OnLog;
                LogAssert.ignoreFailingMessages = ignoreBefore;
                if (facts.problems.Count != 0 || !facts.fixtureDeleted) facts.finished = false;
                File.WriteAllText(run.FactsPath, JsonUtility.ToJson(facts, true));
                File.WriteAllText(NstRun.BuildPointer, run.RunId);
                run.Write("process-end", ("finished", facts.finished), ("problems", facts.problems), ("fixtureDeleted", facts.fixtureDeleted));
            }
            Assert.That(File.Exists(run.RecordPath), Is.True, "record was not written");
        }

        // B3o: SShared1 を開き、root の名前を変えて未保存の状態にする（file は書かない）。batch の Editor では変更を足しただけでは
        // dirty にならないことがある（S2-2b の判定 C の観察）ので dirty の印を明示に付け、付いたことを確かめる。
        private static void OpenUnsaved(NstStepFacts facts)
        {
            var scene = EditorSceneManager.OpenScene(S.Scene(S.SShared1), OpenSceneMode.Single);
            scene.GetRootGameObjects()[0].name = S.UnsavedName;
            EditorSceneManager.MarkSceneDirty(scene);
            facts.unsavedOpened = scene.isLoaded && scene.isDirty;
        }

        // build の後、変更を捨てる前に、SShared1 が開いたままで未保存の変更が残っているかを記録し、その後に変更を捨てて閉じる。
        private static void ObserveAndDiscardUnsaved(NstStepFacts facts)
        {
            var scene = SceneManager.GetSceneByPath(S.Scene(S.SShared1));
            facts.unsavedLoadedAfter = scene.isLoaded;
            facts.unsavedDirtyAfter = scene.isLoaded && scene.isDirty;
            facts.unsavedRootNameAfter = scene.isLoaded && scene.rootCount > 0 ? scene.GetRootGameObjects()[0].name : "";
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static BuildContentCandidate[] Candidates() => S.Scenes.Select(name =>
            new BuildContentCandidate(name, name, AssetDatabase.AssetPathToGUID(S.Scene(name)), new BuildProvenance("nst-fixture", name))).ToArray();

        private static BuildPlan Plan() =>
            new BuildPlan(new BuildRequest(Array.Empty<BuildTag>()), Candidates(), Array.Empty<BuildContentExclusion>());

        // 製品の AssetDependencySnapshotBuilder で閉包を作る（製品の呼び手と同じく、snapshot と coordinator の間に Refresh を呼ばない）。
        private static BuildMaterializationSnapshot Snapshot(NstRun run, string step, List<string> problems)
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

        private static NstSceneState Observe(string name)
        {
            var path = S.Scene(name);
            var disk = NstFixture.Disk(path);
            var importer = AssetImporter.GetAtPath(path);
            return new NstSceneState
            {
                scene = name,
                fileSha256 = File.Exists(disk) ? S.Sha256(File.ReadAllBytes(disk)) : "missing",
                fileTicks = File.Exists(disk) ? File.GetLastWriteTimeUtc(disk).Ticks : 0,
                metaSha256 = File.Exists(disk + ".meta") ? S.Sha256(File.ReadAllBytes(disk + ".meta")) : "missing",
                metaTicks = File.Exists(disk + ".meta") ? File.GetLastWriteTimeUtc(disk + ".meta").Ticks : 0,
                userData = importer == null ? "missing-importer" : importer.userData,
            };
        }

        private static string Describe(NstSceneState s) =>
            s.scene + " file=" + s.fileSha256 + "@" + s.fileTicks + " meta=" + s.metaSha256 + "@" + s.metaTicks + " userData=" + s.userData;

        private static string Describe(NstAssetState a) => a.path + " sha=" + a.sha256 + " dep=" + a.dependencyHash;

        private static IEnumerable<string> Outputs(string content) => !Directory.Exists(content) ? new[] { "missing" }
            : Directory.GetFiles(content, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal)
                .Select(f => Path.GetRelativePath(content, f).Replace('\\', '/') + " " + S.Sha256(File.ReadAllBytes(f)));

        // build 履歴は Unity の保持の規則で消えうるので、判定の process のために run の下へ写す。
        private static void CopyHistory(NstRun run, NstStepFacts step)
        {
            var source = NstHistory.Find(step.identity);
            if (source == null) return;
            step.historyName = Path.GetFileName(source);
            step.historyCopy = Path.Combine(run.Directory, "history", step.step);
            Directory.CreateDirectory(step.historyCopy);
            foreach (var file in NstHistory.Files)
                if (File.Exists(Path.Combine(source, file))) File.Copy(Path.Combine(source, file), Path.Combine(step.historyCopy, file), true);
        }

        // B5 を publish し、transport manifest から上書きの base の識別を作る（呼び手が、登録する base から読む形）。
        private static ContentOverlayBaseIdentity Publish(NstRun run, BuildContentResult result)
        {
            var published = ContentTransportPublisher.Publish(result, Path.Combine(NstRun.ArtifactsRoot, "transport", result.Identity));
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
            const string marker = "nst-artifacts.marker";
            var root = NstRun.ArtifactsRoot;
            if (Directory.Exists(root))
            {
                if (!File.Exists(Path.Combine(root, marker))) throw new InvalidOperationException(root + " exists without the harness marker; not touching it.");
                Directory.Delete(root, true);
            }
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, marker), "nst");
        }
    }
}
