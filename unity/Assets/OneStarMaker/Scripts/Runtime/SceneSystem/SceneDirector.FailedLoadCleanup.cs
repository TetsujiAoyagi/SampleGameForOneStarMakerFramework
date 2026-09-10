#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace OneStarMaker.Runtime.SceneSystem
{
    // ─── Failed load recovery ───
    // 通常例外で失敗した Add が作った newlyCreated 個体だけを回収する。
    // 公開 API / SceneState 値 / SceneEventType は増やさない。

    public partial class SceneDirector
    {
        /// <summary>
        /// この Add が作った個体を到達状態に応じて回収する。
        /// 二次例外はログし、呼び出し元の元例外を隠さない。
        /// </summary>
        private async UniTask RecoverFailedLoadAsync(List<string> newlyCreatedScenes)
        {
            try
            {
                await AwaitRemainingInFlightLoads(newlyCreatedScenes);

                var created = CaptureCreatedInstances(newlyCreatedScenes);
                var activeRoots = new List<string>();
                var stillLoading = new List<string>();

                // 子から親へ。cancel cleanup と同じ。
                for (var i = newlyCreatedScenes.Count - 1; i >= 0; i--)
                {
                    var id = newlyCreatedScenes[i];
                    if (!TryGetCapturedPair(id, created, out var pair))
                    {
                        continue;
                    }

                    if (_inFlightUnloads.TryGetValue(id, out var unloadInFlight))
                    {
                        await ObserveCompletedTask(unloadInFlight.Task);
                        continue;
                    }

                    var lifecycle = pair.SceneBase.Lifecycle;
                    if (lifecycle.IsUnloadStarted)
                    {
                        await ResumeUnloadFromCurrentState(id);
                        continue;
                    }

                    ClassifyFailedScene(id, pair, created, activeRoots, stillLoading);
                }

                for (var i = 0; i < activeRoots.Count; i++)
                {
                    await RemoveScene(activeRoots[i]);
                }

                for (var i = 0; i < stillLoading.Count; i++)
                {
                    var id = stillLoading[i];
                    if (_currentScenes.ContainsKey(id))
                    {
                        await CleanupCanceledScene(id);
                    }
                }

                for (var i = 0; i < newlyCreatedScenes.Count; i++)
                {
                    _pendingUnloads.Remove(newlyCreatedScenes[i]);
                }
            }
            catch (Exception cleanupEx)
            {
                Debug.LogException(cleanupEx);
            }
        }

        private Dictionary<string, SceneBase> CaptureCreatedInstances(List<string> newlyCreatedScenes)
        {
            var created = new Dictionary<string, SceneBase>(newlyCreatedScenes.Count);
            for (var i = 0; i < newlyCreatedScenes.Count; i++)
            {
                var id = newlyCreatedScenes[i];
                if (_currentScenes.TryGetValue(id, out var pair))
                {
                    created[id] = pair.SceneBase;
                }
            }

            return created;
        }

        private bool TryGetCapturedPair(
            string id,
            Dictionary<string, SceneBase> created,
            out ScenePair pair)
        {
            if (!_currentScenes.TryGetValue(id, out pair!))
            {
                return false;
            }

            return created.TryGetValue(id, out var instance)
                   && ReferenceEquals(pair.SceneBase, instance);
        }

        private void ClassifyFailedScene(
            string id,
            ScenePair pair,
            Dictionary<string, SceneBase> created,
            List<string> activeRoots,
            List<string> stillLoading)
        {
            var lifecycle = pair.SceneBase.Lifecycle;

            // Add 完了後の TransitionPlan 失敗などはロード失敗ではない。
            if (lifecycle.State == SceneState.Stable)
            {
                return;
            }

            // Initializing。親もこの Add の active 個体なら RemoveScene(親) に任せる。
            if (lifecycle.IsActive)
            {
                if (IsRootAmongCreatedActive(pair, created))
                {
                    activeRoots.Add(id);
                }

                return;
            }

            if (lifecycle.IsInLoadingPhase)
            {
                stillLoading.Add(id);
            }
        }

        private bool IsRootAmongCreatedActive(ScenePair pair, Dictionary<string, SceneBase> created)
        {
            var parent = pair.SceneBase.SceneResource.Parent;
            if (parent == null)
            {
                return true;
            }

            if (!created.ContainsKey(parent.Identity))
            {
                return true;
            }

            if (!_currentScenes.TryGetValue(parent.Identity, out var parentPair))
            {
                return true;
            }

            return !parentPair.SceneBase.Lifecycle.IsActive;
        }

        private async UniTask AwaitRemainingInFlightLoads(List<string> identities)
        {
            var tasks = new List<UniTask>(identities.Count * 2);
            for (var i = 0; i < identities.Count; i++)
            {
                var id = identities[i];
                if (_inFlightSceneBaseLoads.TryGetValue(id, out var preLoad))
                {
                    tasks.Add(ObserveCompletedTask(preLoad.Task));
                }

                if (_inFlightUnitySceneLoads.TryGetValue(id, out var unityLoad))
                {
                    tasks.Add(ObserveCompletedTask(unityLoad.Task));
                }
            }

            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

        private async UniTask ResumeUnloadFromCurrentState(string sceneIdentify)
        {
            if (!_currentScenes.TryGetValue(sceneIdentify, out var pair))
            {
                return;
            }

            var state = pair.SceneBase.Lifecycle.State;
            switch (state)
            {
                case SceneState.PreUnloading:
                    await pair.SceneBase.ExecutePreUnLoad();
                    await PhaseUnityUnload(sceneIdentify);
                    await PhaseAfterUnloadAndDispose(sceneIdentify);
                    break;
                case SceneState.PreUnloaded:
                    await PhaseUnityUnload(sceneIdentify);
                    await PhaseAfterUnloadAndDispose(sceneIdentify);
                    break;
                case SceneState.Unloading:
                    await PerformUnitySceneUnload(sceneIdentify, pair.AddressablesSceneLoaded);
                    if (_currentScenes.TryGetValue(sceneIdentify, out pair)
                        && pair.SceneBase.Lifecycle.State == SceneState.Unloading)
                    {
                        TransitionSceneState(sceneIdentify, pair.SceneBase, SceneState.Unloaded);
                    }

                    await PhaseAfterUnloadAndDispose(sceneIdentify);
                    break;
                case SceneState.Unloaded:
                case SceneState.AfterUnloading:
                    await PhaseAfterUnloadAndDispose(sceneIdentify);
                    break;
            }
        }

        private static async UniTask AwaitAllSettled(UniTask[] tasks, int taskCount)
        {
            Exception? first = null;
            for (var i = 0; i < taskCount; i++)
            {
                try
                {
                    await tasks[i];
                }
                catch (Exception ex)
                {
                    first ??= ex;
                }
            }

            if (first != null)
            {
                ExceptionDispatchInfo.Capture(first).Throw();
            }
        }

        private static async UniTask ObserveCompletedTask(UniTask task)
        {
            try
            {
                await task;
            }
            catch
            {
                // 合流観測のみ。例外は先発側が保持する。
            }
        }
    }
}
