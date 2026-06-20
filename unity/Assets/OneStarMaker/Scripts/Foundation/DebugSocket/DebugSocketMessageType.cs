#nullable enable

namespace OneStarMaker.Foundation.DebugSocket
{
    /// <summary>
    /// DebugSocket 上で流すメッセージ種別。
    /// v1 では「ログ」「テレメトリ」「サービス状態」「デバッグコマンド」「コマンド結果」に限定する。
    /// </summary>
    public enum DebugSocketMessageType
    {
        Unknown = 0,
        Log = 1,
        Telemetry = 2,
        ServiceStatus = 3,
        DebugCommand = 4,
        CommandResult = 5,
        CapabilityHello = 6,
        CapabilityWelcome = 7,
        HierarchySnapshot = 8,
        HierarchyDelta = 9,
        InspectorQuery = 10,
        InspectorDetail = 11,
    }
}
