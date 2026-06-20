#nullable enable

using DebugStudio.Export.Models;

namespace DebugStudio.App.Features.LogViewer;

/// <summary>
/// export format の UI 選択肢。
/// 表示文言と内部 format を束ねる。
/// </summary>
public sealed class LogExportFormatOption
{
    public LogExportFormatOption(string label, LogExportFormat format, string fileExtension)
    {
        Label = label;
        Format = format;
        FileExtension = fileExtension;
    }

    public string Label { get; }

    public LogExportFormat Format { get; }

    public string FileExtension { get; }
}
