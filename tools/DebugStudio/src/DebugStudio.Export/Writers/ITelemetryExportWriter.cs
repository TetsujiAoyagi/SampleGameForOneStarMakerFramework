#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// normalized telemetry export record を永続化先へ書き出す writer 抽象。
/// file 出力・bulk artifact・将来の adapter CLI で同じ contract を共有する。
/// </summary>
public interface ITelemetryExportWriter
{
    TelemetryExportFormat Format { get; }

    Task WriteAsync(IReadOnlyList<TelemetryExportRecord> records, string outputPath, CancellationToken cancellationToken = default);
}
