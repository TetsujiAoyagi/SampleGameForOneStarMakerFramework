#nullable enable

using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Services;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Tests;

/// <summary>
/// LogQueryService と CapabilityHandshakeService の重要な不変条件を検証。
/// 特にフィルタリング、検索、順序、交渉エンベロープの堅牢性を確認する。
/// </summary>
public sealed class ServiceSafetyTests
{
    #region LogQueryService Tests

    [Fact]
    public void Kind絞り込み_指定Kindのみ返す()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "Test", LogEntryKind.Information, "Info message"),
            CreateLogRecord(2, "Test", LogEntryKind.Warning, "Warning message"),
            CreateLogRecord(3, "Test", LogEntryKind.Error, "Error message"),
            CreateLogRecord(4, "Test", LogEntryKind.Information, "Another info"),
        };

        var options = new LogQueryOptions { Kind = LogEntryKind.Information };

        // Act
        var results = service.Query(records, options);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(LogEntryKind.Information, r.Kind));
    }

    [Fact]
    public void Kind絞り込み_Nullの場合は全件返す()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "Test", LogEntryKind.Information, "Info"),
            CreateLogRecord(2, "Test", LogEntryKind.Warning, "Warn"),
            CreateLogRecord(3, "Test", LogEntryKind.Error, "Error"),
        };

        var options = new LogQueryOptions { Kind = null };

        // Act
        var results = service.Query(records, options);

        // Assert
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public void 検索_大文字小文字を区別しない()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "TestApp", LogEntryKind.Information, "User Login Failed"),
            CreateLogRecord(2, "TestApp", LogEntryKind.Information, "user logout"),
            CreateLogRecord(3, "TestApp", LogEntryKind.Information, "System startup"),
        };

        var options = new LogQueryOptions { SearchText = "USER" };

        // Act
        var results = service.Query(records, options);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Message.Contains("Login", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.Message.Contains("logout", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void 検索_複数フィールドから一致を探す()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "AuthApp", LogEntryKind.Information, "Normal message", category: "Security"),
            CreateLogRecord(2, "DataApp", LogEntryKind.Warning, "Data issue", category: "Database"),
            CreateLogRecord(3, "TestApp", LogEntryKind.Error, "Failed", eventName: "SecurityAlert"),
        };

        var options = new LogQueryOptions { SearchText = "security" };

        // Act
        var results = service.Query(records, options);

        // Assert
        // Category と EventName の両方から見つかる
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Category.Contains("Security", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(results, r => r.EventName?.Contains("Security", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void 検索_ApplicationNameからも一致検出()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "GameEngine", LogEntryKind.Information, "Started"),
            CreateLogRecord(2, "NetworkModule", LogEntryKind.Information, "Connected"),
            CreateLogRecord(3, "AudioEngine", LogEntryKind.Information, "Initialized"),
        };

        var options = new LogQueryOptions { SearchText = "engine" };

        // Act
        var results = service.Query(records, options);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("Engine", r.ApplicationName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void 検索_Exceptionフィールドからも一致検出()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "App", LogEntryKind.Error, "Error occurred", exception: "NullReferenceException at line 42"),
            CreateLogRecord(2, "App", LogEntryKind.Error, "Failed", exception: "IOException: file not found"),
            CreateLogRecord(3, "App", LogEntryKind.Warning, "Warning", exception: null),
        };

        var options = new LogQueryOptions { SearchText = "NullReference" };

        // Act
        var results = service.Query(records, options);

        // Assert
        Assert.Single(results);
        Assert.Contains("NullReference", results[0].Exception, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void 検索_ThreadNameとMemberNameとFilePathからも一致検出()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "App", LogEntryKind.Debug, "Trace", threadName: "MainThread"),
            CreateLogRecord(2, "App", LogEntryKind.Debug, "Trace", memberName: "Initialize"),
            CreateLogRecord(3, "App", LogEntryKind.Debug, "Trace", filePath: "C:\\Source\\Main.cs"),
        };

        // Act - ThreadName
        var resultsThread = service.Query(records, new LogQueryOptions { SearchText = "mainthread" });
        Assert.Single(resultsThread);

        // Act - MemberName
        var resultsMember = service.Query(records, new LogQueryOptions { SearchText = "initialize" });
        Assert.Single(resultsMember);

        // Act - FilePath
        var resultsFile = service.Query(records, new LogQueryOptions { SearchText = "main.cs" });
        Assert.Single(resultsFile);
    }

    [Fact]
    public void 順序_常に降順_最新が先頭()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "App", LogEntryKind.Information, "First", timestampMs: 1000),
            CreateLogRecord(2, "App", LogEntryKind.Information, "Second", timestampMs: 2000),
            CreateLogRecord(3, "App", LogEntryKind.Information, "Third", timestampMs: 3000),
            CreateLogRecord(4, "App", LogEntryKind.Information, "Fourth", timestampMs: 4000),
        };

        // Act
        var results = service.Query(records);

        // Assert
        Assert.Equal(4, results.Count);
        // 降順なので最新のsequence numberが先に来る
        Assert.Equal(4, results[0].SequenceNumber);
        Assert.Equal(3, results[1].SequenceNumber);
        Assert.Equal(2, results[2].SequenceNumber);
        Assert.Equal(1, results[3].SequenceNumber);
    }

    [Fact]
    public void 順序_フィルタ後も降順を保つ()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "App", LogEntryKind.Information, "Info1"),
            CreateLogRecord(2, "App", LogEntryKind.Error, "Error1"),
            CreateLogRecord(3, "App", LogEntryKind.Information, "Info2"),
            CreateLogRecord(4, "App", LogEntryKind.Error, "Error2"),
            CreateLogRecord(5, "App", LogEntryKind.Information, "Info3"),
        };

        var options = new LogQueryOptions { Kind = LogEntryKind.Information };

        // Act
        var results = service.Query(records, options);

        // Assert
        Assert.Equal(3, results.Count);
        Assert.Equal(5, results[0].SequenceNumber);
        Assert.Equal(3, results[1].SequenceNumber);
        Assert.Equal(1, results[2].SequenceNumber);
    }

    [Fact]
    public void 検索とKind絞り込み_両方適用される()
    {
        // Arrange
        var service = new LogQueryService();
        var records = new List<LogRecord>
        {
            CreateLogRecord(1, "App", LogEntryKind.Information, "User action completed"),
            CreateLogRecord(2, "App", LogEntryKind.Warning, "User action warning"),
            CreateLogRecord(3, "App", LogEntryKind.Error, "System error"),
            CreateLogRecord(4, "App", LogEntryKind.Information, "System status"),
        };

        var options = new LogQueryOptions
        {
            Kind = LogEntryKind.Information,
            SearchText = "user"
        };

        // Act
        var results = service.Query(records, options);

        // Assert
        Assert.Single(results);
        Assert.Equal(LogEntryKind.Information, results[0].Kind);
        Assert.Contains("User", results[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region CapabilityHandshakeService Tests

    [Fact]
    public void HelloEnvelope_SupportedCapabilitiesが設定されている()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var hello = service.CreateHello();

        // Assert
        Assert.NotEqual(DebugStudioCapability.None, hello.SupportedCapabilities);
        // 必須 capability の確認
        Assert.True(hello.SupportedCapabilities.HasFlag(DebugStudioCapability.CapabilityNegotiation));
        Assert.True(hello.SupportedCapabilities.HasFlag(DebugStudioCapability.LogStream));
    }

    [Fact]
    public void HelloEnvelope_LocalSupportedCapabilitiesと一致()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var hello = service.CreateHello();

        // Assert
        Assert.Equal(service.LocalSupportedCapabilities, hello.SupportedCapabilities);
    }

    [Fact]
    public void HelloEnvelope_SchemaVersionRangeが有効()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var hello = service.CreateHello();

        // Assert
        Assert.True(hello.MinSchemaVersion > 0, "MinSchemaVersion は正の値であるべき");
        Assert.True(hello.MaxSchemaVersion >= hello.MinSchemaVersion, "MaxSchemaVersion は MinSchemaVersion 以上であるべき");
    }

    [Fact]
    public void HelloEnvelope_ClientInstanceIdは毎回同じ値()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var hello1 = service.CreateHello();
        var hello2 = service.CreateHello();

        // Assert
        // 同じサービスインスタンスからは同じ ClientInstanceId が返される
        Assert.Equal(hello1.ClientInstanceId, hello2.ClientInstanceId);
        Assert.NotEmpty(hello1.ClientInstanceId);
    }

    [Fact]
    public void HelloEnvelope_異なるサービスインスタンスは異なるClientInstanceId()
    {
        // Arrange
        var service1 = new CapabilityHandshakeService();
        var service2 = new CapabilityHandshakeService();

        // Act
        var hello1 = service1.CreateHello();
        var hello2 = service2.CreateHello();

        // Assert
        // 異なるサービスインスタンスは異なる ClientInstanceId を持つ
        Assert.NotEqual(hello1.ClientInstanceId, hello2.ClientInstanceId);
    }

    [Fact]
    public void HelloEnvelope_SupportedMessageTypesが空でない()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var hello = service.CreateHello();

        // Assert
        Assert.NotNull(hello.SupportedMessageTypes);
        Assert.NotEmpty(hello.SupportedMessageTypes);
    }

    [Fact]
    public void HelloEnvelope_SupportedMessageTypesに必須タイプが含まれる()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var hello = service.CreateHello();

        // Assert
        // 必須のメッセージタイプが含まれていることを確認
        Assert.Contains((int)DebugSocketMessageType.Log, hello.SupportedMessageTypes);
        Assert.Contains((int)DebugSocketMessageType.CapabilityHello, hello.SupportedMessageTypes);
        Assert.Contains((int)DebugSocketMessageType.CapabilityWelcome, hello.SupportedMessageTypes);
    }

    [Fact]
    public void HelloEnvelope_ClientNameが設定されている()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var hello = service.CreateHello();

        // Assert
        Assert.NotEmpty(hello.ClientName);
        Assert.Equal("DebugStudio.App", hello.ClientName);
    }

    [Fact]
    public void HelloEnvelope_SchemaVersionはデフォルト値()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var hello = service.CreateHello();

        // Assert
        Assert.Equal(1, hello.SchemaVersion);
    }

    [Fact]
    public void HelloEnvelope_複数回生成しても安定した内容()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var hello1 = service.CreateHello();
        var hello2 = service.CreateHello();
        var hello3 = service.CreateHello();

        // Assert
        // 同じサービスインスタンスから生成されたエンベロープは安定している
        Assert.Equal(hello1.ClientName, hello2.ClientName);
        Assert.Equal(hello1.ClientName, hello3.ClientName);
        Assert.Equal(hello1.ClientInstanceId, hello2.ClientInstanceId);
        Assert.Equal(hello1.ClientInstanceId, hello3.ClientInstanceId);
        Assert.Equal(hello1.MinSchemaVersion, hello2.MinSchemaVersion);
        Assert.Equal(hello1.MaxSchemaVersion, hello2.MaxSchemaVersion);
        Assert.Equal(hello1.SupportedCapabilities, hello2.SupportedCapabilities);
    }

    [Fact]
    public void LocalSupportedCapabilities_主要Capabilityを含む()
    {
        // Arrange
        var service = new CapabilityHandshakeService();

        // Act
        var capabilities = service.LocalSupportedCapabilities;

        // Assert
        // 主要な capability が含まれていることを確認
        Assert.True(capabilities.HasFlag(DebugStudioCapability.CapabilityNegotiation));
        Assert.True(capabilities.HasFlag(DebugStudioCapability.LogStream));
        Assert.True(capabilities.HasFlag(DebugStudioCapability.TelemetryStream));
        Assert.True(capabilities.HasFlag(DebugStudioCapability.ServiceStatusStream));
        Assert.True(capabilities.HasFlag(DebugStudioCapability.DebugCommand));
        Assert.True(capabilities.HasFlag(DebugStudioCapability.CommandResult));
        Assert.True(capabilities.HasFlag(DebugStudioCapability.HierarchySnapshot));
        Assert.True(capabilities.HasFlag(DebugStudioCapability.HierarchyDelta));
        Assert.True(capabilities.HasFlag(DebugStudioCapability.InspectorQuery));
        Assert.True(capabilities.HasFlag(DebugStudioCapability.InspectorDetail));
    }

    #endregion

    #region Helper Methods

    private static LogRecord CreateLogRecord(
        long sequenceNumber,
        string applicationName,
        LogEntryKind kind,
        string message,
        string category = "TestCategory",
        string? eventName = null,
        string? exception = null,
        string? threadName = null,
        string? memberName = null,
        string? filePath = null,
        long timestampMs = 0)
    {
        // LogEnvelopeV1.Kind は LogLevel から計算される読み取り専用プロパティなので、
        // LogLevel に対応する値を設定する必要がある
        int logLevel = kind switch
        {
            LogEntryKind.Trace => 0,
            LogEntryKind.Debug => 1,
            LogEntryKind.Information => 2,
            LogEntryKind.Warning => 3,
            LogEntryKind.Error => 4,
            LogEntryKind.Critical => 5,
            LogEntryKind.None => 6,
            _ => 2, // デフォルトは Information
        };

        var envelope = new LogEnvelopeV1
        {
            ApplicationName = applicationName,
            TimestampUnixTimeMilliseconds = timestampMs > 0 ? timestampMs : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Category = category,
            LogLevel = logLevel,
            EventId = 0,
            EventName = eventName,
            Message = message,
            Exception = exception,
            ThreadId = 1,
            ThreadName = threadName,
            MemberName = memberName,
            FilePath = filePath,
            LineNumber = 0,
        };

        return LogRecord.FromEnvelope(sequenceNumber, envelope);
    }

    #endregion
}
