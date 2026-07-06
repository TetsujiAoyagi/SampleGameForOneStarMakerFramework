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
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Tests;

/// <summary>
/// SessionMessageRouter からの log 受信を rolling file へ永続化できることを検証する。
/// </summary>
public sealed class LogPersistenceServiceTests
{
    [Fact]
    public async Task RouteLogMessage_受信logがDispose後にNDJSONへ書き出される()
    {
        var directory = CreateTempDirectory();
        var messageRouter = CreateMessageRouter();

        try
        {
            await using var persistence = new LogPersistenceService(
                messageRouter,
                new RollingLogFileWriter(directory, maxFileSizeBytes: 4096));

            messageRouter.RouteLogMessage(CreateLogEnvelope("persisted-from-router", "Network"));

            await persistence.DisposeAsync();

            var lines = await ReadAllLinesAsync(directory);
            Assert.Single(lines);
            Assert.Contains("persisted-from-router", lines[0], StringComparison.Ordinal);

            using var document = JsonDocument.Parse(lines[0]);
            Assert.Equal("Network", document.RootElement.GetProperty("category").GetString());
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
            var writer = new RollingLogFileWriter(directory, maxFileSizeBytes: 4096);
            var persistence = new LogPersistenceService(messageRouter, writer);

            for (var index = 0; index < recordCount; index++)
            {
                messageRouter.RouteLogMessage(CreateLogEnvelope($"flush-{index}", "Default"));
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
            capabilityStateStore);
    }

    private static LogEnvelopeV1 CreateLogEnvelope(string message, string category)
    {
        return new LogEnvelopeV1
        {
            SchemaVersion = 1,
            ApplicationName = "TestApp",
            TimestampUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Category = category,
            LogLevel = 2,
            EventId = 0,
            Message = message,
            ThreadId = Environment.CurrentManagedThreadId,
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
            .GetFiles(directory, $"debugstudio-logs_{today}_*.ndjson")
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
