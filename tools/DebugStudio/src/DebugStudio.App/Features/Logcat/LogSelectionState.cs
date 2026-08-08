#nullable enable


using DebugStudio.App.Core.Mvvm;
using System.Collections.ObjectModel;
using DebugStudio.App.Core.Models;

namespace DebugStudio.App.Features.LogViewer;

internal sealed class LogSelectionState : ObservableObject
{
    private LogViewerListItemViewModel? _selectedLog;
    private string _selectedLogTitle = "No row selected.";
    private string _selectedLogSummary = "Select a log row to inspect its fields and future structured payload surface.";
    private string _selectedLogMessage = "Message body will appear here.";
    private string _selectedLogException = string.Empty;
    private bool _hasSelectedLogException;
    private string _detailEmptyState = "ログを選択すると、本文・例外・メタデータをこのペインで確認できます。";
    private string _structuredPayloadStatus =
        "現行 transport の scalar payload は field map として表示されます。将来 StructuredLogEnvelopeV2 相当が届いたら、このタブへ typed tree を差し込めます。";
    private bool _hasStructuredFields;

    public LogSelectionState()
    {
        DetailFields = new ObservableCollection<LogDetailFieldViewModel>();
        StructuredFields = new ObservableCollection<LogDetailFieldViewModel>();
    }

    public ObservableCollection<LogDetailFieldViewModel> DetailFields { get; }

    public ObservableCollection<LogDetailFieldViewModel> StructuredFields { get; }

    public LogViewerListItemViewModel? SelectedLog
    {
        get => _selectedLog;
        set
        {
            if (SetProperty(ref _selectedLog, value))
            {
                RefreshDetailPane(value);
            }
        }
    }

    public string SelectedLogTitle
    {
        get => _selectedLogTitle;
        private set => SetProperty(ref _selectedLogTitle, value);
    }

    public string SelectedLogSummary
    {
        get => _selectedLogSummary;
        private set => SetProperty(ref _selectedLogSummary, value);
    }

    public string SelectedLogMessage
    {
        get => _selectedLogMessage;
        private set => SetProperty(ref _selectedLogMessage, value);
    }

    public string SelectedLogException
    {
        get => _selectedLogException;
        private set => SetProperty(ref _selectedLogException, value);
    }

    public bool HasSelectedLogException
    {
        get => _hasSelectedLogException;
        private set => SetProperty(ref _hasSelectedLogException, value);
    }

    public string DetailEmptyState
    {
        get => _detailEmptyState;
        private set => SetProperty(ref _detailEmptyState, value);
    }

    public string StructuredPayloadStatus
    {
        get => _structuredPayloadStatus;
        private set => SetProperty(ref _structuredPayloadStatus, value);
    }

    public bool HasStructuredFields
    {
        get => _hasStructuredFields;
        private set => SetProperty(ref _hasStructuredFields, value);
    }

