#nullable enable

namespace DebugStudio.App.Core.Models;

/// <summary>
/// request と result を requestId 単位で相関した後の app model。
///
/// <para>
/// protocol 上では command request と result が別 envelope だが、
/// UI は「1 件の command がどう終わったか」を一覧で見たい。
/// そのため correlation 後の状態をこの record に畳んで保持する。
/// </para>
/// </summary>
public readonly record struct CommandDispatchRecord(
    long SequenceNumber,
    string RequestId,
    string CommandType,
    string RequestPayloadJson,
    CommandDispatchState State,
    string StatusMessage,
    string ResultPayloadJson,
    long StartedAtUnixTimeMilliseconds,
    long? CompletedAtUnixTimeMilliseconds);
