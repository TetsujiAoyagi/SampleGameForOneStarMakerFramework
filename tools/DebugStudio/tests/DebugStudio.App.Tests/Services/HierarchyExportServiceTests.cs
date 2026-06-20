#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Infrastructure;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests.Services;

/// <summary>
/// hierarchy export の normalized record と path policy を固定する。
/// </summary>
public sealed class HierarchyExportServiceTests
{
    [Fact]
    public async Task ExportAsync_保持中hierarchyをnode単位で平坦化する()
    {
        var store = new HierarchyStore();
        store.ApplySnapshot(new HierarchySnapshotEnvelopeV1
        {
            Revision = 7,
            CapturedAtUnixTimeMilliseconds = new DateTimeOffset(2026, 4, 29, 2, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            ScopeName = "Loaded Scenes",
            Nodes =
            [
                new HierarchyNodeDtoV1
                {
                    NodeId = 100,
                    ParentId = 0,
                    TypeId = 1,
                    Flags = HierarchyNodeFlags.SceneRoot | HierarchyNodeFlags.ActiveInHierarchy,
                    Depth = 0,
                    SiblingIndex = 0,
                    ChildCount = 1,
                    TraversalIndex = 0,
                    Name = "Root",
                    TypeName = "GameObject",
                },
                new HierarchyNodeDtoV1
                {
                    NodeId = 200,
                    ParentId = 100,
                    TypeId = 1,
                    Flags = HierarchyNodeFlags.ActiveInHierarchy,
                    Depth = 1,
                    SiblingIndex = 0,
                    ChildCount = 0,
                    TraversalIndex = 1,
                    Name = "Child",
                    TypeName = "GameObject",
                },
            ],
        });
        store.SetSelectedNodeId(200);

        var writer = new RecordingHierarchyExportWriter();
        var service = new HierarchyExportService(store, writer);

        await service.ExportAsync(@"C:\exports\hierarchy.ndjson");

        Assert.Equal(@"C:\exports\hierarchy.ndjson", writer.LastOutputPath);
        Assert.Equal(2, writer.LastRecords.Count);
        Assert.Equal(100, writer.LastRecords[0].NodeId);
        Assert.Equal(200, writer.LastRecords[1].NodeId);
        Assert.Equal(200, writer.LastRecords[0].SelectedNodeId);
        Assert.Equal("Loaded Scenes", writer.LastRecords[0].ScopeName);
        Assert.Equal("hierarchy", writer.LastRecords[0].Stream);
    }

    [Fact]
    public void HierarchyExportPathPolicy_専用ディレクトリへtimestamp付きファイルを作る()
    {
        var policy = new HierarchyExportPathPolicy(@"C:\TelemetryRoot");
        var now = new DateTimeOffset(2026, 4, 29, 11, 3, 15, TimeSpan.FromHours(9));

        var path = policy.CreateDefaultPath(now: now);

        Assert.Equal(
            @"C:\TelemetryRoot\hierarchy\2026-04-29\debugstudio-hierarchy-20260429-110315.ndjson",
            path);
    }

    [Fact]
    public async Task ExportAsync_空hierarchyでは空ファイルを作らず失敗する()
    {
        var store = new HierarchyStore();
        var writer = new RecordingHierarchyExportWriter();
        var service = new HierarchyExportService(store, writer);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(@"C:\exports\hierarchy.ndjson"));

        Assert.Contains("empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingHierarchyExportWriter : IHierarchyExportWriter
    {
        public string LastOutputPath { get; private set; } = string.Empty;

        public IReadOnlyList<HierarchyExportRecord> LastRecords { get; private set; } = Array.Empty<HierarchyExportRecord>();

        public Task WriteAsync(IReadOnlyList<HierarchyExportRecord> records, string outputPath, CancellationToken cancellationToken = default)
        {
            LastOutputPath = outputPath;
            LastRecords = records;
            return Task.CompletedTask;
        }
    }
}
