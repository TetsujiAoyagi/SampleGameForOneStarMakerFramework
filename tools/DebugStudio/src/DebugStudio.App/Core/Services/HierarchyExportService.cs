#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Infrastructure;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Stores;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// retained hierarchy を normalized export record へ変換して永続化する app service。
/// </summary>
public sealed class HierarchyExportService
{
    private readonly HierarchyStore _hierarchyStore;
    private readonly IHierarchyExportWriter _writer;

    public HierarchyExportService(HierarchyStore hierarchyStore, IHierarchyExportWriter writer)
    {
        _hierarchyStore = hierarchyStore ?? throw new ArgumentNullException(nameof(hierarchyStore));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public Task ExportAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var retainedSnapshot = _hierarchyStore.GetRetainedSnapshot();
        var state = retainedSnapshot.State;
        if (retainedSnapshot.Nodes.Count == 0)
        {
            throw new InvalidOperationException("Hierarchy snapshot is empty.");
        }

        var records = new List<HierarchyExportRecord>(retainedSnapshot.Nodes.Count);

        for (var index = 0; index < retainedSnapshot.Nodes.Count; index++)
        {
            var node = retainedSnapshot.Nodes[index];
            records.Add(new HierarchyExportRecord
            {
                TimestampUtc = FormatTimestampUtc(state.CapturedAtUnixTimeMilliseconds),
                TimestampUnixTimeMilliseconds = state.CapturedAtUnixTimeMilliseconds,
                ScopeName = state.ScopeName,
                Revision = state.Revision,
                SelectedNodeId = state.SelectedNodeId,
                NodeId = node.NodeId,
                ParentId = node.ParentId,
                TypeId = node.TypeId,
                TypeName = node.TypeName,
                Name = node.Name,
                Depth = node.Depth,
                SiblingIndex = node.SiblingIndex,
                ChildCount = node.ChildCount,
                TraversalIndex = node.TraversalIndex,
                Flags = node.Flags.ToString(),
            });
        }

        return _writer.WriteAsync(records, outputPath, cancellationToken);
    }

    private static string FormatTimestampUtc(long unixTimeMilliseconds)
    {
        if (unixTimeMilliseconds <= 0)
        {
            return "1970-01-01T00:00:00.0000000Z";
        }

        try
        {
            return DateTimeOffset
                .FromUnixTimeMilliseconds(unixTimeMilliseconds)
                .UtcDateTime
                .ToString("O", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "1970-01-01T00:00:00.0000000Z";
        }
    }
}
