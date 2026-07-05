#nullable enable

using System;
using OneStarMaker.Foundation.Core;
using OneStarMaker.Foundation.Telemetry;
using UnityEngine.Profiling;

namespace OneStarMaker.Runtime
{
    /// <summary>
    /// Runtime / Debug で使う telemetry metadata と tag 判定の共通 helper。
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

        public static Metadata CreateMemoryMetadata(in RuntimeTelemetryMemorySnapshot snapshot)
        {
            return new Metadata(
                managedMem: snapshot.ManagedMem,
                nativeMem: snapshot.NativeMem);
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
