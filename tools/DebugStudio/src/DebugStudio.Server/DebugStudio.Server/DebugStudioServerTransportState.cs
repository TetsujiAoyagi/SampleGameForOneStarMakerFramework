namespace DebugStudio.Server;

/// <summary>
/// DebugStudio server transport の状態。
/// UI や上位 service が string 比較をしなくてよいよう、状態は enum で固定する。
/// </summary>
public enum DebugStudioServerTransportState
{
    Idle = 0,
    Listening = 1,
    Connected = 2,
    Faulted = 3,
    Disposed = 4,
}