    private void RefreshDetailPane(LogViewerListItemViewModel? selectedLog)
    {
        DetailFields.Clear();
        StructuredFields.Clear();
        HasStructuredFields = false;

        if (selectedLog == null)
        {
            SelectedLogTitle = "No row selected.";
            SelectedLogSummary = "Select a log row to inspect its fields and future structured payload surface.";
            SelectedLogMessage = "Message body will appear here.";
            SelectedLogException = string.Empty;
            HasSelectedLogException = false;
            DetailEmptyState = "ログを選択すると、本文・例外・メタデータをこのペインで確認できます。";
            StructuredPayloadStatus =
                "現行 transport の scalar payload は field map として表示されます。将来 StructuredLogEnvelopeV2 相当が届いたら、このタブへ typed tree を差し込めます。";
            return;
        }

        var record = selectedLog.Record;
        SelectedLogTitle = $"{record.KindText}  •  {record.Category}";
        SelectedLogSummary = $"#{record.SequenceNumber}  •  {record.TimestampText}  •  {record.ApplicationName}";
        SelectedLogMessage = record.Message;
        var hasException = !string.IsNullOrWhiteSpace(record.Exception);
        SelectedLogException = hasException ? record.Exception! : string.Empty;
        HasSelectedLogException = hasException;
        DetailEmptyState = "選択中エントリの本文・例外・メタデータを表示しています。将来はここへ structured payload drill-down も追加します。";
        PopulateStructuredFields(record);
        StructuredPayloadStatus = HasStructuredFields
            ? $"現行 V1 envelope を {StructuredFields.Count} 個の field path に正規化して表示しています。StructuredLogEnvelopeV2 導入時は同じペインに typed tree を追加できます。"
            : "現行 transport の scalar payload は受信していますが、表示できる field path はありません。";

        AddDetailField("Sequence", record.SequenceNumber.ToString(), "store 上での受信順序");
        AddDetailField("Timestamp", record.TimestampText, "transport の unix time から整形");
        AddDetailField("Kind", record.KindText, "LogEntryKind と raw level の表示");
        AddDetailField("Category", record.Category, "logger/category 名");
        AddDetailField("Application", record.ApplicationName, "送信側 application 名");
        AddDetailField("Event", BuildEventText(record), "event id / name");
        AddDetailField("Thread", BuildThreadText(record), "thread id / name");
        AddDetailField("Member", record.MemberName ?? "n/a", "呼び出し member 名");
        AddDetailField("Source", BuildSourceText(record), "file path / line number");
        AddDetailField("Exception", hasException ? "present" : "none", "本文は下段テキスト領域へ表示");
    }

    private void AddDetailField(string label, string value, string hint)
    {
        DetailFields.Add(new LogDetailFieldViewModel(label, value, hint));
    }

    private void AddStructuredField(string path, string value, string hint)
    {
        StructuredFields.Add(new LogDetailFieldViewModel(path, value, hint));
    }

    private void PopulateStructuredFields(LogRecord record)
    {
        AddStructuredField("schema.version", record.SchemaVersion.ToString(), "transport contract version");
        AddStructuredField("application.name", record.ApplicationName, "sender application identity");
        AddStructuredField("timestamp.unixTimeMs", record.TimestampUnixTimeMilliseconds.ToString(), "wire payload raw timestamp");
        AddStructuredField("level.kind", record.KindText, "normalized LogEntryKind view");
        AddStructuredField("level.raw", record.RawLogLevel.ToString(), "transport raw log level integer");
        AddStructuredField("category.name", record.Category, "logger/category");
        AddStructuredField("event.id", record.EventId.ToString(), "event correlation id");
        AddStructuredField("payload.message", record.Message, "rendered message body");

        if (!string.IsNullOrWhiteSpace(record.EventName))
        {
            AddStructuredField("event.name", record.EventName, "optional event name");
        }

        AddStructuredField("thread.id", record.ThreadId.ToString(), "managed thread id");
        if (!string.IsNullOrWhiteSpace(record.ThreadName))
        {
            AddStructuredField("thread.name", record.ThreadName, "optional thread name");
        }

        if (!string.IsNullOrWhiteSpace(record.MemberName))
        {
            AddStructuredField("source.member", record.MemberName, "caller member captured by sender");
        }

        if (!string.IsNullOrWhiteSpace(record.FilePath))
        {
            AddStructuredField("source.file", record.FilePath, "caller file path captured by sender");
            AddStructuredField("source.line", record.LineNumber.ToString(), "caller line number");
        }

        if (!string.IsNullOrWhiteSpace(record.Exception))
        {
            AddStructuredField("payload.exception", record.Exception, "exception text payload");
        }

        HasStructuredFields = StructuredFields.Count > 0;
    }

    private static string BuildEventText(LogRecord record)
    {
        return record.EventName is { Length: > 0 }
            ? $"{record.EventId} / {record.EventName}"
            : record.EventId.ToString();
    }

    private static string BuildThreadText(LogRecord record)
    {
        return record.ThreadName is { Length: > 0 }
            ? $"{record.ThreadName} (tid:{record.ThreadId})"
            : $"tid:{record.ThreadId}";
    }

    private static string BuildSourceText(LogRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.FilePath))
        {
            return $"{record.FilePath}:{record.LineNumber}";
        }

        return "n/a";
    }
}
