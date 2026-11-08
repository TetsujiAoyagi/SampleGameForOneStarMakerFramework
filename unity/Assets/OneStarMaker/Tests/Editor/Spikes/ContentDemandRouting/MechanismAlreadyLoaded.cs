#nullable enable

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Tests.Editor.Spikes.ContentDemandRouting
{
    /// <summary>
    /// 機構 (i): snapshot を Addressables の封じより前に、既ロードの Scene として Play に渡す。
    /// Play の起点は空の `Boot`（U-b1）なので、元の Scene は Play に載らない。Play に入った直後、封じより前に
    /// snapshot を `EditorSceneManager.LoadSceneAsyncInPlayMode`（AssetDatabase 経由）で additive に読み、その後で封じる。
    /// fixture の directory は `RoutingBackend` に 1 つだけ登録し、`Next` などはそこから読む。
    /// snapshot の Scene は現行の既ロード経路（`GetSceneByName` で見つけて `BackendSceneLoaded=false`）で SceneDirector に入り、
    /// unload は `SceneManager.UnloadSceneAsync` に回る。AssetManagement の台帳（所有の (c)）は持たない（HANDOFF §2 所有の成立水準）。
    /// </summary>
    internal static class MechanismAlreadyLoaded
    {
        internal const string Name = "i-already-loaded";

        internal static async UniTask<DirectorHarness> SetUpPlayAsync(HarnessState state, ExperimentRecord record, Cell cell)
        {
            foreach (var snapshot in state.snapshots)
            {
                if (snapshot.refused) continue;
                record.Write(cell, "snapshot.playLoad.started", ("identity", snapshot.identity), ("path", snapshot.snapshotPath),
                    ("route", "EditorSceneManager.LoadSceneAsyncInPlayMode"));
                var operation = EditorSceneManager.LoadSceneAsyncInPlayMode(snapshot.snapshotPath, new LoadSceneParameters(LoadSceneMode.Additive));
                if (operation == null)
                {
                    record.Write(cell, "snapshot.playLoad.failed", ("identity", snapshot.identity), ("reason", "operation-null"));
                    continue;
                }
                // この asmdef の UniTask では AsyncOperation を直接 await できないので、完了を frame ごとに見る。
                while (!operation.isDone) await UniTask.Yield();
                record.Write(cell, "snapshot.playLoad.completed", ("identity", snapshot.identity), ("path", snapshot.snapshotPath),
                    ("scenes", PlayObserver.LoadedScenes()));
            }
            var routing = new RoutingBackend(record, cell);
            routing.Add("fixture", "fixture", state.fixtureDirectory);
            var harness = DirectorHarness.Create(state.mapIdentities, state.SceneGuids(), routing, record, cell);
            harness.Seal();
            await harness.RegisterAlreadyLoadedScenesAsync();
            return harness;
        }
    }
}
