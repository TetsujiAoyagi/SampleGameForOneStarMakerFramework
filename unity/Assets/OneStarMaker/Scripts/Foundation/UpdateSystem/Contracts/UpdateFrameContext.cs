using System;

namespace OneStarMaker.Foundation.UpdateSystem
{
    /// <summary>
    /// 1 tick 分の時間情報と Layer 状態を Element へ渡すコンテキスト。
    /// Element と native processor が同じ入力条件で更新できるよう、読み取り専用値としてまとめている。
    /// </summary>
    public readonly struct UpdateFrameContext
    {
        public UpdateFrameContext(
            uint frameIndex,
            float deltaTime,
            float unscaledDeltaTime,
            float timeScale,
            bool isPaused)
        {
            FrameIndex = frameIndex;
            DeltaTime = deltaTime;
            UnscaledDeltaTime = unscaledDeltaTime;
            TimeScale = timeScale;
            IsPaused = isPaused;
        }

        /// <summary>
        /// UpdateCoordinator 内で採番されるフレーム番号。
        /// 複数 Layer が同一フレーム内に走っても、この番号自体は coordinator 単位で一貫する。
        /// </summary>
        public uint FrameIndex { get; }

        /// <summary>
        /// Layer の timeScale 適用後 deltaTime。
        /// Element はこの値を通常のシミュレーション進行に使う。
        /// </summary>
        public float DeltaTime { get; }

        /// <summary>
        /// timeScale 非適用の deltaTime。
        /// UI や補正ロジックなど、実時間基準の処理で参照する。
        /// </summary>
        public float UnscaledDeltaTime { get; }

        /// <summary>
        /// この Layer に現在設定されている timeScale。
        /// Element 側で delta の意味を補足したい場合に使用する。
        /// </summary>
        public float TimeScale { get; }

        /// <summary>
        /// 実行時点で Layer が pause 状態か。
        /// 通常は pause 時に scheduler 側まで到達しないが、将来の特殊 backend でも
        /// 状態を参照できるよう context に保持している。
        /// </summary>
        public bool IsPaused { get; }
    }
}
