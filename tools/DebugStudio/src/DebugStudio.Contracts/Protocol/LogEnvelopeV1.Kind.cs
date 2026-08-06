#nullable enable

using DebugStudio.Contracts.Schema;

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// DS 側固有の表示用 Kind ビュー（手書き partial）。
/// wire には載せない（[IgnoreMember]）。
/// </summary>
public sealed partial class LogEnvelopeV1
{
    /// <summary>
    /// wire 上の int level を、共有 schema で再利用できる kind へ写した読み取り専用ビュー。
    /// MessagePack key を増やさず transport 互換性を維持するため、serialize 対象にはしない。
    /// </summary>
    [MessagePack.IgnoreMember]
    public LogEntryKind Kind => LogLevel switch
    {
        0 => LogEntryKind.Trace,
        1 => LogEntryKind.Debug,
        2 => LogEntryKind.Information,
        3 => LogEntryKind.Warning,
        4 => LogEntryKind.Error,
        5 => LogEntryKind.Critical,
        6 => LogEntryKind.None,
        _ => LogEntryKind.Unknown,
    };
}
