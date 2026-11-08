#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// 機構 (ii): Play の前に snapshot から一時 Content Directory を build し、fixture の directory と合わせて 2 件を
    /// `RoutingBackend` に登録する。snapshot の directory を先に足し、同じ identity は snapshot を優先して解決する。
    /// Play 中に AssetDatabase で Scene を読まない。snapshot の Scene も directory の load なので、所有の (a)(b)(c) が全て揃うはず。
    /// 現行製品は 2 件目の directory を `AssetManagement.InstallContentDirectory` と `ContentRevisionGate` の 2 か所で拒否するので、
    /// ここでの成立は native 層の成立であり、製品の 2 件 install の証明ではない（HANDOFF §2 判定定義）。
    /// </summary>
    internal static class MechanismTempDirectory
    {
        internal const string Name = "ii-temp-directory";

        /// <summary>AC9 (a) の注入点。"after-build" で true を返すと、build の後に例外を投げる。</summary>
        internal static Func<string, bool>? FailAt;

        /// <summary>Edit で、round の snapshot から一時 directory を build する。失敗した時は作った directory を消してから投げる。</summary>
        internal static BuiltDirectory Build(HarnessState state, TempRootLease lease, ExperimentRecord record, Cell cell)
        {
            var scenes = new List<(string, string)>();
            foreach (var snapshot in state.snapshots)
                if (!snapshot.refused) scenes.Add((snapshot.identity, snapshot.snapshotPath));
            if (scenes.Count == 0) throw new InvalidOperationException("No snapshot is available for the temporary directory.");
            var identity = "cdr-" + lease.LeaseId + "-r" + state.index;
            var workspace = Path.Combine(lease.DiskPath, "built", "r" + state.index);
            try
            {
                var built = FixtureFactory.BuildDirectory(identity, workspace, scenes);
                if (FailAt != null && FailAt("after-build")) throw new InjectedFailureException("temp-directory:after-build");
                record.Write(cell, "tempDirectory.built", ("identity", built.identity), ("path", built.path), ("scenes", built.logicalKeys),
                    ("snapshotCount", scenes.Count), ("elapsedMs", built.elapsedMs), ("summary", built.summary));
                return built;
            }
            catch
            {
                if (Directory.Exists(workspace)) Directory.Delete(workspace, true);
                record.Write(cell, "tempDirectory.cleanedAfterFailure", ("path", workspace), ("exists", Directory.Exists(workspace)));
                throw;
            }
        }

        internal static async UniTask<DirectorHarness> SetUpPlayAsync(HarnessState state, ExperimentRecord record, Cell cell)
        {
            var routing = new RoutingBackend(record, cell);
            routing.Add("snapshot", "snapshot", state.tempDirectory);
            routing.Add("fixture", "fixture", state.fixtureDirectory);
            var harness = DirectorHarness.Create(state.mapIdentities, state.SceneGuids(), routing, record, cell);
            harness.Seal();
            await harness.RegisterAlreadyLoadedScenesAsync();
            return harness;
        }
    }
}
