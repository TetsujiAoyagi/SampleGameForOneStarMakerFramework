#nullable enable

namespace DebugStudio.App.Features.Commands;

/// <summary>
/// recent command list 1 行分の表示モデル。
/// raw record をそのまま UI へ露出せず、見たい列だけへ整形して持たせる。
/// </summary>
public sealed class CommandHistoryItemViewModel
{
    public CommandHistoryItemViewModel(
        string state,
        string requestId,
        string commandType,
        string summary,
        string timing)
    {
        State = state;
        RequestId = requestId;
        CommandType = commandType;
        Summary = summary;
        Timing = timing;
    }

    public string State { get; }

    public string RequestId { get; }

    public string CommandType { get; }

    public string Summary { get; }

    public string Timing { get; }
}
