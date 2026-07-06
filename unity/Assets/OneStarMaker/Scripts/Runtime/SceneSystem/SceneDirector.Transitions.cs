#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Text;
using Cysharp.Threading.Tasks;
using OneStarMaker.Foundation.Telemetry;
using OneStarMaker.Runtime;

namespace OneStarMaker.Runtime.SceneSystem
{
    // ─── Transitions ───
    // SwitchScene, GoBack, ClearHistory, ExecuteTransitionPlan

    public partial class SceneDirector
    {
        /// <summary>
        /// 兄弟シーンを切り替える。
        /// ローディング表示 → 旧シーンアンロード → 新シーンロード → ローディング終了
        /// をアトミックに行い、遷移中のちらつきを防ぐ。
        /// 遷移履歴をスタックに記録し、<see cref="GoBack"/> による巻き戻しを可能にする。
        ///
        /// キャンセルポリシー:
        ///   ct が効くのは _loadingDisplay.Show() のみ（フェードイン中のキャンセル）。
        ///   Show 完了後は PoNR（ポイント・オブ・ノーリターン）。
        ///   以降の Unload / AddScene / Hide は全て CancellationToken.None で実行し、
        ///   旧シーンが消えたまま宙ぶらりんになる状態を原理的に排除する。
        ///
        /// Unload → Add の順序は意図的:
        ///   旧シーンのメモリを先に解放してから新シーンをロードすることで、
        ///   両シーンが同時に存在するメモリピークを回避する。
        ///   PoNR 通過後のため、キャンセルによる画面消失リスクはない。
        /// </summary>
        /// <param name="fromSceneIdentify">アンロードするシーンの ID。null なら Unload なし（Add のみ）。</param>
        /// <param name="toSceneIdentify">ロードするシーンの ID。</param>
        /// <param name="ct">キャンセルトークン（Show フェーズのみ有効）。</param>
        /// <param name="context">遷移先シーンに渡す型付きコンテキスト。</param>
        /// <param name="loadingDisplay">ローディング表示モード。</param>
        /// <param name="telemetryTags">テレメトリスパンに付与する追加タグ。</param>
        public async UniTask SwitchScene(
            string? fromSceneIdentify,
            string toSceneIdentify,
            CancellationToken ct,
            SceneContext? context = null,
            LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen,
            IReadOnlyDictionary<string, string>? telemetryTags = null)
        {
            await SwitchSceneCore(fromSceneIdentify, toSceneIdentify, ct, context, loadingDisplay,
                recordHistory: true, telemetryTags: telemetryTags);
        }

        /// <summary>
        /// ひとつ前のシーンに戻る。
        /// <see cref="SwitchScene"/> で記録された遷移履歴をスタックから取り出し、
        /// 逆方向の遷移を行う。
        ///
        /// キャンセルポリシーは SwitchScene と同一:
        ///   ct が効くのは Show のフェードイン中のみ。
        ///   キャンセルされた場合、履歴はそのまま残る（何も変化しない）。
        /// </summary>
        /// <param name="ct">キャンセルトークン（Show フェーズのみ有効）。</param>
        /// <param name="context">戻り先シーンに渡す型付きコンテキスト。</param>
        /// <param name="loadingDisplay">ローディング表示モード。</param>
        /// <param name="telemetryTags">テレメトリスパンに付与する追加タグ。</param>
        /// <exception cref="InvalidOperationException">履歴が空の場合。</exception>
        public async UniTask GoBack(
            CancellationToken ct,
            SceneContext? context = null,
            LoadingDisplayType loadingDisplay = LoadingDisplayType.BlackScreen,
            IReadOnlyDictionary<string, string>? telemetryTags = null)
        {
            if (_sceneHistory.Count == 0)
            {
                throw new InvalidOperationException("シーン履歴が空のため GoBack できません。");
            }

            var entry = _sceneHistory.Peek();

            // SwitchSceneCore 内で Show が ct でキャンセルされた場合は
            // OperationCanceledException が throw され、Pop は実行されない → 履歴は残る。
            await SwitchSceneCore(
                entry.ToSceneId,
                entry.FromSceneId,
                ct,
                context,
                loadingDisplay,
                recordHistory: false,
                telemetryTags: telemetryTags);

            // PoNR 通過 + 遷移成功後にのみ Pop
            _sceneHistory.Pop();
        }

        /// <summary>
        /// シーン遷移履歴をクリアする。
        /// タイトル画面への復帰など、履歴をリセットしたい場合に使用する。
        /// </summary>
        public void ClearHistory() => _sceneHistory.Clear();

        // ─── Private: Core switch logic ───

