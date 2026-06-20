#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.Export.Models;

namespace DebugStudio.Export.Writers;

/// <summary>
/// log export の出力先を隠蔽する writer 抽象。
/// App 側は query と選択だけを持ち、永続化方式はここへ委譲する。
/// </summary>
public interface ILogExportWriter
{
    LogExportFormat Format { get; }

    Task WriteAsync(IReadOnlyList<LogExportRecord> logs, string outputPath, CancellationToken cancellationToken = default);
}
