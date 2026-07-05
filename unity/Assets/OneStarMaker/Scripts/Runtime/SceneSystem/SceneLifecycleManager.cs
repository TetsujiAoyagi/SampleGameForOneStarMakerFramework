#nullable enable

using System;
using System.Diagnostics;

namespace OneStarMaker.Runtime.SceneSystem
{
    /// <summary>
    /// SceneState の遷移を一元管理する。
    /// SceneState の変更はこのクラスのみが行う。SceneDirector や SceneBase が直接書き換えてはならない。
    /// フェーズ毎の滞在時間を Stopwatch で自動計測する。
    /// </summary>
    internal class SceneLifecycleManager
    {
        private SceneState _state = SceneState.None;
        private readonly Stopwatch _phaseStopwatch = new();
        private long _lastPhaseElapsedMs;

        /// <summary>現在の状態。</summary>
        public SceneState State => _state;

        // ─── ヘルパープロパティ: 範囲比較を隠蔽する ───

        /// <summary>ロードフェーズ中か（PreLoading 〜 WaitLoadChildScene）。</summary>
        public bool IsInLoadingPhase
            => _state is >= SceneState.PreLoading and <= SceneState.WaitLoadChildScene;

        /// <summary>安定状態か（Initializing + Stable）。</summary>
        public bool IsActive
            => _state is SceneState.Initializing or SceneState.Stable;

        /// <summary>アンロードが開始されたか（PreUnloading 以降）。</summary>
        public bool IsUnloadStarted
            => _state >= SceneState.PreUnloading
               && _state != SceneState.LoadCanceled;

        /// <summary>ロード開始済みかつアンロード未開始か。AddScene の二重呼び出し判定用。</summary>
        public bool IsLoadedOrActive
            => _state is >= SceneState.Loading and <= SceneState.Stable
               && _state != SceneState.LoadCanceled;

        /// <summary>PreLoad が未実行か。</summary>
        public bool IsNone
            => _state == SceneState.None;

        /// <summary>ロードがキャンセルされたか。</summary>
        public bool IsLoadCanceled
            => _state == SceneState.LoadCanceled;

        /// <summary>AfterUnloading 以降か。Unload 完了待ち判定用。</summary>
        public bool IsInAfterUnloading
            => _state >= SceneState.AfterUnloading;

        /// <summary>直前のフェーズの所要時間 (ms)。TransitionTo 呼び出し後に有効。</summary>
        public long LastPhaseElapsedMs => _lastPhaseElapsedMs;

        /// <summary>
        /// 状態を遷移させる。無効な遷移の場合は例外を投げる。
        /// フェーズの滞在時間を自動計測する。
        /// </summary>
        /// <param name="newState">遷移先の状態。</param>
        /// <exception cref="InvalidOperationException">無効な状態遷移。</exception>
        public void TransitionTo(SceneState newState)
        {
            if (!IsValidTransition(_state, newState))
            {
                throw new InvalidOperationException(
                    $"Invalid scene state transition: {_state} → {newState}");
            }

            // フェーズ計測: 前フェーズの経過時間を記録し、新フェーズの計測を開始
            _lastPhaseElapsedMs = _phaseStopwatch.ElapsedMilliseconds;
            _phaseStopwatch.Restart();

            _state = newState;
        }

        /// <summary>
        /// 状態遷移の妥当性を検証する。
        /// </summary>
        private static bool IsValidTransition(SceneState from, SceneState to)
        {
            return (from, to) switch
            {
                // ── Load パス ──
                (SceneState.None, SceneState.PreLoading) => true,
                (SceneState.PreLoading, SceneState.PreLoaded) => true,
                (SceneState.PreLoaded, SceneState.Loading) => true,
                (SceneState.Loading, SceneState.Loaded) => true,
                (SceneState.Loaded, SceneState.WaitLoadChildScene) => true,
                (SceneState.WaitLoadChildScene, SceneState.Initializing) => true,
                (SceneState.Initializing, SceneState.Stable) => true,

                // ── Unload パス ──
                (SceneState.Stable, SceneState.PreUnloading) => true,
                (SceneState.PreUnloading, SceneState.PreUnloaded) => true,
                (SceneState.PreUnloaded, SceneState.Unloading) => true,
                (SceneState.Unloading, SceneState.Unloaded) => true,
                (SceneState.Unloaded, SceneState.AfterUnloading) => true,

                // ── キャンセルパス（ロードフェーズからのみ） ──
                (>= SceneState.PreLoading and <= SceneState.WaitLoadChildScene,
                    SceneState.LoadCanceled) => true,

                // ── キャンセル後クリーンアップ: PreLoad で確保したリソースを AfterUnload で解放 ──
                (SceneState.LoadCanceled, SceneState.AfterUnloading) => true,

                _ => false,
            };
        }
    }
}
