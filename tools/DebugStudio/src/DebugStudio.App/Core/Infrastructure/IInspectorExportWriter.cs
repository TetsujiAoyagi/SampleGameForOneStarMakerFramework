#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;

namespace DebugStudio.App.Core.Infrastructure;

/// <summary>
/// normalized inspector export record を永続化先へ書き出す writer 抽象。
/// </summary>
public interface IInspectorExportWriter
{
    Task WriteAsync(IReadOnlyList<InspectorExportRecord> records, string outputPath, CancellationToken cancellationToken = default);
}
