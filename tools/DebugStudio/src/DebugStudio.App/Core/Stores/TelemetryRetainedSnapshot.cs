#nullable enable

using System.Collections.Generic;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// telemetry export 用に、retained telemetry / service status を同一時点で束ねた snapshot。
/// export service が別々に lock を取り直さず、時系列整合した 1 組として扱えるようにする。
/// </summary>
public readonly record struct TelemetryRetainedSnapshot(
    IReadOnlyList<DebugTelemetryEnvelopeV1> Telemetry,
    IReadOnlyList<DebugSocketServiceStatusEnvelopeV1> ServiceStatuses);
