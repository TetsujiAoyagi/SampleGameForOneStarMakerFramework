#nullable enable

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Models;

namespace DebugStudio.App.Core.Infrastructure;

/// <summary>
/// normalized hierarchy export record を永続化先へ書き出す writer 抽象。
/// </summary>
public interface IHierarchyExportWriter
{
    Task WriteAsync(IReadOnlyList<HierarchyExportRecord> records, string outputPath, CancellationToken cancellationToken = default);
}
