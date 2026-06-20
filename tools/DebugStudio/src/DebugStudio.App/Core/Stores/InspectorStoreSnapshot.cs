#nullable enable

using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// inspector store の軽量サマリー。
/// target / state / property count を即座に参照したい UI に渡す。
/// </summary>
public readonly record struct InspectorStoreSnapshot(
    long TargetId,
    string TargetName,
    string? TargetTypeName,
    InspectorDetailState DetailState,
    long Revision,
    int SectionCount,
    int PropertyCount,
    string Message);