        /// <summary>
        /// SwitchScene / GoBack 共通のコア実装。
        /// </summary>
        private async UniTask SwitchSceneCore(
            string? fromSceneIdentify,
            string toSceneIdentify,
            CancellationToken ct,
            SceneContext? context,
            LoadingDisplayType loadingDisplay,
            bool recordHistory,
            IReadOnlyDictionary<string, string>? telemetryTags = null)
        {
            // セル identity ガード（R-3/G-4）: セルは AddScene / UnloadScene 専用（D-5）。
            // 履歴・TransitionPlan を汚染しないよう、span 開始・Show・履歴記録より前に失敗させる。
            ThrowIfCellIdentity(fromSceneIdentify);
            ThrowIfCellIdentity(toSceneIdentify);

            // 文字列タグの持ち回りはやめ、操作種別は StartType、数値情報は Metadata に寄せる。
            // これにより scene 遷移でも追加ヒープ確保を増やさず transport へ流せる。
            var span = AppTelemetry.StartSpan(Foundation.Core.TelemetryStartType.SceneTransition, null);
            var success = false;

            // ── メモリスナップショット: Before ──
            var memBefore = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();

            var showedLoading = loadingDisplay != LoadingDisplayType.None;
            if (showedLoading)
            {
                // ★ キャンセル可能なのはここだけ。フェードイン中に ct がキャンセルされれば
                //    OperationCanceledException が throw され、何も変異せずに終了する。
                await _loadingDisplay.Show(loadingDisplay, ct);
            }

            // ★ ポイント・オブ・ノーリターン ★
            // Show 完了後はコミット。以降は CancellationToken.None で実行する。

            // 履歴記録（PoNR 通過後に記録するため、キャンセル時の巻き戻しが不要）
            if (recordHistory && fromSceneIdentify != null)
            {
                if (_sceneHistory.Count >= MaxSceneHistoryCount)
                {
                    // 最古のエントリ（スタック底）を破棄して空きを作る
                    var items = _sceneHistory.ToArray(); // top-first order
                    _sceneHistory.Clear();
                    for (var i = items.Length - 2; i >= 0; i--)
                    {
                        _sceneHistory.Push(items[i]);
                    }
                }
                _sceneHistory.Push(new SceneHistoryEntry(fromSceneIdentify, toSceneIdentify));
            }

            try
            {
                // 旧シーンのアンロード（メモリ解放を先行）
                if (fromSceneIdentify != null)
                {
                    await UnloadScene(fromSceneIdentify);
                }

                // 新シーンのロード（非キャンセル）
                // ※ loadingDisplay は意図的にデフォルト None。
                //    SwitchScene が Show/Hide を管理しているため、AddScene 内で二重表示させない。
                await AddScene(toSceneIdentify, null, CancellationToken.None, context);

                // NOTE: SceneTransitionPlan のチェックはここでは行わない。
                // AddScene 内で Stable 到達後に CreateTransitionPlan() → ExecuteTransitionPlan() が
                // 既に実行されるため、ここで重複チェックすると二重実行・無限ループのリスクがある。
                success = true;
            }
            finally
            {
                if (showedLoading)
                {
                    await _loadingDisplay.Hide(CancellationToken.None);
                }

                var memAfter = RuntimeTelemetryMetadataFactory.CaptureMemorySnapshot();
                var tags = RuntimeTelemetryMetadataFactory.ClassifyMemoryDelta(memBefore, memAfter, AppTelemetry.Thresholds);
                if (tags.HasValue)
                {
                    AppTelemetry.NotifyBottleneck(
                        ZString.Format(
                            "[\u26a0 Memory] managed={0:F1}MB native={1:F1}MB after SwitchScene {2} \u2192 {3}",
                            RuntimeTelemetryMetadataFactory.GetManagedDeltaMb(memBefore, memAfter),
                            RuntimeTelemetryMetadataFactory.GetNativeDeltaMb(memBefore, memAfter),
                            fromSceneIdentify ?? "(none)",
                            toSceneIdentify));
                }

                var metadata = RuntimeTelemetryMetadataFactory.CreateMemoryMetadata(memAfter);

                AppTelemetry.FinishSpan(span, metadata, success, TelemetryLevel.Summary, tags);
            }
        }

        /// <summary>
        /// セル identity（`Cell_{x}_{y}`）を画面遷移に乗せようとした場合に即失敗させる。
        /// GoBack / ExecuteTransitionPlan も SwitchSceneCore を経由するため、ここで全経路を守る。
        /// </summary>
        private static void ThrowIfCellIdentity(string? sceneIdentify)
        {
            if (sceneIdentify != null && CellIdentity.IsCellId(sceneIdentify))
            {
                throw new InvalidOperationException(
                    $"セル identity '{sceneIdentify}' を SwitchScene / GoBack / TransitionPlan に乗せることはできません（R-3）。" +
                    "セルは AddScene / UnloadScene 専用です（21-scene-streaming.md D-5）。");
            }
        }

        // ─── Private: SceneTransitionPlan execution ───

        /// <summary>
        /// SceneTransitionPlan を解釈し、安全な順序で実行する。
        /// Stable 到達後に呼ばれることを前提とする。
        /// </summary>
        private async UniTask ExecuteTransitionPlan(string currentSceneIdentify, SceneTransitionPlan plan)
        {
            if (plan.NextSceneId != null)
            {
                // 兄弟切り替え: 現在のシーンをアンロードし、次のシーンをロード
                await SwitchScene(
                    currentSceneIdentify,
                    plan.NextSceneId,
                    CancellationToken.None,
                    plan.Context,
                    plan.LoadingDisplay);
            }
            else
            {
                // Unload のみ
                await UnloadScene(currentSceneIdentify, plan.LoadingDisplay);
            }
        }
    }
}
