#nullable enable

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DebugStudio.App.Core.Services;
using DebugStudio.App.Core.Stores;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Contracts.Schema;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Tests;

/// <summary>
/// SessionMessageRouter からの telemetry 受信を rolling file へ永続化できることを検証する。
/// </summary>
public sealed class TelemetryPersistenceServiceTests
{
    [Fact]
    public async Task RouteTelemetryMessage_受信telemetryがDispose後にNDJSONへ書き出される()
    {
        var directory = CreateTempDirectory();
        var messageRouter = CreateMessageRouter();

        try
        {
            await using var persistence = new TelemetryPersistenceService(
                messageRouter,
                new RollingTelemetryFileWriter(directory, maxFileSizeBytes: 4096),
                new TelemetrySessionAttributesStore());

            messageRouter.RouteTelemetryMessage(CreateTelemetryEnvelope("persisted-from-router"));

            await persistence.DisposeAsync();

            var lines = await ReadAllLinesAsync(directory);
            Assert.Single(lines);
            Assert.Contains("persisted-from-router", lines[0], StringComparison.Ordinal);

            using var document = JsonDocument.Parse(lines[0]);
            Assert.Equal("telemetry", document.RootElement.GetProperty("stream").GetString());
            Assert.Equal("debugstudio", document.RootElement.GetProperty("source").GetString());
            Assert.True(document.RootElement.TryGetProperty("@timestamp", out _));
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    [Fact]
    public async Task DisposeAsync_未処理queueをflushしてから終了する()
    {
        var directory = CreateTempDirectory();
        var messageRouter = CreateMessageRouter();
        const int recordCount = 4;

        try
        {
            var writer = new RollingTelemetryFileWriter(directory, maxFileSizeBytes: 4096);
            var persistence = new TelemetryPersistenceService(messageRouter, writer, new TelemetrySessionAttributesStore());

            for (var index = 0; index < recordCount; index++)
            {
                messageRouter.RouteTelemetryMessage(CreateTelemetryEnvelope($"flush-{index}"));
            }

            await persistence.DisposeAsync();

            var lines = await ReadAllLinesAsync(directory);
            Assert.Equal(recordCount, lines.Length);
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    [Fact]
    public async Task RouteTelemetryMessage_NDJSONのstreamと主要fieldが手動Export契約と一致する()
    {
        var directory = CreateTempDirectory();
        var messageRouter = CreateMessageRouter();
        var endTimestamp = new DateTime(2026, 4, 29, 1, 0, 2, DateTimeKind.Utc);

        try
        {
            await using var persistence = new TelemetryPersistenceService(
                messageRouter,
                new RollingTelemetryFileWriter(directory, maxFileSizeBytes: 4096),
                new TelemetrySessionAttributesStore());

            messageRouter.RouteTelemetryMessage(new DebugTelemetryEnvelopeV1
            {
                Name = "load-scene",
                EndTimestampUtcTicks = endTimestamp.Ticks,
                ElapsedMs = 12.5,
                IsSuccess = true,
                TraceId = 10,
                SpanId = 11,
                TagBits = (int)(DebugTelemetryTagBits.CpuTimeOver | DebugTelemetryTagBits.AllocSpike),
            });

            await persistence.DisposeAsync();

            var lines = await ReadAllLinesAsync(directory);
            using var document = JsonDocument.Parse(Assert.Single(lines));
            var root = document.RootElement;

            Assert.Equal("telemetry", root.GetProperty("stream").GetString());
            Assert.Equal("load-scene", root.GetProperty("name").GetString());
            Assert.Equal(12.5, root.GetProperty("elapsedMs").GetDouble());
            Assert.True(root.GetProperty("isSuccess").GetBoolean());
            Assert.Equal(10, root.GetProperty("traceId").GetInt64());
            Assert.Equal(11, root.GetProperty("spanId").GetInt64());
            Assert.Equal(
                (int)(DebugTelemetryTagBits.CpuTimeOver | DebugTelemetryTagBits.AllocSpike),
                root.GetProperty("tagBits").GetInt32());
            Assert.Equal("CpuTimeOver", root.GetProperty("tags")[0].GetString());
            Assert.Equal("AllocSpike", root.GetProperty("tags")[1].GetString());
        }
        finally
        {
            CleanupDirectory(directory);
        }
    }

    private static SessionMessageRouter CreateMessageRouter()
    {
        var logStore = new LogStore(capacity: 64);
        var hierarchyStore = new HierarchyStore();
        var inspectorStore = new InspectorStore();
        var telemetryStore = new TelemetryStore();
        var commandStore = new CommandStore();
        var capabilityHandshakeService = new CapabilityHandshakeService();
        var capabilityStateStore = new CapabilityStateStore(capabilityHandshakeService.LocalSupportedCapabilities);

        return new SessionMessageRouter(
            logStore,
            hierarchyStore,
            inspectorStore,
            telemetryStore,
            commandStore,
            capabilityStateStore,
            new TelemetrySessionAttributesStore());
    }

    private static DebugTelemetryEnvelopeV1 CreateTelemetryEnvelope(string name)
    {
        return new DebugTelemetryEnvelopeV1
        {
            Name = name,
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            ElapsedMs = 1.0,
            IsSuccess = true,
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task<string[]> ReadAllLinesAsync(string directory)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var filePath = Directory
            .GetFiles(directory, $"debugstudio-telemetry_{today}_*.ndjson")
            .Single();
        return await File.ReadAllLinesAsync(filePath);
    }

    private static void CleanupDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
