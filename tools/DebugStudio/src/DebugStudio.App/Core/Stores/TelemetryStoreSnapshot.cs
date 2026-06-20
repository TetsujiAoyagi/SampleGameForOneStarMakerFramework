#nullable enable

using System.Collections.Generic;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// telemetry / service status window が購読する軽量サマリー。
///
/// <para>
/// 直近 1 件だけだと「今どの失敗が連続しているか」「接続直後に何が流れたか」を追いづらいため、
/// R2 では recent history もここへ載せる。
/// envelope 自体は軽量参照として共有し、文字列化は引き続き ViewModel 側に寄せる。
/// </para>
/// </summary>
public readonly record struct TelemetryStoreSnapshot(
    long TelemetryCount,
    long ServiceStatusCount,
    DebugTelemetryEnvelopeV1? LatestTelemetry,
    DebugSocketServiceStatusEnvelopeV1? LatestServiceStatus,
    IReadOnlyList<DebugTelemetryEnvelopeV1> RecentTelemetry,
    IReadOnlyList<DebugSocketServiceStatusEnvelopeV1> RecentServiceStatuses,
    int RetainedTelemetryCount,
    int RetainedServiceStatusCount);
