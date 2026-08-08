#nullable enable

namespace DebugStudio.Export.Models;

/// <summary>
/// handshake Welcome で受け取ったセッション定数。sessionId をキーに export へ付与する。
/// 空文字は「無し」として扱い、NDJSON ではキーごと省略する。
/// </summary>
public sealed record TelemetrySessionAttributes(
    string BuildVersion,
    string Platform,
    string DeviceModel,
    string OsVersion,
    string EngineVersion);
