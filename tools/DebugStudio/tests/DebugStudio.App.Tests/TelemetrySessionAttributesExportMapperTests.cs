#nullable enable

using DebugStudio.App.Core.Services;
using DebugStudio.Contracts.Protocol;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.App.Tests;

/// <summary>
/// セッション属性が mapper → NDJSON へ正しく載る／欠測時はキー省略されることを固定する。
/// </summary>
public sealed class TelemetrySessionAttributesExportMapperTests
{
    [Fact]
    public void TelemetryExportMapper_属性ありで5キーがNDJSONに出る()
    {
        var telemetry = CreateTelemetry("sess-attrs");
        var attributes = new TelemetrySessionAttributes(
            BuildVersion: "1.4.2",
            Platform: "WindowsPlayer",
            DeviceModel: "PC",
            OsVersion: "Windows 11",
            EngineVersion: "6000.5.0f1");

        var exportRecord = TelemetryRecordExportMapper.ToExportRecord(telemetry, attributes);
        var ndjson = NdjsonTelemetryRecordSerializer.Serialize(exportRecord);

        Assert.Contains("\"buildVersion\":\"1.4.2\"", ndjson, StringComparison.Ordinal);
        Assert.Contains("\"platform\":\"WindowsPlayer\"", ndjson, StringComparison.Ordinal);
        Assert.Contains("\"deviceModel\":\"PC\"", ndjson, StringComparison.Ordinal);
        Assert.Contains("\"osVersion\":\"Windows 11\"", ndjson, StringComparison.Ordinal);
        Assert.Contains("\"engineVersion\":\"6000.5.0f1\"", ndjson, StringComparison.Ordinal);
    }

    [Fact]
    public void TelemetryExportMapper_属性nullのときキー自体が出ない()
    {
        var telemetry = CreateTelemetry("sess-no-attrs");

        var exportRecord = TelemetryRecordExportMapper.ToExportRecord(telemetry, sessionAttributes: null);
        var ndjson = NdjsonTelemetryRecordSerializer.Serialize(exportRecord);

        Assert.DoesNotContain("buildVersion", ndjson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"platform\"", ndjson, StringComparison.Ordinal);
        Assert.DoesNotContain("deviceModel", ndjson, StringComparison.Ordinal);
        Assert.DoesNotContain("osVersion", ndjson, StringComparison.Ordinal);
        Assert.DoesNotContain("engineVersion", ndjson, StringComparison.Ordinal);
    }

    [Fact]
    public void TelemetryExportMapper_属性はあるが値が空文字ならキー自体が出ない()
    {
        // 旧 Unity（field 9〜13 欠測）や Capture() 前に Welcome を送った producer では、
        // 属性オブジェクトは引けるが中身が空文字になる。null と同じくキーごと省略する。
        var telemetry = CreateTelemetry("sess-empty-attrs");
        var attributes = new TelemetrySessionAttributes(
            BuildVersion: string.Empty,
            Platform: string.Empty,
            DeviceModel: string.Empty,
            OsVersion: string.Empty,
            EngineVersion: string.Empty);

        var exportRecord = TelemetryRecordExportMapper.ToExportRecord(telemetry, attributes);
        var ndjson = NdjsonTelemetryRecordSerializer.Serialize(exportRecord);

        Assert.DoesNotContain("buildVersion", ndjson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"platform\"", ndjson, StringComparison.Ordinal);
        Assert.DoesNotContain("deviceModel", ndjson, StringComparison.Ordinal);
        Assert.DoesNotContain("osVersion", ndjson, StringComparison.Ordinal);
        Assert.DoesNotContain("engineVersion", ndjson, StringComparison.Ordinal);
    }

    private static DebugTelemetryEnvelopeV1 CreateTelemetry(string sessionId)
    {
        return new DebugTelemetryEnvelopeV1
        {
            Name = "SceneLoad",
            EndTimestampUtcTicks = DateTime.UtcNow.Ticks,
            ElapsedMs = 1.0,
            IsSuccess = true,
            SessionId = sessionId,
        };
    }
}
