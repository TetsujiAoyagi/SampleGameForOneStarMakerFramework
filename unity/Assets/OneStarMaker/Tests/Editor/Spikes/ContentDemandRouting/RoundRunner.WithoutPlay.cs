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
    /// <summary>Play を伴わない観測（AC2 の saveAsCopy 単独の対照、AC9 (a) の注入、AC10 の予約の衝突）。</summary>
    internal static partial class RoundRunner
    {
        // ───────────────────────── Play を伴わない観測 ─────────────────────────

        private static void RunWithoutPlay(HarnessState state, ExperimentRecord record, Cell cell)
        {
            switch (state.Round.scenario)
            {
                case Scenarios.LeaseConflict: LeaseConflict(record, cell); break;
                case Scenarios.OsmGateConstraint: GateConstraint.Observe(state, record, cell); break;
                case Scenarios.SaveAsCopyControl: SaveAsCopyControl(state, record, cell); break;
                case Scenarios.FailSnapshot:
                case Scenarios.FailBuild:
                    InjectedFailure(state, record, cell); break;
            }
            record.Write(cell, "round.completedWithoutPlay", ("playEntered", false));
        }

        private static void LeaseConflict(ExperimentRecord record, Cell cell)
        {
            var first = TempRootLease.Acquire();
            try
            {
                string outcome;
                try { TempRootLease.Acquire(first.LeaseId); outcome = "second-reservation-accepted"; }
                catch (LeaseConflictException ex) { outcome = "second-reservation-rejected:" + ex.OwnerPid; }
                var firstLock = Path.Combine(first.DiskPath, "lease.json");
                record.Write(cell, "ac10.leaseConflict", ("outcome", outcome), ("firstFolderKept", AssetDatabase.IsValidFolder(first.AssetPath)),
                    ("firstLockKept", File.Exists(firstLock)), ("firstStillOwned", TempRootLease.Attach(first.LeaseId, first.Token) != null),
                    ("note", "in-process simulation only; two real Editor processes are not observed"));
            }
            finally { first.Release(); }
        }

        private static void InjectedFailure(HarnessState state, ExperimentRecord record, Cell cell)
        {
            if (!state.fixtureReady) { Untested(state, record, cell, "harness", "fixture unavailable"); return; }
            var lease = state.Lease() ?? throw new InvalidOperationException("Temp root lease was lost.");
            var build = state.Round.scenario == Scenarios.FailBuild;
            if (build && !state.fixtureDirectoryReady) { Untested(state, record, cell, "fixture-directory", "build target unavailable"); return; }
            var targets = SetUpEditor(state, Scenarios.Main);
            var t0 = SceneStateProbe.CaptureSetup();
            string failure = "none";
            try
            {
                if (build) MechanismTempDirectory.FailAt = stage => stage == "after-build";
                else SnapshotWriter.FailAt = stage => stage == "after-marker";
                CreateSnapshots(state, lease, targets, record, cell);
                if (build)
                {
                    state.tempDirectory = MechanismTempDirectory.Build(state, lease, record, cell);
                    state.hasTempDirectory = true;
                }
            }
            catch (InjectedFailureException ex) { failure = ex.Stage; }
            finally
            {
                SnapshotWriter.FailAt = null;
                MechanismTempDirectory.FailAt = null;
            }
            // 失敗した時は、それまでに作った snapshot と一時 directory を全て消し、Play に入らない（fail-closed）。
            CleanupRound(state, record, cell);
            var snapFolder = TempRootLease.FullPath(lease.AssetPath + "/snap/r" + state.index);
            var leftovers = Directory.Exists(snapFolder) ? Directory.GetFiles(snapFolder, "*.unity").Length : 0;
            var workspace = Path.Combine(lease.DiskPath, "built", "r" + state.index);
            var t1 = SceneStateProbe.CaptureSetup();
            record.Write(cell, "ac9a.result", ("failedAt", failure), ("snapshotFilesLeft", leftovers), ("tempDirectoryLeft", Directory.Exists(workspace)),
                ("playEntered", false), ("originalDifferences", SceneStateProbe.Compare(t0, t1)));
            OpenBoot(state);
        }

        private static void SaveAsCopyControl(HarnessState state, ExperimentRecord record, Cell cell)
        {
            if (!state.fixtureReady) { Untested(state, record, cell, "harness", "fixture unavailable"); return; }
            var lease = state.Lease() ?? throw new InvalidOperationException("Temp root lease was lost.");
            var states = new[] { Scenarios.Main, Scenarios.NewUnsaved, Scenarios.AdditiveTwo, Scenarios.PrefabOverride, Scenarios.RefPrefabChanged };
            for (var i = 0; i < states.Length; i++)
            {
                state.ResetRound();
                var controlCell = Cell.Of(cell.mechanism, cell.epm, cell.scenario, i);
                try
                {
                    var targets = SetUpEditor(state, states[i]);
                    var t0 = SceneStateProbe.CaptureSetup();
                    var folder = lease.EnsureAssetChild("snap/control" + i);
                    var created = new List<object?>();
                    foreach (var target in targets)
                    {
                        var info = SnapshotWriter.Create(target, folder, "control:" + i);
                        created.Add(info.refused ? "refused:" + info.refusal : info.snapshotPath + "|" + info.snapshotGuid);
                        if (!info.refused) state.snapshots.Add(info);
                    }
                    var t1 = SceneStateProbe.CaptureSetup();
                    record.Write(controlCell, "ac2.saveAsCopyControl", ("state", states[i]), ("snapshots", created),
                        ("differences", SceneStateProbe.Compare(t0, t1)), ("note", "saveAsCopy alone, without entering Play"));
                }
                catch (Exception ex)
                {
                    record.Write(controlCell, "ac2.saveAsCopyControl.failed", ("state", states[i]), ("error", ex));
                    state.harnessErrors.Add(cell.scenario + "#" + i + ": " + ex.Message);
                }
                finally
                {
                    CleanupRound(state, record, controlCell);
                    RestorePrefab(state, record, controlCell);
                    OpenBoot(state);
                }
            }
            state.ResetRound();
        }
    }
}
