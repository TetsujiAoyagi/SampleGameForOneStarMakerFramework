#nullable enable

using System;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using UnityEngine.Profiling;

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// Runtime / Debug で使う telemetry metadata / payload と tag 判定の共通 helper。
    /// hot path のゼロアロ特性を崩さないことを優先する。
    /// </summary>
    public static class RuntimeTelemetryMetadataFactory
    {
        public static RuntimeTelemetryMemorySnapshot CaptureMemorySnapshot()
        {
            return new RuntimeTelemetryMemorySnapshot(
                managedMem: GC.GetTotalMemory(false),
                nativeMem: Profiler.GetTotalAllocatedMemoryLong());
        }

        /// <summary>
        /// 旧フラット metadata（finish 時の絶対値）。段階移行の併記用。
        /// 新規意味づけは <see cref="CreateTimingMemoryTelemetry"/> の payload を正とする。
        /// </summary>
        public static Metadata CreateMemoryMetadata(in RuntimeTelemetryMemorySnapshot snapshot)
        {
            return new Metadata(
                managedMem: snapshot.ManagedMem,
                nativeMem: snapshot.NativeMem);
        }

        /// <summary>
        /// Scene* / AppStartup 向けに、旧 metadata（after 絶対値）と v3 payload（before/after/delta）を同時生成する。
        /// cpu/gpu は載せない（区間計測が無いのに欄を持たせない）。
        /// </summary>
        public static (Metadata metadata, TelemetryPayload payload) CreateTimingMemoryTelemetry(
            in RuntimeTelemetryMemorySnapshot before,
            in RuntimeTelemetryMemorySnapshot after,
            string? targetIdentity = null,
            string? stage = null)
        {
            var metadata = CreateMemoryMetadata(after);
            var payload = TelemetryPayload.ForTimingMemory(
                managedBeforeBytes: before.ManagedMem,
                nativeBeforeBytes: before.NativeMem,
                managedAfterBytes: after.ManagedMem,
                nativeAfterBytes: after.NativeMem,
                targetIdentity: targetIdentity,
                stage: stage);
            return (metadata, payload);
        }

        /// <summary>
        /// ProfilerSummary 向け。旧 flat metadata と Frame payload を同時生成する。
        /// GPU 非対応時は payload 側で GpuMs を省略し、flat の GpuTime は 0 のまま（旧互換）。
        /// </summary>
        public static (Metadata metadata, TelemetryPayload payload) CreateFrameSampleTelemetry(
            float fps,
            float cpuTime,
            float gpuTime,
            bool gpuAvailable)
        {
            var managed = Profiler.GetMonoUsedSizeLong();
            var native = Profiler.GetTotalAllocatedMemoryLong();
            var metadata = new Metadata(
                cpuTime: cpuTime,
                gpuTime: gpuAvailable ? gpuTime : 0f,
                managedMem: managed,
                nativeMem: native);
            var payload = TelemetryPayload.ForFrameSample(
                fps: fps,
                cpuMs: cpuTime,
                gpuMs: gpuTime,
                gpuAvailable: gpuAvailable,
                managedBytes: managed,
                nativeBytes: native);
            return (metadata, payload);
        }

        public static Metadata CreateProfilerMetadata(float cpuTime, float gpuTime)
        {
            return new Metadata(
                cpuTime: cpuTime,
                gpuTime: gpuTime,
                managedMem: Profiler.GetMonoUsedSizeLong(),
                nativeMem: Profiler.GetTotalAllocatedMemoryLong());
        }

        public static TelemetryTagType? ClassifyFrameRate(float fps)
        {
            if (fps > 0f && fps < 30f)
            {
                return TelemetryTagType.FrameRateDrop;
            }

            return null;
        }

        public static TelemetryTagType? ClassifyMemoryDelta(
            in RuntimeTelemetryMemorySnapshot before,
            in RuntimeTelemetryMemorySnapshot after,
            TelemetryThresholds? thresholds)
        {
            if (thresholds == null)
            {
                return null;
            }

            var managedDeltaMb = GetManagedDeltaMb(before, after);
            var nativeDeltaMb = GetNativeDeltaMb(before, after);
            TelemetryTagType? tags = null;

            if (managedDeltaMb > thresholds.MemoryDeltaMb)
            {
                tags = TelemetryTagType.ManagedMemoryOver;
            }

            if (nativeDeltaMb > thresholds.MemoryDeltaMb)
            {
                tags = tags.HasValue
                    ? tags | TelemetryTagType.NativeMemoryOver
                    : TelemetryTagType.NativeMemoryOver;
            }

            if (tags.HasValue)
            {
                tags |= TelemetryTagType.Bottleneck;
            }

            return tags;
        }

        public static double GetManagedDeltaMb(
            in RuntimeTelemetryMemorySnapshot before,
            in RuntimeTelemetryMemorySnapshot after)
        {
            return (after.ManagedMem - before.ManagedMem) / (1024.0 * 1024.0);
        }

        public static double GetNativeDeltaMb(
            in RuntimeTelemetryMemorySnapshot before,
            in RuntimeTelemetryMemorySnapshot after)
        {
            return (after.NativeMem - before.NativeMem) / (1024.0 * 1024.0);
        }
    }
}
