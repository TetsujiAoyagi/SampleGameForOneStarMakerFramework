#nullable enable

using System;
using System.Collections.Generic;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// telemetry と service status の最新状態を session 単位で保持する store。
///
/// <para>
/// 初期 wave では「件数」と「最後に届いた frame」だけだったが、
/// 調査用途では「直近に何が続けて起きたか」を panel 内だけで追いたくなる。
/// そのため recent history もここで保持し、ViewModel は描画整形に集中させる。
/// </para>
/// </summary>
public sealed class TelemetryStore
{
    private readonly object _gate = new();
    private readonly int _historyCapacity;
    private readonly int _retainedCapacity;
    private readonly Queue<DebugTelemetryEnvelopeV1> _recentTelemetry = new();
    private readonly Queue<DebugSocketServiceStatusEnvelopeV1> _recentServiceStatuses = new();
    private readonly Queue<DebugTelemetryEnvelopeV1> _retainedTelemetry = new();
    private readonly Queue<DebugSocketServiceStatusEnvelopeV1> _retainedServiceStatuses = new();
    private long _telemetryCount;
    private long _serviceStatusCount;
    private DebugTelemetryEnvelopeV1? _latestTelemetry;
    private DebugSocketServiceStatusEnvelopeV1? _latestServiceStatus;

    public TelemetryStore(int historyCapacity = 20, int retainedCapacity = 256)
    {
        _historyCapacity = historyCapacity > 0
            ? historyCapacity
            : throw new ArgumentOutOfRangeException(nameof(historyCapacity));
        _retainedCapacity = retainedCapacity > 0
            ? retainedCapacity
            : throw new ArgumentOutOfRangeException(nameof(retainedCapacity));
    }

    public event Action<TelemetryStoreSnapshot>? Changed;

