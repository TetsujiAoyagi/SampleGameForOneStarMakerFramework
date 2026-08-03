#nullable enable

using OneStarMaker.Foundation.Core;

namespace OneStarMaker.Foundation.Telemetry
{
    /// <summary>
    /// payload 内の意味ブロック識別子。
    /// kind×name ごとにどのフィールドが有効かを示す（0 埋め欠測を避けるため）。
    /// </summary>
    public enum TelemetryPayloadShape : byte
    {
        /// <summary>payload なし（早期 return の超短 span 等）。</summary>
        None = 0,

        /// <summary>span 向け: target/stage + memory before/after/delta。</summary>
        TimingMemory = 1,

        /// <summary>sample 向け: fps / cpu / gpu / 絶対メモリ。</summary>
        Frame = 2,

        /// <summary>event 向け: GcSpike 等の薄い根拠値。</summary>
        EventDetail = 3,

        /// <summary>sample 向け: CameraSystem の View / スタックカウンタ。</summary>
        CameraCounters = 4,
    }

    /// <summary>
    /// Contract v3 の用途固有ペイロード（ゼロアロ struct）。
    ///
    /// <para>
    /// 共通エンベロープ（elapsed / tags / session 等）には用途固有数値を増やさない。
    /// 未設定フィールドは Shape 外として無視し、wire 化時にキーごと省略する。
    /// 文字列（TargetIdentity / Stage）だけ参照を持ち得るが、呼び出し元が既に持つ
    /// identity / stage を渡すだけなので追加アロケーションは発生しない。
    /// </para>
    /// </summary>
    public readonly struct TelemetryPayload
    {
        public readonly TelemetryPayloadShape Shape;

        /// <summary>SceneLoad 等の対象 identity。無い場合は null。</summary>
        public readonly string? TargetIdentity;

        /// <summary>AppStartup の段階名（BeforeSceneLoad / AfterSceneLoad の stage 等）。</summary>
        public readonly string? Stage;

        public readonly long ManagedBeforeBytes;
        public readonly long NativeBeforeBytes;
        public readonly long ManagedAfterBytes;
        public readonly long NativeAfterBytes;
        public readonly long ManagedDeltaBytes;
        public readonly long NativeDeltaBytes;

        public readonly float Fps;
        public readonly float CpuMs;
        public readonly float GpuMs;
        public readonly bool GpuAvailable;
        public readonly long ManagedBytes;
        public readonly long NativeBytes;

        public readonly int GcGen0Delta;
        public readonly int UnityFrame;

        public readonly int CameraTotalViewCount;
        public readonly int CameraAdditionalViewCount;
        public readonly int CameraBlendingViewCount;
        public readonly int CameraMaxStackDepthTotal;

        /// <summary>memory timing ブロックを持つか（Shape 判定の糖衣）。</summary>
        public bool HasTimingMemory => Shape == TelemetryPayloadShape.TimingMemory;

        /// <summary>frame sample ブロックを持つか。</summary>
        public bool HasFrame => Shape == TelemetryPayloadShape.Frame;

        /// <summary>event 詳細ブロックを持つか。</summary>
        public bool HasEventDetail => Shape == TelemetryPayloadShape.EventDetail;

        /// <summary>camera counters sample ブロックを持つか。</summary>
        public bool HasCameraCounters => Shape == TelemetryPayloadShape.CameraCounters;

        private TelemetryPayload(
            TelemetryPayloadShape shape,
            string? targetIdentity,
            string? stage,
            long managedBeforeBytes,
            long nativeBeforeBytes,
            long managedAfterBytes,
            long nativeAfterBytes,
            long managedDeltaBytes,
            long nativeDeltaBytes,
            float fps,
            float cpuMs,
            float gpuMs,
            bool gpuAvailable,
            long managedBytes,
            long nativeBytes,
            int gcGen0Delta,
            int unityFrame,
            int cameraTotalViewCount = 0,
            int cameraAdditionalViewCount = 0,
            int cameraBlendingViewCount = 0,
            int cameraMaxStackDepthTotal = 0)
        {
            Shape = shape;
            TargetIdentity = targetIdentity;
            Stage = stage;
            ManagedBeforeBytes = managedBeforeBytes;
            NativeBeforeBytes = nativeBeforeBytes;
            ManagedAfterBytes = managedAfterBytes;
            NativeAfterBytes = nativeAfterBytes;
            ManagedDeltaBytes = managedDeltaBytes;
            NativeDeltaBytes = nativeDeltaBytes;
            Fps = fps;
            CpuMs = cpuMs;
            GpuMs = gpuMs;
            GpuAvailable = gpuAvailable;
            ManagedBytes = managedBytes;
            NativeBytes = nativeBytes;
            GcGen0Delta = gcGen0Delta;
            UnityFrame = unityFrame;
            CameraTotalViewCount = cameraTotalViewCount;
            CameraAdditionalViewCount = cameraAdditionalViewCount;
            CameraBlendingViewCount = cameraBlendingViewCount;
            CameraMaxStackDepthTotal = cameraMaxStackDepthTotal;
        }

        /// <summary>
        /// Scene* / AppStartup 用。cpu/gpu は載せない（区間 CPU 計測が無いのに欄を持たない）。
        /// </summary>
        public static TelemetryPayload ForTimingMemory(
            long managedBeforeBytes,
            long nativeBeforeBytes,
            long managedAfterBytes,
            long nativeAfterBytes,
            string? targetIdentity = null,
            string? stage = null)
        {
            return new TelemetryPayload(
                shape: TelemetryPayloadShape.TimingMemory,
                targetIdentity: targetIdentity,
                stage: stage,
                managedBeforeBytes: managedBeforeBytes,
                nativeBeforeBytes: nativeBeforeBytes,
                managedAfterBytes: managedAfterBytes,
                nativeAfterBytes: nativeAfterBytes,
                managedDeltaBytes: managedAfterBytes - managedBeforeBytes,
                nativeDeltaBytes: nativeAfterBytes - nativeBeforeBytes,
                fps: 0f,
                cpuMs: 0f,
                gpuMs: 0f,
                gpuAvailable: false,
                managedBytes: 0,
                nativeBytes: 0,
                gcGen0Delta: 0,
                unityFrame: 0);
        }

        /// <summary>
        /// ProfilerSummary 用 sample。gpu 非対応時は <paramref name="gpuAvailable"/> = false とし、
        /// wire では gpuMs キーを省略する（0 埋め禁止）。
        /// </summary>
        public static TelemetryPayload ForFrameSample(
            float fps,
            float cpuMs,
            float gpuMs,
            bool gpuAvailable,
            long managedBytes,
            long nativeBytes)
        {
            return new TelemetryPayload(
                shape: TelemetryPayloadShape.Frame,
                targetIdentity: null,
                stage: null,
                managedBeforeBytes: 0,
                nativeBeforeBytes: 0,
                managedAfterBytes: 0,
                nativeAfterBytes: 0,
                managedDeltaBytes: 0,
                nativeDeltaBytes: 0,
                fps: fps,
                cpuMs: cpuMs,
                gpuMs: gpuAvailable ? gpuMs : 0f,
                gpuAvailable: gpuAvailable,
                managedBytes: managedBytes,
                nativeBytes: nativeBytes,
                gcGen0Delta: 0,
                unityFrame: 0);
        }

        /// <summary>GcSpike 等の薄い event payload。</summary>
        public static TelemetryPayload ForEventDetail(int gcGen0Delta, int unityFrame)
        {
            return new TelemetryPayload(
                shape: TelemetryPayloadShape.EventDetail,
                targetIdentity: null,
                stage: null,
                managedBeforeBytes: 0,
                nativeBeforeBytes: 0,
                managedAfterBytes: 0,
                nativeAfterBytes: 0,
                managedDeltaBytes: 0,
                nativeDeltaBytes: 0,
                fps: 0f,
                cpuMs: 0f,
                gpuMs: 0f,
                gpuAvailable: false,
                managedBytes: 0,
                nativeBytes: 0,
                gcGen0Delta: gcGen0Delta,
                unityFrame: unityFrame);
        }

        /// <summary>CameraSystemSnapshot 用 sample。カウンタの正本はここ（flat metadata は併記）。</summary>
        public static TelemetryPayload ForCameraCounters(
            int totalViewCount,
            int additionalViewCount,
            int blendingViewCount,
            int maxStackDepthTotal)
        {
            return new TelemetryPayload(
                shape: TelemetryPayloadShape.CameraCounters,
                targetIdentity: null,
                stage: null,
                managedBeforeBytes: 0,
                nativeBeforeBytes: 0,
                managedAfterBytes: 0,
                nativeAfterBytes: 0,
                managedDeltaBytes: 0,
                nativeDeltaBytes: 0,
                fps: 0f,
                cpuMs: 0f,
                gpuMs: 0f,
                gpuAvailable: false,
                managedBytes: 0,
                nativeBytes: 0,
                gcGen0Delta: 0,
                unityFrame: 0,
                cameraTotalViewCount: totalViewCount,
                cameraAdditionalViewCount: additionalViewCount,
                cameraBlendingViewCount: blendingViewCount,
                cameraMaxStackDepthTotal: maxStackDepthTotal);
        }
    }

    /// <summary>
    /// StartType → Kind の既定対応。producer が明示指定しない場合の安全な既定。
    /// </summary>
    public static class TelemetryKindRules
    {
        /// <summary>
        /// 現行 StartType から Contract v3 の kind を決める。
        /// 新しい StartType を足すときはここに分岐を追加する。
        /// </summary>
        public static TelemetryKind InferKind(TelemetryStartType name)
        {
            return name switch
            {
                TelemetryStartType.ProfilerSummary => TelemetryKind.Sample,
                TelemetryStartType.CameraSystemSnapshot => TelemetryKind.Sample,
                TelemetryStartType.GcSpike => TelemetryKind.Event,
                TelemetryStartType.UiCost => TelemetryKind.Event,
                _ => TelemetryKind.Span,
            };
        }
    }
}
