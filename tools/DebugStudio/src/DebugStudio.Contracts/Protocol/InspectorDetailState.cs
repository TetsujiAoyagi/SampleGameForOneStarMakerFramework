#nullable enable

namespace DebugStudio.Contracts.Protocol;

/// <summary>
/// inspector detail 応答の状態。
/// sender 側が未実装でも protocol 的には正常応答できるよう、unsupported / not-found を分離する。
/// </summary>
public enum InspectorDetailState
{
    Unknown = 0,
    Pending = 1,
    Ready = 2,
    NotFound = 3,
    Unsupported = 4,
    Faulted = 5,
}