    public TelemetryStoreSnapshot AppendTelemetry(DebugTelemetryEnvelopeV1 telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        TelemetryStoreSnapshot snapshot;
        lock (_gate)
        {
            _telemetryCount++;
            var retainedTelemetry = CloneTelemetry(telemetry);
            _latestTelemetry = retainedTelemetry;
            AppendRecentUnsafe(_recentTelemetry, retainedTelemetry);
            AppendRetainedUnsafe(_retainedTelemetry, retainedTelemetry);
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public TelemetryStoreSnapshot AppendServiceStatus(DebugSocketServiceStatusEnvelopeV1 serviceStatus)
    {
        ArgumentNullException.ThrowIfNull(serviceStatus);

        TelemetryStoreSnapshot snapshot;
        lock (_gate)
        {
            _serviceStatusCount++;
            var retainedServiceStatus = CloneServiceStatus(serviceStatus);
            _latestServiceStatus = retainedServiceStatus;
            AppendRecentUnsafe(_recentServiceStatuses, retainedServiceStatus);
            AppendRetainedUnsafe(_retainedServiceStatuses, retainedServiceStatus);
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public TelemetryStoreSnapshot Reset()
    {
        TelemetryStoreSnapshot snapshot;
        lock (_gate)
        {
            _telemetryCount = 0;
            _serviceStatusCount = 0;
            _latestTelemetry = null;
            _latestServiceStatus = null;
            _recentTelemetry.Clear();
            _recentServiceStatuses.Clear();
            _retainedTelemetry.Clear();
            _retainedServiceStatuses.Clear();
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public TelemetryStoreSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return CreateSnapshotUnsafe();
        }
    }

    /// <summary>
    /// export や後続 query 用に、保持中 telemetry の正本コピーを返す。
    /// 呼び出し側から store 内部 state を変更できないよう、clone 済み配列にして返す。
    /// </summary>
    public IReadOnlyList<DebugTelemetryEnvelopeV1> GetTelemetrySnapshot()
    {
        lock (_gate)
        {
            return CloneArrayUnsafe(_retainedTelemetry, CloneTelemetry);
        }
    }

    /// <summary>
    /// 保持中 service status の正本コピーを返す。
    /// telemetry export では service status も同じ NDJSON stream に正規化して流すため、この導線を持つ。
    /// </summary>
    public IReadOnlyList<DebugSocketServiceStatusEnvelopeV1> GetServiceStatusSnapshot()
    {
        lock (_gate)
        {
            return CloneArrayUnsafe(_retainedServiceStatuses, CloneServiceStatus);
        }
    }

    /// <summary>
    /// telemetry export 用に retained telemetry / service status を同一 lock で複製する。
    /// これにより export service は 2 回 lock を取り直さず、一貫した時点の snapshot を扱える。
    /// </summary>
    public TelemetryRetainedSnapshot GetRetainedSnapshot()
    {
        lock (_gate)
        {
            return new TelemetryRetainedSnapshot(
                CloneArrayUnsafe(_retainedTelemetry, CloneTelemetry),
                CloneArrayUnsafe(_retainedServiceStatuses, CloneServiceStatus));
        }
    }

    private TelemetryStoreSnapshot CreateSnapshotUnsafe()
    {
        var recentTelemetry = CreateLatestFirstArrayUnsafe(_recentTelemetry);
        var recentServiceStatuses = CreateLatestFirstArrayUnsafe(_recentServiceStatuses);
        return new TelemetryStoreSnapshot(
            _telemetryCount,
            _serviceStatusCount,
            _latestTelemetry,
            _latestServiceStatus,
            recentTelemetry,
            recentServiceStatuses,
            _retainedTelemetry.Count,
            _retainedServiceStatuses.Count);
    }

    private void AppendRecentUnsafe<TEnvelope>(Queue<TEnvelope> queue, TEnvelope envelope)
    {
        while (queue.Count >= _historyCapacity)
        {
            queue.Dequeue();
        }

        queue.Enqueue(envelope);
    }

    private void AppendRetainedUnsafe<TEnvelope>(Queue<TEnvelope> queue, TEnvelope envelope)
    {
        while (queue.Count >= _retainedCapacity)
        {
            queue.Dequeue();
        }

        queue.Enqueue(envelope);
    }

    private static TEnvelope[] CreateLatestFirstArrayUnsafe<TEnvelope>(Queue<TEnvelope> queue)
    {
        if (queue.Count == 0)
        {
            return Array.Empty<TEnvelope>();
        }

        var items = queue.ToArray();
        Array.Reverse(items);
        return items;
    }

    private static TEnvelope[] CloneArrayUnsafe<TEnvelope>(Queue<TEnvelope> queue, Func<TEnvelope, TEnvelope> clone)
    {
        if (queue.Count == 0)
        {
            return Array.Empty<TEnvelope>();
        }

        var source = queue.ToArray();
        var result = new TEnvelope[source.Length];
        for (var index = 0; index < source.Length; index++)
        {
            result[index] = clone(source[index]);
        }

        return result;
    }

    private static DebugTelemetryEnvelopeV1 CloneTelemetry(DebugTelemetryEnvelopeV1 telemetry)
    {
        return new DebugTelemetryEnvelopeV1
        {
            SchemaVersion = telemetry.SchemaVersion,
            TraceId = telemetry.TraceId,
            SpanId = telemetry.SpanId,
            ParentSpanId = telemetry.ParentSpanId,
            Name = telemetry.Name,
            StartTimestampUtcTicks = telemetry.StartTimestampUtcTicks,
            EndTimestampUtcTicks = telemetry.EndTimestampUtcTicks,
            ElapsedMs = telemetry.ElapsedMs,
            IsSuccess = telemetry.IsSuccess,
            Level = telemetry.Level,
            TagBits = telemetry.TagBits,
            CpuTime = telemetry.CpuTime,
            GpuTime = telemetry.GpuTime,
            ManagedMem = telemetry.ManagedMem,
            NativeMem = telemetry.NativeMem,
            SceneFrom = telemetry.SceneFrom,
            SceneTo = telemetry.SceneTo,
            CameraTotalViewCount = telemetry.CameraTotalViewCount,
            CameraAdditionalViewCount = telemetry.CameraAdditionalViewCount,
            CameraBlendingViewCount = telemetry.CameraBlendingViewCount,
            CameraMaxStackDepthTotal = telemetry.CameraMaxStackDepthTotal,
            CameraViewId = telemetry.CameraViewId,
            CameraActiveCameraHash = telemetry.CameraActiveCameraHash,
            SessionId = telemetry.SessionId,
            ProducerSequence = telemetry.ProducerSequence,
            UnityFrameAtStart = telemetry.UnityFrameAtStart,
            UnityFrameAtEnd = telemetry.UnityFrameAtEnd,
        };
    }

    private static DebugSocketServiceStatusEnvelopeV1 CloneServiceStatus(DebugSocketServiceStatusEnvelopeV1 serviceStatus)
    {
        return new DebugSocketServiceStatusEnvelopeV1
        {
            Status = serviceStatus.Status,
            Message = serviceStatus.Message,
            TimestampUnixTimeMilliseconds = serviceStatus.TimestampUnixTimeMilliseconds,
        };
    }
}
