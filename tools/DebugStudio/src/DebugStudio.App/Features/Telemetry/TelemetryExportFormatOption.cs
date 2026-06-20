#nullable enable

using DebugStudio.Export.Models;

namespace DebugStudio.App.Features.Telemetry;

/// <summary>
/// telemetry export format の UI 選択肢。
/// 表示名と内部 format、既定拡張子を 1 つに束ねる。
/// </summary>
public sealed class TelemetryExportFormatOption
{
    public TelemetryExportFormatOption(string label, TelemetryExportFormat format, string fileExtension)
    {
        Label = label;
        Format = format;
        FileExtension = fileExtension;
    }

    public string Label { get; }

    public TelemetryExportFormat Format { get; }

    public string FileExtension { get; }
}
