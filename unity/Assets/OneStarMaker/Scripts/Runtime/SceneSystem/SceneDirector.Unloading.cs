#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime;
using OneStarMaker.Runtime.AssetManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneStarMaker.Runtime.SceneSystem
{
    // ─── Unloading ───
    // UnloadScene, RemoveScene, 3-Phase (PhasePreUnload / PhaseUnityUnload / PhaseAfterUnloadAndDispose),
    // CollectLoadedDescendants, CleanupCanceledScene, PerformUnitySceneUnload

    public partial class SceneDirector
    {
        /// <summary>
        /// シーンをアンロードする。子シーンも再帰的にアンロードされる。
        /// アンロード処理はキャンセル不可。開始したら必ず最終状態まで到達する。
        /// ロード中のシーンに対しては、キャンセル窓内ならキャンセル、窓外なら Stable 到達を待ってからアンロードする。
        /// </summary>
        /// <param name="sceneIdentify">シーンの一意識別子。</param>
        /// <param name="loadingDisplay">ローディング表示モード。</param>
        /// <param name="telemetryTags">テレメトリスパンに付与する追加タグ。</param>
        /// <param name="telemetryLevel">UnloadScene スパンのテレメトリ出力レベル。</param>
        public async UniTask UnloadScene(
            string sceneIdentify,
            LoadingDisplayType loadingDisplay = LoadingDisplayType.None,
            IReadOnlyDictionary<string, string>? telemetryTags = null,
            TelemetryLevel telemetryLevel = TelemetryLevel.Summary)
        {
            if (!_currentScenes.TryGetValue(sceneIdentify, out var pair))
            {
                return;
            }

            // 既にアンロード開始済み or ペンディング登録済みならスキップ
            if (pair.SceneBase.Lifecycle.IsUnloadStarted || _pendingUnloads.Contains(sceneIdentify))
            {
                return;
            }

            // ロード中のシーン: キャンセル窓内ならキャンセル、窓外なら Stable まで待つ
            if (pair.SceneBase.Lifecycle.IsInLoadingPhase)
            {
                if (pair.LoadCts != null)
                {
                    pair.LoadCts.Cancel();
                    return;
                }

                _pendingUnloads.Add(sceneIdentify);
                return;
            }

            var span = AppTelemetry.StartSpan(Foundation.Core.TelemetryStartType.SceneUnload, null);
            var success = false;
            var memBefore = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();

            try
            {
                var showedLoading = loadingDisplay != LoadingDisplayType.None;
                if (showedLoading)
                {
                    await _loadingDisplay.Show(loadingDisplay, CancellationToken.None);
                }

                await RemoveScene(sceneIdentify);

                if (showedLoading)
                {
                    await _loadingDisplay.Hide(CancellationToken.None);
                }

                success = true;
            }
            finally
            {
                var memAfter = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();
                var tags = RuntimeTelemetryMetadataFactory.ClassifyMemoryDelta(memBefore, memAfter, AppTelemetry.Thresholds);
                if (tags.HasValue)
                {
                    AppTelemetry.NotifyBottleneck(
                        ZString.Format(
                            "[\u26a0 Scene] UnloadScene memory managed={0:F1}MB native={1:F1}MB {2}",
                            RuntimeTelemetryMetadataFactory.GetManagedDeltaMb(memBefore, memAfter),
                            RuntimeTelemetryMetadataFactory.GetNativeDeltaMb(memBefore, memAfter),
                            sceneIdentify));
                }

                var metadata = RuntimeTelemetryMetadataFactory.CreateMemoryMetadata(memAfter);

                AppTelemetry.FinishSpan(span, metadata, success, telemetryLevel, tags);
            }
        }

        // ─── Private: Scene unloading (3-phase) ───
        // sibling 間参照を許容するため、アンロードを3フェーズに分離する。
        //   Phase 1: 全子孫の ViewOut + PreUnload（全 Unity Scene がまだ残っている）
        //   Phase 2: 全子孫の Unity Scene アンロード
        //   Phase 3: 全子孫の AfterUnload + Dispose
        //   最後に自分自身を同じ順序で処理する。

        /// <summary>
        /// 通常のシーンアンロード。子シーンも再帰的にアンロードする。
        /// sibling 間参照を保証するため、3フェーズで処理する。
        /// </summary>
        private async UniTask RemoveScene(string sceneIdentify)
        {
            if (!_currentScenes.TryGetValue(sceneIdentify, out var pair))
            {
                return;
            }

            var sceneBase = pair.SceneBase;

            // 既にアンロード開始済みならスキップ
            if (sceneBase.Lifecycle.IsUnloadStarted)
            {
                return;
            }

            // RemoveScene は Stable または Initializing からのみ呼ばれることを保証
            if (!sceneBase.Lifecycle.IsActive)
            {
                throw new InvalidOperationException(
                    $"Cannot unload scene that is not active: {sceneIdentify} (state: {sceneBase.Lifecycle.State})");
            }

            // 実際にロード済みの子孫を収集（葉が先の post-order）
            var descendants = new List<string>();
            CollectLoadedDescendants(sceneIdentify, descendants);

            // 1つの List を Clear() しながら3フェーズで再利用する
            var phaseTasks = new List<UniTask>(descendants.Count);

            // ── Phase 1: 全子孫の ViewOut + PreUnload ──
            // この時点で全 Unity Scene がまだ存在するため、sibling 間参照が有効。
            foreach (var desc in descendants)
            {
                phaseTasks.Add(PhasePreUnload(desc));
            }
            await UniTask.WhenAll(phaseTasks);

            // ── Phase 2: 全子孫の Unity Scene アンロード ──
            phaseTasks.Clear();
            foreach (var desc in descendants)
            {
                phaseTasks.Add(PhaseUnityUnload(desc));
            }
            await UniTask.WhenAll(phaseTasks);

            // ── Phase 3: 全子孫の AfterUnload + Dispose ──
            phaseTasks.Clear();
            foreach (var desc in descendants)
            {
                phaseTasks.Add(PhaseAfterUnloadAndDispose(desc));
            }
            await UniTask.WhenAll(phaseTasks);

            // ── Self: 自分自身のフルライフサイクル ──
            await PhasePreUnload(sceneIdentify);
            await PhaseUnityUnload(sceneIdentify);
            await PhaseAfterUnloadAndDispose(sceneIdentify);
        }

        /// <summary>
        /// Phase 1: ViewOut + PreUnload を実行する。
        /// 全 Unity Scene がまだ存在するフェーズで呼ばれる。
        /// </summary>
        private async UniTask PhasePreUnload(string sceneIdentify)
        {
            if (!_currentScenes.TryGetValue(sceneIdentify, out var pair))
            {
                return;
            }

            if (pair.SceneBase.Lifecycle.IsUnloadStarted)
            {
                return;
            }

            _sceneEventSubject.OnNext(new SceneEvent(
                SceneEventType.StateChanged, sceneIdentify, SceneState.PreUnloading));

            // UIView の ViewOut（SceneDirector が仲介者として UICommon を呼ぶ）
            await _uiCommon.RemoveUIView(sceneIdentify);

            // Stable → PreUnloading → PreUnloaded
            await pair.SceneBase.ExecutePreUnLoad();
            _sceneEventSubject.OnNext(new SceneEvent(
                SceneEventType.StateChanged, sceneIdentify, SceneState.PreUnloaded,
                pair.SceneBase.Lifecycle.LastPhaseElapsedMs));
        }

        /// <summary>
        /// Phase 2: Unity Scene をアンロードする。
        /// PreUnloaded → Unloading → Unloaded。
        /// </summary>
        private async UniTask PhaseUnityUnload(string sceneIdentify)
        {
            if (!_currentScenes.TryGetValue(sceneIdentify, out var pair))
            {
                return;
            }

            TransitionSceneState(sceneIdentify, pair.SceneBase, SceneState.Unloading);
            await PerformUnitySceneUnload(sceneIdentify, pair.AddressablesSceneLoaded);
            TransitionSceneState(sceneIdentify, pair.SceneBase, SceneState.Unloaded);
        }

        /// <summary>
        /// Phase 3: AfterUnload + 所有アセット Release + Dispose + 辞書除去。
        /// この時点では Phase 2 で Scene 本体は既に Unload 済みなので、
        /// <see cref="IAssetManagement.ReleaseScene"/> は所有アセット解放のみを行う（backend Unload はしない）。
        /// </summary>
        private async UniTask PhaseAfterUnloadAndDispose(string sceneIdentify)
        {
            if (!_currentScenes.TryGetValue(sceneIdentify, out var pair))
            {
                return;
            }

            await pair.SceneBase.ExecuteAfterUnLoad();
            // Phase 3: Scene 所有の PreLoad アセット等を解放（Scene 本体は Phase 2 済み）
            _assetManagement.ReleaseScene(sceneIdentify);
            pair.SceneBase.Dispose();
            _currentScenes.Remove(sceneIdentify);
            _sceneEventSubject.OnNext(new SceneEvent(
                SceneEventType.Removed, sceneIdentify, SceneState.AfterUnloading,
                pair.SceneBase.Lifecycle.LastPhaseElapsedMs));
        }

        /// <summary>
        /// 指定シーンの実際にロード済みの子孫を post-order（葉が先）で収集する。
        /// SceneResource.Children の定義上の全子ではなく、_currentScenes に存在するもののみ。
        /// </summary>
        private void CollectLoadedDescendants(string sceneIdentify, List<string> result)
        {
            if (!_currentScenes.TryGetValue(sceneIdentify, out var pair))
            {
                return;
            }

            foreach (var child in pair.SceneBase.SceneResource.Children)
            {
                if (!_currentScenes.TryGetValue(child.Identity, out var childPair))
                {
                    continue;
                }

                if (childPair.SceneBase.Lifecycle.IsActive)
                {
                    CollectLoadedDescendants(child.Identity, result);
                    result.Add(child.Identity);
                    continue;
                }

                // Incremental child がまだ loading 中なら、親アンロードに合わせて
                // 「安定化後に即アンロード」または「キャンセル可能ならキャンセル」へ倒す。
                if (childPair.SceneBase.Lifecycle.IsInLoadingPhase)
                {
                    if (childPair.LoadCts != null)
                    {
                        childPair.LoadCts.Cancel();
                    }
                    else
                    {
                        _pendingUnloads.Add(child.Identity);
                    }
                }
            }
        }

        /// <summary>
        /// キャンセル時のクリーンアップ。通常の RemoveScene とは分離する。
        /// CancellationToken.None で実行し、クリーンアップ自体がキャンセルされないようにする。
        /// PreLoad で確保したリソースを AfterUnload フックで解放するため、
        /// LoadCanceled → AfterUnloading の遷移を経由する。
        /// </summary>
        private async UniTask CleanupCanceledScene(string sceneIdentify)
        {
            if (!_currentScenes.TryGetValue(sceneIdentify, out var pair))
            {
                return;
            }

            var activeDescendants = new List<string>();
            var inactiveChildren = new List<string>();

            // 子シーンを分類する。
            // 既に active へ到達した subtree は sibling-safe な 3-phase でまとめて落とし、
            // まだ active でない subtree だけを cancel cleanup で再帰処理する。
            foreach (var child in pair.SceneBase.SceneResource.Children)
            {
                if (!_currentScenes.TryGetValue(child.Identity, out var childPair))
                {
                    continue;
                }

                if (childPair.SceneBase.Lifecycle.IsActive)
                {
                    CollectLoadedDescendants(child.Identity, activeDescendants);
                    activeDescendants.Add(child.Identity);
                    continue;
                }

                inactiveChildren.Add(child.Identity);
            }

            if (activeDescendants.Count > 0)
            {
                await RunThreePhaseUnload(activeDescendants);
            }

            for (var i = 0; i < inactiveChildren.Count; i++)
            {
                await CleanupCanceledScene(inactiveChildren[i]);
            }

            // LoadCanceled に遷移（まだ遷移していなければ）
            if (!pair.SceneBase.Lifecycle.IsLoadCanceled)
            {
                TransitionSceneState(sceneIdentify, pair.SceneBase, SceneState.LoadCanceled);
            }

            // Unity Scene のアンロード（キャンセル窓内のため通常は Handle が null で no-op）
            await PerformUnitySceneUnload(sceneIdentify, pair.AddressablesSceneLoaded);

            // PreLoad で確保したリソースを解放する（LoadCanceled → AfterUnloading）
            await pair.SceneBase.ExecuteAfterUnLoad();
            _assetManagement.ReleaseScene(sceneIdentify);

            pair.SceneBase.Dispose();
            _currentScenes.Remove(sceneIdentify);
            _sceneEventSubject.OnNext(new SceneEvent(
                SceneEventType.CancelCleanedUp, sceneIdentify, SceneState.LoadCanceled,
                pair.SceneBase.Lifecycle.LastPhaseElapsedMs));
        }

        private async UniTask RunThreePhaseUnload(List<string> sceneIds)
        {
            if (sceneIds.Count == 0)
            {
                return;
            }

            var phaseTasks = new List<UniTask>(sceneIds.Count);

            for (var i = 0; i < sceneIds.Count; i++)
            {
                phaseTasks.Add(PhasePreUnload(sceneIds[i]));
            }

            await UniTask.WhenAll(phaseTasks);

            phaseTasks.Clear();
            for (var i = 0; i < sceneIds.Count; i++)
            {
                phaseTasks.Add(PhaseUnityUnload(sceneIds[i]));
            }

            await UniTask.WhenAll(phaseTasks);

            phaseTasks.Clear();
            for (var i = 0; i < sceneIds.Count; i++)
            {
                phaseTasks.Add(PhaseAfterUnloadAndDispose(sceneIds[i]));
            }

            await UniTask.WhenAll(phaseTasks);
        }

        // ─── Protected: Unity Scene Unload（テスト時にオーバーライド可能） ───

        /// <summary>
        /// Unity Scene をアンロードする。
        /// テスト時にオーバーライドして Addressables / SceneManager 依存を排除する。
        ///
        /// <para>AssetManagement 経由でロードしたシーン（addressablesSceneLoaded=true）は
        /// AssetManagement.UnloadSceneAsync を呼ぶ（Phase 2）。
        /// Editor 既存シーン等（false）は SceneManager.UnloadSceneAsync にフォールバックする。
        /// Payload 空の論理ノードは Unity Scene 実体を持たないため、UnloadSceneAsync を呼ばない。</para>
        /// </summary>
        /// <param name="sceneIdentify">対象シーンの Identity。</param>
        /// <param name="addressablesSceneLoaded">PerformUnitySceneLoad の戻り値 AddressablesLoaded。</param>
        protected virtual async UniTask PerformUnitySceneUnload(
            string sceneIdentify, bool addressablesSceneLoaded)
        {
            if (addressablesSceneLoaded)
            {
                // Phase 2: Unity Scene アンロードのみ。所有アセット Release は Phase 3
                await _assetManagement.UnloadSceneAsync(sceneIdentify);
                return;
            }

            // 空 Payload の論理ノード（InGameScene / InGameSession 等）は
            // PerformUnitySceneLoad が AddressablesLoaded=false・RootObjects 空で返す。
            // 実体の無い名前で UnloadSceneAsync すると "Scene to unload is invalid" になるためスキップする。
            var unityScene = SceneManager.GetSceneByName(sceneIdentify);
            if (!unityScene.IsValid() || !unityScene.isLoaded)
            {
                return;
            }

            var asyncOp = SceneManager.UnloadSceneAsync(sceneIdentify);
            if (asyncOp != null)
            {
                await asyncOp;
            }
        }
    }
}
