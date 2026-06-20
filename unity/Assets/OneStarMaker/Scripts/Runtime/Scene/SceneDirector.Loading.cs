#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetDescriptions;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.SceneSystem
{
    // ─── Loading ───
    // AddScene, LoadSceneBase, LoadUnityScene, IncrementalLoadAsync, PerformUnitySceneLoad

    public partial class SceneDirector
    {
        /// <summary>
        /// シーンを追加する。必要に応じて親シーンも再帰的にロードする。
        /// キャンセルは SceneBase PreLoad フェーズのみ有効（キャンセル窓）。
        /// Unity Scene ロード開始以降はキャンセル不可（ポイント・オブ・ノーリターン）。
        /// </summary>
        /// <param name="sceneIdentify">シーンの一意識別子。</param>
        /// <param name="afterOnLoadedTask">ロード後に追加実行するタスク。</param>
        /// <param name="ct">キャンセルトークン（PreLoad フェーズのみ有効）。</param>
        /// <param name="context">遷移先シーンに渡す型付きコンテキスト（Shared Context）。</param>
        /// <param name="progress">進捗通知。IsCancelable で窓の状態を確認可能。</param>
        /// <param name="loadingDisplay">ローディング表示モード。</param>
        /// <param name="telemetryTags">テレメトリスパンに付与する追加タグ。</param>
        public async UniTask AddScene(
            string sceneIdentify,
            Func<UniTask>? afterOnLoadedTask,
            CancellationToken ct,
            SceneContext? context = null,
            IProgress<SceneLoadProgress>? progress = null,
            LoadingDisplayType loadingDisplay = LoadingDisplayType.None,
            IReadOnlyDictionary<string, string>? telemetryTags = null)
        {
            // 追加の文字列 tag はここでは組み立てない。
            // シーンロードは実行頻度こそ高くないが、transport へ載せる本命は
            // StartType / Metadata / TagBits なので、ヒープ文字列を増やさない。
            var span = AppTelemetry.StartSpan(Foundation.Core.TelemetryStartType.SceneLoad, null);
            var success = false;
            var memBefore = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();

            // 既にロード中・ロード済みならスキップ。
            // PreLoad 中も「同じシーンを二重に起こさない」ことを優先する。
            if (_currentScenes.TryGetValue(sceneIdentify, out var existing)
                && (existing.SceneBase.Lifecycle.IsLoadedOrActive
                    || existing.SceneBase.Lifecycle.IsInLoadingPhase))
            {
                AppTelemetry.FinishSpan(span, default, true, TelemetryLevel.Verbose, null);
                return;
            }

            // アンロード進行中のシーンは、辞書から除去されてから再ロードする。
            // 中途半端な lifecycle を跨ぐより待機した方が整合性を保ちやすい。
            while (_currentScenes.TryGetValue(sceneIdentify, out var unloading)
                && (unloading.SceneBase.Lifecycle.IsUnloadStarted
                    || unloading.SceneBase.Lifecycle.IsLoadCanceled))
            {
                await UniTask.Yield();
            }

            CancellationTokenSource? linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var cancelableCt = linkedCts.Token;

            // キャンセル時に巻き戻す対象だけを追跡する。
            // シーン数は大きくなりにくいため、小さめ capacity で十分。
            var newlyCreatedScenes = new List<string>(4);
            var parentList = new List<SceneResource>(4);
            var showedLoading = loadingDisplay != LoadingDisplayType.None;

            if (showedLoading)
            {
                await _loadingDisplay.Show(loadingDisplay, ct);
            }

            try
            {
                progress?.Report(new SceneLoadProgress(
                    SceneLoadPhase.PreLoadStarted, true, sceneIdentify));

                var targetSceneResource = _sceneResourceMap.GetSceneResource(sceneIdentify)
                    ?? throw new InvalidOperationException($"SceneResource not found: {sceneIdentify}");

                if (targetSceneResource.Parent != null)
                {
                    CollectNecessaryScenes(targetSceneResource.Parent, parentList);
                }

                for (var index = 0; index < parentList.Count; index++)
                {
                    await LoadSceneBase(parentList[index].Identity, cancelableCt, isLoadChildren: false, newlyCreatedScenes);
                }

                await LoadSceneBase(sceneIdentify, cancelableCt, isLoadChildren: true, newlyCreatedScenes);

                // 共有 context はターゲットシーンだけに入れる。
                // 親シーンへ流すと既存状態を不用意に汚すため、明示的に対象を絞る。
                if (context != null && _currentScenes.TryGetValue(sceneIdentify, out var target))
                {
                    target.SceneBase.SetContext(context);
                }

                for (var index = 0; index < newlyCreatedScenes.Count; index++)
                {
                    var createdSceneId = newlyCreatedScenes[index];
                    if (_currentScenes.TryGetValue(createdSceneId, out var created))
                    {
                        created.LoadCts = linkedCts;
                    }
                }

                progress?.Report(new SceneLoadProgress(
                    SceneLoadPhase.PreLoadCompleted, true, sceneIdentify));

                // ★ ポイント・オブ・ノーリターン ★
                // ここを越えたら Unity Scene ロードに入り、外部キャンセルは受けない。
                for (var index = 0; index < newlyCreatedScenes.Count; index++)
                {
                    var createdSceneId = newlyCreatedScenes[index];
                    if (_currentScenes.TryGetValue(createdSceneId, out var created))
                    {
                        created.LoadCts = null;
                    }
                }

                linkedCts.Dispose();
                linkedCts = null;

                progress?.Report(new SceneLoadProgress(
                    SceneLoadPhase.UnitySceneLoading, false, sceneIdentify));

                for (var index = 0; index < parentList.Count; index++)
                {
                    var parentSceneId = parentList[index].Identity;
                    var parentBase = _currentScenes[parentSceneId].SceneBase;
                    await LoadUnityScene(parentSceneId, parentBase, CancellationToken.None, isLoadChildScene: false);
                }

                await LoadUnityScene(sceneIdentify, _currentScenes[sceneIdentify].SceneBase, CancellationToken.None, isLoadChildScene: true);

                if (afterOnLoadedTask != null)
                {
                    await afterOnLoadedTask();
                }

                progress?.Report(new SceneLoadProgress(
                    SceneLoadPhase.Completed, false, sceneIdentify));

                if (showedLoading)
                {
                    await _loadingDisplay.Hide(CancellationToken.None);
                    showedLoading = false;
                }

                // Stable 到達後に保留アンロードがあれば即実行する。
                if (_pendingUnloads.Remove(sceneIdentify))
                {
                    await RemoveScene(sceneIdentify);
                }

                // Stable 到達後に transition plan があれば、ここで一度だけ解釈する。
                if (_currentScenes.TryGetValue(sceneIdentify, out var stableScene))
                {
                    var plan = stableScene.SceneBase.CreateTransitionPlan();
                    if (plan != null)
                    {
                        await ExecuteTransitionPlan(sceneIdentify, plan);
                    }
                }

                success = true;
            }
            catch (OperationCanceledException)
            {
                for (var index = 0; index < newlyCreatedScenes.Count; index++)
                {
                    var createdSceneId = newlyCreatedScenes[index];
                    if (_currentScenes.TryGetValue(createdSceneId, out var created))
                    {
                        created.LoadCts = null;
                    }
                }

                linkedCts?.Dispose();
                linkedCts = null;

                // Stable 未達のシーンだけを逆順で掃除する。
                // 子から親へ戻すため reverse order にしている。
                for (var index = newlyCreatedScenes.Count - 1; index >= 0; index--)
                {
                    var createdSceneId = newlyCreatedScenes[index];
                    if (_currentScenes.TryGetValue(createdSceneId, out var created)
                        && !created.SceneBase.Lifecycle.IsActive)
                    {
                        await CleanupCanceledScene(createdSceneId);
                    }
                }

                for (var index = 0; index < newlyCreatedScenes.Count; index++)
                {
                    _pendingUnloads.Remove(newlyCreatedScenes[index]);
                }

                if (ct.IsCancellationRequested)
                {
                    throw;
                }
            }
            finally
            {
                if (showedLoading)
                {
                    await _loadingDisplay.Hide(CancellationToken.None);
                }

                linkedCts?.Dispose();

                var memAfter = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();
                var tags = RuntimeTelemetryMetadataFactory.ClassifyMemoryDelta(memBefore, memAfter, AppTelemetry.Thresholds);
                if (tags.HasValue)
                {
                    AppTelemetry.NotifyBottleneck(
                        ZString.Format(
                            "[\u26a0 Scene] AddScene memory managed={0:F1}MB native={1:F1}MB {2}",
                            RuntimeTelemetryMetadataFactory.GetManagedDeltaMb(memBefore, memAfter),
                            RuntimeTelemetryMetadataFactory.GetNativeDeltaMb(memBefore, memAfter),
                            sceneIdentify));
                }

                var metadata = RuntimeTelemetryMetadataFactory.CreateMemoryMetadata(memAfter);

                AppTelemetry.FinishSpan(span, metadata, success, TelemetryLevel.Summary, tags);
            }
        }

        // ─── Private: Scene loading ───

        /// <summary>
        /// SceneBase のインスタンスを生成し、PreLoad を実行する。
        /// キャンセル時のクリーンアップは AddScene のトップレベル catch で一括処理する。
        /// </summary>
        private async UniTask LoadSceneBase(
            string sceneIdentify,
            CancellationToken ct,
            bool isLoadChildren,
            List<string> newlyCreatedScenes)
        {
            if (!_currentScenes.ContainsKey(sceneIdentify))
            {
                var sceneResource = _sceneResourceMap.GetSceneResource(sceneIdentify)
                    ?? throw new InvalidOperationException($"SceneResource not found: {sceneIdentify}");

                var newInstance = _sceneFactory.CreateSceneClass(sceneResource, this)
                    ?? throw new InvalidOperationException($"SceneFactory returned null for: {sceneIdentify}");

                var pair = new ScenePair(newInstance);
                _currentScenes.Add(sceneIdentify, pair);
                newlyCreatedScenes.Add(sceneIdentify);

                _sceneEventSubject.OnNext(new SceneEvent(
                    SceneEventType.StateChanged, sceneIdentify, SceneState.PreLoading));
                await newInstance.ExecutePreLoad(ct);
                _sceneEventSubject.OnNext(new SceneEvent(
                    SceneEventType.StateChanged, sceneIdentify, SceneState.PreLoaded,
                    newInstance.Lifecycle.LastPhaseElapsedMs));

                if (isLoadChildren)
                {
                    // Children.Count で確保。OnDemand 分の未使用スロットは default(UniTask)=即完了で無害
                    var tasks = new UniTask[sceneResource.Children.Count];
                    var taskCount = 0;
                    foreach (var child in sceneResource.Children)
                    {
                        if (child.LoadType != LoadType.OnDemand)
                        {
                            tasks[taskCount++] = LoadSceneBase(child.Identity, ct, isLoadChildren, newlyCreatedScenes);
                        }
                    }
                    await UniTask.WhenAll(tasks);
                }
            }
            else
            {
                var sceneBase = _currentScenes[sceneIdentify].SceneBase;
                if (sceneBase.Lifecycle.IsNone)
                {
                    await sceneBase.ExecutePreLoad(ct);
                }
            }
        }

        /// <summary>
        /// Unity Scene を Addressables でロードし、SceneBase を初期化する。
        /// ポイント・オブ・ノーリターン通過後に呼ばれるため、キャンセルは発生しない。
        /// </summary>
        private async UniTask LoadUnityScene(
            string sceneIdentify,
            SceneBase sceneBase,
            CancellationToken ct,
            bool isLoadChildScene)
        {
            // 既に Stable に到達済みの親シーンはスキップ
            if (sceneBase.Lifecycle.IsActive)
            {
                return;
            }

            // PreLoaded → Loading: Addressable ロード開始
            TransitionSceneState(sceneIdentify, sceneBase, SceneState.Loading);

            var (handle, rootObjects) = await PerformUnitySceneLoad(sceneIdentify, sceneBase.SceneResource);
            _currentScenes[sceneIdentify].Handle = handle;

            // Loading → Loaded → WaitLoadChildScene
            TransitionSceneState(sceneIdentify, sceneBase, SceneState.Loaded);
            TransitionSceneState(sceneIdentify, sceneBase, SceneState.WaitLoadChildScene);

            // 子シーンのロード
            if (isLoadChildScene && sceneBase.SceneResource.Children.Count > 0)
            {
                // Children.Count で確保。OnDemand/Incremental 分の未使用スロットは default(UniTask)=即完了で無害
                var necessaryTasks = new UniTask[sceneBase.SceneResource.Children.Count];
                var taskCount = 0;

                foreach (var child in sceneBase.SceneResource.Children)
                {
                    if (child.LoadType == LoadType.OnDemand)
                    {
                        continue;
                    }

                    var childBase = _currentScenes[child.Identity].SceneBase;

                    if (child.LoadType == LoadType.NecessaryAlways)
                    {
                        necessaryTasks[taskCount++] =
                            LoadUnityScene(child.Identity, childBase, ct, isLoadChildScene);
                    }
                    else // IncrementalAlways
                    {
                        IncrementalLoadAsync(child.Identity, childBase, ct, isLoadChildScene).Forget();
                    }
                }

                await UniTask.WhenAll(necessaryTasks);
            }

            // RootObjects を取得して SceneBase を初期化
            sceneBase.Initialize(rootObjects);

            // ロード後処理（OnLoadedImpl を呼ぶ。状態遷移はここでは行わない）
            await sceneBase.ExecuteLoaded(ct);

            // WaitLoadChildScene → Initializing: UIView の ViewIn
            TransitionSceneState(sceneIdentify, sceneBase, SceneState.Initializing);
            if (sceneBase.UIView != null)
            {
                await _uiCommon.AddUIView(sceneIdentify, sceneBase.UIView, ct);
            }

            // Initializing → Stable
            TransitionSceneState(sceneIdentify, sceneBase, SceneState.Stable);
            _sceneEventSubject.OnNext(new SceneEvent(
                SceneEventType.Added, sceneIdentify, SceneState.Stable,
                sceneBase.Lifecycle.LastPhaseElapsedMs));
        }

        /// <summary>
        /// IncrementalAlways シーンの非同期ロード。例外を握りつぶしてログに出力する。
        /// </summary>
        private async UniTaskVoid IncrementalLoadAsync(
            string childIdentify,
            SceneBase childBase,
            CancellationToken ct,
            bool isLoadChildScene)
        {
            try
            {
                await LoadUnityScene(childIdentify, childBase, ct, isLoadChildScene);
            }
            catch (OperationCanceledException)
            {
                // キャンセルは無視（親のキャンセル処理で一括クリーンアップされる）
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SceneDirector] Incremental load failed: {childIdentify}: {ex}");
            }
        }

        // ─── Protected: Unity Scene Load（テスト時にオーバーライド可能） ───

        /// <summary>
        /// Unity Scene をロードし、RootGameObjects を返す。
        /// テスト時にオーバーライドして Addressables / SceneManager 依存を排除する。
        /// </summary>
        protected virtual async UniTask<(AsyncOperationHandle<SceneInstance>? Handle, GameObject[] RootObjects)>
            PerformUnitySceneLoad(string sceneIdentify, SceneResource sceneResource)
        {
            var unityScene = SceneManager.GetSceneByName(sceneIdentify);
            if (!unityScene.IsValid() || !unityScene.isLoaded)
            {
                var handle = sceneResource.Load();
                if (handle == null)
                {
                    throw new InvalidOperationException($"SceneResource.Load() returned null: {sceneIdentify}");
                }

                await handle.Value.Task;
                return (handle, handle.Value.Result.Scene.GetRootGameObjects());
            }

            return (null, unityScene.GetRootGameObjects());
        }
    }
}
