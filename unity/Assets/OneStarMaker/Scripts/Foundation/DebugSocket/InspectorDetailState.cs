#nullable enable

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// inspector detail 応答の状態。
    /// sender 未実装でも unsupported として正常応答できるようにする。
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
}
