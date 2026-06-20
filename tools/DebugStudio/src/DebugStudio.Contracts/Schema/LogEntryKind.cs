#nullable enable

namespace DebugStudio.Contracts.Schema;

/// <summary>
/// log を UI 文言ではなく共有 schema の観点で分類するための kind。
///
/// <para>
/// 現在の Unity 側 protocol は int の log level を運んでいるが、
/// そのままでは後続の検索・集計・エクスポート層が「数値の意味」を毎回知る必要がある。
/// この enum を境界に置くことで、transport の数値表現と app/view 層の解釈を疎結合に保つ。
/// </para>
/// </summary>
public enum LogEntryKind : byte
{
    Unknown = 0,
    Trace = 1,
    Debug = 2,
    Information = 3,
    Warning = 4,
    Error = 5,
    Critical = 6,
    None = 7,
}
