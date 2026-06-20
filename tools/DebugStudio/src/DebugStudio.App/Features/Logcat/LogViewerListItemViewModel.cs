#nullable enable

using System.Windows.Media;
using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Schema;

namespace DebugStudio.App.Features.LogViewer;

/// <summary>
/// log list 1 行分の見た目をまとめる薄い adapter。
/// raw model を直接 XAML へ晒しすぎると、表示密度や badge 表現の変更で model へ UI 都合が混ざるため分離する。
/// </summary>
public sealed class LogViewerListItemViewModel
{
    private static readonly Brush TraceBrush = CreateBrush("#FFECEFF4");
    private static readonly Brush TraceForegroundBrush = CreateBrush("#FF374151");
    private static readonly Brush DebugBrush = CreateBrush("#FFDBEAFE");
    private static readonly Brush DebugForegroundBrush = CreateBrush("#FF1D4ED8");
    private static readonly Brush InformationBrush = CreateBrush("#FFD1FAE5");
    private static readonly Brush InformationForegroundBrush = CreateBrush("#FF065F46");
    private static readonly Brush WarningBrush = CreateBrush("#FFFEF3C7");
    private static readonly Brush WarningForegroundBrush = CreateBrush("#FF92400E");
    private static readonly Brush ErrorBrush = CreateBrush("#FFFEE2E2");
    private static readonly Brush ErrorForegroundBrush = CreateBrush("#FFB91C1C");
    private static readonly Brush CriticalBrush = CreateBrush("#FFFCE7F3");
    private static readonly Brush CriticalForegroundBrush = CreateBrush("#FF9D174D");
    private static readonly Brush DefaultBrush = CreateBrush("#FFE5E7EB");
    private static readonly Brush DefaultForegroundBrush = CreateBrush("#FF1F2937");

    public LogViewerListItemViewModel(LogRecord record)
    {
        Record = record;
    }

    public LogRecord Record { get; }

    public long SequenceNumber => Record.SequenceNumber;

    public string TimestampText => Record.TimestampText;

    public string KindText => Record.KindText;

    public string Category => Record.Category;

    public string Message => Record.Message;

    public string SecondaryLine =>
        $"#{Record.SequenceNumber}  •  {Record.ApplicationName}  •  {BuildThreadText()}  •  {BuildLocationText()}";

    public bool HasException => !string.IsNullOrWhiteSpace(Record.Exception);

    public string ExceptionPreview =>
        HasException
            ? $"Exception: {Record.Exception}"
            : "Exception: none";

    public Brush KindBadgeBackground => Record.Kind switch
    {
        LogEntryKind.Trace => TraceBrush,
        LogEntryKind.Debug => DebugBrush,
        LogEntryKind.Information => InformationBrush,
        LogEntryKind.Warning => WarningBrush,
        LogEntryKind.Error => ErrorBrush,
        LogEntryKind.Critical => CriticalBrush,
        _ => DefaultBrush,
    };

    public Brush KindBadgeForeground => Record.Kind switch
    {
        LogEntryKind.Trace => TraceForegroundBrush,
        LogEntryKind.Debug => DebugForegroundBrush,
        LogEntryKind.Information => InformationForegroundBrush,
        LogEntryKind.Warning => WarningForegroundBrush,
        LogEntryKind.Error => ErrorForegroundBrush,
        LogEntryKind.Critical => CriticalForegroundBrush,
        _ => DefaultForegroundBrush,
    };

    private string BuildThreadText()
    {
        if (!string.IsNullOrWhiteSpace(Record.ThreadName))
        {
            return $"{Record.ThreadName} (tid:{Record.ThreadId})";
        }

        return $"tid:{Record.ThreadId}";
    }

    private string BuildLocationText()
    {
        if (!string.IsNullOrWhiteSpace(Record.MemberName) && !string.IsNullOrWhiteSpace(Record.FilePath))
        {
            return $"{Record.MemberName} @ {Record.FilePath}:{Record.LineNumber}";
        }

        if (!string.IsNullOrWhiteSpace(Record.FilePath))
        {
            return $"{Record.FilePath}:{Record.LineNumber}";
        }

        if (!string.IsNullOrWhiteSpace(Record.MemberName))
        {
            return Record.MemberName;
        }

        return "source: n/a";
    }

    private static Brush CreateBrush(string color)
    {
        return (SolidColorBrush)new BrushConverter().ConvertFromString(color)!;
    }
}
