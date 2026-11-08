#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>round の Editor 側の準備と後始末（前の round の残りの検出、Scene setup、snapshot の作成、回収）。</summary>
    internal static partial class RoundRunner
    {
        /// <summary>
        /// round の開始時（harness の最初の処理）に、前の round の物が残っていないかを見る（N4、AC8 の境界）。
        /// 残っていれば回収し、検出と回収を record に残す。
        /// </summary>
        private static void CheckLeftovers(HarnessState state, ExperimentRecord record, Cell cell)
        {
            var unclosed = new List<object?>();
            foreach (var routing in new List<RoutingBackend>(RoutingBackend.Unclosed))
            {
                unclosed.Add(routing.Describe());
                routing.ReclaimFromPreviousRound();
            }
            var snapshotAssets = new List<string>();
            foreach (var path in state.previousSnapshotPaths)
                if (File.Exists(TempRootLease.FullPath(path)) || AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets).Length != 0)
                {
                    snapshotAssets.Add(path);
                    SnapshotWriter.Delete(path);
                }
            var tempDirectories = new List<string>();
            foreach (var path in state.previousTempDirectories)
                if (Directory.Exists(path)) { tempDirectories.Add(path); Directory.Delete(path, true); }
            var buildSettings = new List<string>();
            foreach (var scene in EditorBuildSettings.scenes)
                if (scene.path.StartsWith(TempRootLease.RootAssetPath, StringComparison.Ordinal)) buildSettings.Add(scene.path);
            record.Write(cell, "n4.roundStart", ("unclosedRouting", unclosed), ("snapshotAssetsLeft", snapshotAssets),
                ("tempDirectoriesLeft", tempDirectories), ("buildSettingsSnapshots", buildSettings),
                ("addressablesReferences", AddressablesReferences(state.previousSnapshotGuids)),
                ("adapterGeneratedFolderLeft", AssetDatabase.IsValidFolder("Assets/OneStarMakerGenerated/BS2b")),
                ("startScene", PlayEnvironment.CurrentStartScene()));
            state.previousSnapshotGuids = new List<string>();
            state.previousSnapshotPaths = new List<string>();
            state.previousTempDirectories = new List<string>();
        }

        // Addressables の設定に snapshot の GUID が載っていないか（N4）。Addressables の Editor API はこの asmdef から使えないので、
        // 設定の asset の text を GUID で探す。
        private static List<string> AddressablesReferences(List<string> guids)
        {
            var hits = new List<string>();
            var root = TempRootLease.FullPath("Assets/AddressableAssetsData");
            if (guids.Count == 0 || !Directory.Exists(root)) return hits;
            foreach (var file in Directory.GetFiles(root, "*.asset", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                foreach (var guid in guids) if (guid.Length != 0 && text.Contains(guid)) hits.Add(guid + "@" + file);
            }
            return hits;
        }

        /// <summary>round の Editor の初期状態を作り、snapshot の対象の Scene を返す。</summary>
        private static List<Scene> SetUpEditor(HarnessState state, string scenario)
        {
            var fixture = state.fixture;
            var entry = EditorSceneManager.OpenScene(fixture.entry, OpenSceneMode.Single);
            var targets = new List<Scene> { entry };
            // どの状態でも Entry は Dirty にする（未保存の値を Play の前後で比べるため）。
            MovePayload(entry, state.Round.scenario == Scenarios.Reentry ? 10f + state.Round.play : 0.5f, scenario == Scenarios.Reentry);
            switch (scenario)
            {
                case Scenarios.Main:
                case Scenarios.StaleRemnant:
                    targets.Add(EditorSceneManager.OpenScene(fixture.dup, OpenSceneMode.Additive));
                    break;
                case Scenarios.NewUnsaved:
                    var untitled = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    SceneManager.MoveGameObjectToScene(new GameObject("UntitledPayload"), untitled);
                    targets.Add(untitled);
                    break;
                case Scenarios.AdditiveTwo:
                    var other = EditorSceneManager.OpenScene(fixture.other, OpenSceneMode.Additive);
                    MovePayload(other, 0.25f, false);
                    targets.Add(other);
                    break;
                case Scenarios.PrefabOverride:
                    var instance = FindRoot(entry, "SharedInstance") ?? throw new InvalidOperationException("SharedInstance is missing.");
                    instance.transform.localScale = new Vector3(2f, 2f, 2f);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                    EditorSceneManager.MarkSceneDirty(entry);
                    break;
                case Scenarios.RefPrefabChanged:
                    ChangeSharedPrefab(state);
                    break;
                case Scenarios.OtherInMap:
                case Scenarios.OtherNotInMap:
                case Scenarios.BaselineOtherNoIsolation:
                case Scenarios.BaselineOtherNotInMapNoIsolation:
                    EditorSceneManager.OpenScene(fixture.other, OpenSceneMode.Additive);
                    break;
            }
            EditorSceneManager.SetActiveScene(entry);
            return targets;
        }

        private static List<string> MapIdentities(string scenario)
        {
            var ids = new List<string> { "Entry", "Next", "Dup" };
            if (!Scenarios.OtherOutsideMap(scenario)) ids.Add("Other");
            return ids;
        }

        private static void CreateSnapshots(HarnessState state, TempRootLease lease, List<Scene> targets, ExperimentRecord record, Cell cell)
        {
            var folder = lease.EnsureAssetChild("snap/r" + state.index);
            foreach (var target in targets)
            {
                var marker = "snapshot:r" + state.index + ":" + (target.name.Length == 0 ? "untitled" : target.name);
                var info = SnapshotWriter.Create(target, folder, marker);
                state.snapshots.Add(info);
                if (info.refused)
                {
                    record.Write(cell, "snapshot.refused", ("scene", SceneStateProbe.KeyOf(target, -1)), ("diagnosis", info.refusal));
                    continue;
                }
                if (info.identity == "Entry") state.expectedMarker = marker;
                state.previousSnapshotPaths.Add(info.snapshotPath);
                state.previousSnapshotGuids.Add(info.snapshotGuid);
                record.Write(cell, "snapshot.created", ("identity", info.identity), ("originalPath", info.originalPath),
                    ("originalGuid", info.originalGuid), ("snapshotPath", info.snapshotPath), ("snapshotGuid", info.snapshotGuid),
                    ("guidDiffers", info.snapshotGuid != info.originalGuid), ("sha256", info.sha256), ("marker", info.marker),
                    ("elapsedMs", info.elapsedMs));
            }
        }

        /// <summary>
        /// Editor の Scene を変える前に、type tree の無い directory を build する（AC11）。build できなければ観測が無いので UNTESTED にする。
        /// 一時 directory の欄に持たせ、回収と次の round の残骸の検出は通常の一時 directory と同じ経路を通す。
        /// </summary>
        private static bool BuildNoTypeTreeDirectory(HarnessState state, TempRootLease lease, ExperimentRecord record, Cell cell)
        {
            try
            {
                var identity = "cdr-" + lease.LeaseId + "-r" + state.index + "-no-type-tree";
                var workspace = Path.Combine(lease.DiskPath, "built", "r" + state.index);
                state.tempDirectory = FixtureFactory.BuildDirectoryWithoutTypeTree(identity, workspace, lease.EnsureAssetChild("snap/r" + state.index), "Next", state.fixture.buildNext);
                state.hasTempDirectory = true;
                record.Write(cell, "ac11.built", ("identity", identity), ("path", state.tempDirectory.path), ("summary", state.tempDirectory.summary),
                    ("elapsedMs", state.tempDirectory.elapsedMs), ("options", "DisableWriteTypeTree"));
                return true;
            }
            catch (Exception ex)
            {
                Untested(state, record, cell, "no-type-tree-build", ex.ToString());
                return false;
            }
        }

        private static void SeedAndReclaimStale(HarnessState state, ExperimentRecord record, Cell cell)
        {
            var deadPid = TempRootLease.FindDeadPid();
            var stale = TempRootLease.SeedStaleLease(deadPid);
            var staleScene = stale + "/Entry.unity";
            File.Copy(TempRootLease.FullPath(state.fixture.entry), TempRootLease.FullPath(staleScene));
            OriginMarker.RewriteInFile(TempRootLease.FullPath(staleScene), "stale");
            AssetDatabase.ImportAsset(staleScene, ImportAssetOptions.ForceSynchronousImport);
            state.staleLeasePath = stale;
            var imported = AssetDatabase.AssetPathToGUID(staleScene, AssetPathToGUIDOptions.OnlyExistingAssets).Length != 0;
            var reclaimed = TempRootLease.ReclaimStale();
            record.Write(cell, "ac9b.staleReclaim", ("deadPid", deadPid), ("seeded", stale), ("seededSceneImported", imported),
                ("reclaimed", reclaimed), ("seededFolderLeft", Directory.Exists(TempRootLease.FullPath(stale))),
                ("note", "a real Editor crash is not reproduced; it is out of scope and not a cell"));
        }


        // ───────────────────────── 共通の後始末 ─────────────────────────

        private static void CleanupRound(HarnessState state, ExperimentRecord record, Cell cell)
        {
            var deleted = new List<object?>();
            foreach (var snapshot in state.snapshots)
                if (!snapshot.refused && snapshot.snapshotPath.Length != 0)
                    deleted.Add(snapshot.snapshotPath + "|deleted=" + SnapshotWriter.Delete(snapshot.snapshotPath));
            var lease = state.Lease();
            if (lease != null)
            {
                var folder = lease.AssetPath + "/snap/r" + state.index;
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                var workspace = Path.Combine(lease.DiskPath, "built", "r" + state.index);
                if (Directory.Exists(workspace))
                {
                    try { Directory.Delete(workspace, true); }
                    catch (IOException ex) { deleted.Add(workspace + "|deleteFailed=" + ex.Message); }
                }
                if (Directory.Exists(workspace)) state.previousTempDirectories.Add(workspace);
                deleted.Add(workspace + "|exists=" + Directory.Exists(workspace));
            }
            record.Write(cell, "cleanup.round", ("deleted", deleted));
        }

        private static void ChangeSharedPrefab(HarnessState state)
        {
            var lease = state.Lease() ?? throw new InvalidOperationException("Temp root lease was lost.");
            var backup = Path.Combine(lease.EnsureDiskChild("prefab-backup"), "Shared.prefab");
            File.Copy(TempRootLease.FullPath(state.fixture.prefab), backup, true);
            state.prefabBackupPath = backup;
            state.prefabT0 = JsonUtility.ToJson(SceneStateProbe.CaptureFile(state.fixture.prefab));
            var contents = PrefabUtility.LoadPrefabContents(state.fixture.prefab);
            try
            {
                contents.transform.localScale = new Vector3(3f, 3f, 3f);
                PrefabUtility.SaveAsPrefabAsset(contents, state.fixture.prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static void RestorePrefab(HarnessState state, ExperimentRecord record, Cell cell)
        {
            if (state.prefabBackupPath.Length == 0 || !File.Exists(state.prefabBackupPath)) return;
            File.Copy(state.prefabBackupPath, TempRootLease.FullPath(state.fixture.prefab), true);
            AssetDatabase.ImportAsset(state.fixture.prefab, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            File.Delete(state.prefabBackupPath);
            record.Write(cell, "fixture.prefabRestored", ("path", state.fixture.prefab));
            state.prefabBackupPath = string.Empty;
        }

        private static void Untested(HarnessState state, ExperimentRecord record, Cell cell, string cause, string detail)
        {
            record.Write(cell, "round.untested", ("cause", cause), ("detail", detail));
            state.roundPlays = false;
        }

        private static void OpenBoot(HarnessState state)
        {
            if (state.fixtureReady && File.Exists(TempRootLease.FullPath(state.fixture.boot)))
                EditorSceneManager.OpenScene(state.fixture.boot, OpenSceneMode.Single);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void MovePayload(Scene scene, float x, bool absolute)
        {
            var payload = FindRoot(scene, "Payload") ?? throw new InvalidOperationException("Payload is missing in " + scene.path);
            var position = payload.transform.localPosition;
            payload.transform.localPosition = absolute ? new Vector3(x, position.y, position.z) : position + new Vector3(x, 0f, 0f);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        internal static GameObject? FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects()) if (root != null && root.name == name) return root;
            return null;
        }

        // test の前に利用者が開いていた Scene setup。batch では通常は空。test の終了で戻す。
        private static void SaveInitialSetup()
        {
            var paths = new List<string>();
            foreach (var setup in EditorSceneManager.GetSceneManagerSetup())
                if (setup.path.Length != 0) paths.Add(setup.path + "|" + setup.isLoaded + "|" + setup.isActive);
            SessionState.SetString(InitialSetupKey, string.Join("\n", paths));
        }

        private static void RestoreInitialSetup()
        {
            var saved = SessionState.GetString(InitialSetupKey, string.Empty);
            SessionState.EraseString(InitialSetupKey);
            if (EditorApplication.isPlaying) return;
            var setups = new List<SceneSetup>();
            foreach (var line in saved.Split('\n'))
            {
                var parts = line.Split('|');
                if (parts.Length != 3 || !File.Exists(TempRootLease.FullPath(parts[0]))) continue;
                setups.Add(new SceneSetup { path = parts[0], isLoaded = parts[1] == "True", isActive = parts[2] == "True" });
            }
            if (setups.Exists(x => x.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setups.ToArray());
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
