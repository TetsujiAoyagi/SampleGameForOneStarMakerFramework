#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Infrastructure;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Tests.Services;

/// <summary>
/// inspector export の normalized record と path policy を固定する。
/// </summary>
public sealed class InspectorExportServiceTests
{
    [Fact]
    public async Task ExportAsync_保持中inspectorをproperty単位で平坦化する()
    {
        var store = new InspectorStore();
        store.BeginQuery(100, "Root", "GameObject");
        store.ApplyDetail(new InspectorDetailEnvelopeV1
        {
            TargetId = 100,
            TargetName = "Root",
            TargetTypeName = "GameObject",
            Revision = 9,
            CapturedAtUnixTimeMilliseconds = new DateTimeOffset(2026, 4, 29, 2, 5, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(),
            State = InspectorDetailState.Ready,
            Message = "Inspector detail captured.",
            Sections =
            [
                new InspectorSectionDtoV1
                {
                    SectionId = 1,
                    Kind = InspectorSectionKind.Header,
                    DisplayName = "GameObject",
                    TypeName = "GameObject",
                    Properties =
                    [
                        new InspectorPropertyDtoV1
                        {
                            PropertyId = 1,
                            DisplayName = "Name",
                            ValueTypeId = 5,
                            ValueText = "Root",
                            Path = "GameObject.Name",
                        },
                    ],
                },
            ],
        });

        var writer = new RecordingInspectorExportWriter();
        var service = new InspectorExportService(store, writer);

        await service.ExportAsync(@"C:\exports\inspector.ndjson");

        Assert.Equal(@"C:\exports\inspector.ndjson", writer.LastOutputPath);
        Assert.Single(writer.LastRecords);
        Assert.Equal(100, writer.LastRecords[0].TargetId);
        Assert.Equal("Ready", writer.LastRecords[0].State);
        Assert.Equal("GameObject", writer.LastRecords[0].SectionDisplayName);
        Assert.Equal("Name", writer.LastRecords[0].PropertyName);
        Assert.Equal("inspector", writer.LastRecords[0].Stream);
    }

    [Fact]
    public async Task ExportAsync_空文書でも状態行を1件残す()
    {
        var store = new InspectorStore();
        store.SetUnsupported(100, "Root", "GameObject", "Not negotiated.");

        var writer = new RecordingInspectorExportWriter();
        var service = new InspectorExportService(store, writer);

        await service.ExportAsync(@"C:\exports\inspector.ndjson");

        Assert.Single(writer.LastRecords);
        Assert.Equal("Unsupported", writer.LastRecords[0].State);
        Assert.Equal("Not negotiated.", writer.LastRecords[0].Message);
        Assert.Null(writer.LastRecords[0].PropertyName);
    }

    [Fact]
    public void InspectorExportPathPolicy_専用ディレクトリへtimestamp付きファイルを作る()
    {
        var policy = new InspectorExportPathPolicy(@"C:\TelemetryRoot");
        var now = new DateTimeOffset(2026, 4, 29, 11, 4, 20, TimeSpan.FromHours(9));

        var path = policy.CreateDefaultPath(now: now);

        Assert.Equal(
            @"C:\TelemetryRoot\inspector\2026-04-29\debugstudio-inspector-20260429-110420.ndjson",
            path);
    }

    [Fact]
    public async Task ExportAsync_未選択状態では失敗する()
    {
        var store = new InspectorStore();
        var writer = new RecordingInspectorExportWriter();
        var service = new InspectorExportService(store, writer);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(@"C:\exports\inspector.ndjson"));

        Assert.Contains("no selected target", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RecordingInspectorExportWriter : IInspectorExportWriter
    {
        public string LastOutputPath { get; private set; } = string.Empty;

        public IReadOnlyList<Core.Models.InspectorExportRecord> LastRecords { get; private set; } = Array.Empty<Core.Models.InspectorExportRecord>();

        public Task WriteAsync(IReadOnlyList<Core.Models.InspectorExportRecord> records, string outputPath, CancellationToken cancellationToken = default)
        {
            LastOutputPath = outputPath;
            LastRecords = records;
            return Task.CompletedTask;
        }
    }
}
