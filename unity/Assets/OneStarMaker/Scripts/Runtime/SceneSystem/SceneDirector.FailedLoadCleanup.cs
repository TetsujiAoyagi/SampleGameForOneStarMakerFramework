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

                var captured = new Dictionary<string, SceneBase>(newlyCreatedScenes.Count);
                for (var i = 0; i < newlyCreatedScenes.Count; i++)
                {
                    var id = newlyCreatedScenes[i];
                    if (_currentScenes.TryGetValue(id, out var pair))
                    {
                        captured[id] = pair.SceneBase;
                    }
                }

                var activeRoots = new List<string>();
                var loading = new List<string>();

                for (var i = newlyCreatedScenes.Count - 1; i >= 0; i--)
                {
                    var id = newlyCreatedScenes[i];
                    if (!_currentScenes.TryGetValue(id, out var pair)
                        || !captured.TryGetValue(id, out var instance)
                        || !ReferenceEquals(pair.SceneBase, instance))
                    {
                        continue;
                    }

                    if (_inFlightUnloads.TryGetValue(id, out var unloadInFlight))
                    {
                        await ObserveCompletedTask(unloadInFlight.Task);
                        continue;
                    }

                    if (pair.SceneBase.Lifecycle.State == SceneState.Stable)
                    {
                        continue;
                    }

                    if (pair.SceneBase.Lifecycle.IsActive)
                    {
                        var parentId = pair.SceneBase.SceneResource.Parent != null
                            ? pair.SceneBase.SceneResource.Parent.Identity
                            : null;
                        var parentIsNewlyCreatedActive = parentId != null
                            && captured.ContainsKey(parentId)
                            && _currentScenes.TryGetValue(parentId, out var parentPair)
                            && parentPair.SceneBase.Lifecycle.IsActive;
                        if (!parentIsNewlyCreatedActive)
                        {
                            activeRoots.Add(id);
                        }

                        continue;
                    }

                    if (pair.SceneBase.Lifecycle.IsInLoadingPhase)
                    {
                        loading.Add(id);
                        continue;
                    }

                    if (pair.SceneBase.Lifecycle.IsUnloadStarted)
                    {
                        await ResumeUnloadFromCurrentState(id);
                    }
                }

                for (var i = 0; i < activeRoots.Count; i++)
                {
                    await RemoveScene(activeRoots[i]);
                }

                for (var i = 0; i < loading.Count; i++)
                {
                    if (_currentScenes.ContainsKey(loading[i]))
                    {
                        await CleanupCanceledScene(loading[i]);
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
            if (state is SceneState.PreUnloading or SceneState.PreUnloaded)
            {
                if (state == SceneState.PreUnloading)
                {
                    await pair.SceneBase.ExecutePreUnLoad();
                }

                await PhaseUnityUnload(sceneIdentify);
                await PhaseAfterUnloadAndDispose(sceneIdentify);
                return;
            }

            if (state == SceneState.Unloading)
            {
                await PerformUnitySceneUnload(sceneIdentify, pair.AddressablesSceneLoaded);
                if (_currentScenes.TryGetValue(sceneIdentify, out pair)
                    && pair.SceneBase.Lifecycle.State == SceneState.Unloading)
                {
                    TransitionSceneState(sceneIdentify, pair.SceneBase, SceneState.Unloaded);
                }

                await PhaseAfterUnloadAndDispose(sceneIdentify);
                return;
            }

            if (state is SceneState.Unloaded or SceneState.AfterUnloading)
            {
                await PhaseAfterUnloadAndDispose(sceneIdentify);
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
