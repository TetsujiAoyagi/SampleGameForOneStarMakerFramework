#nullable enable

using System;
using System.Linq;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Models;

/// <summary>
/// 現在表示中の inspector 文書。
///
/// <para>
/// 1 hierarchy target に対して最新の detail 一式を保持する。
/// Pending / Unsupported のような「まだ中身が無いが、状態としては意味がある」ケースも
/// 同じモデルで表現できるようにしている。
/// </para>
/// </summary>
public sealed class InspectorDocumentRecord
{
    public required long Revision { get; init; }

    public required long CapturedAtUnixTimeMilliseconds { get; init; }

    public required long TargetId { get; init; }

    public required string TargetName { get; init; }

    public required int TargetTypeId { get; init; }

    public string? TargetTypeName { get; init; }

    public required InspectorDetailState State { get; init; }

    public required string Message { get; init; }

    public required InspectorSectionRecord[] Sections { get; init; }

    public int PropertyCount => Sections.Sum(section => section.Properties.Length);

    public static InspectorDocumentRecord FromEnvelope(InspectorDetailEnvelopeV1 envelope)
    {
        return new InspectorDocumentRecord
        {
            Revision = envelope.Revision,
            CapturedAtUnixTimeMilliseconds = envelope.CapturedAtUnixTimeMilliseconds,
            TargetId = envelope.TargetId,
            TargetName = envelope.TargetName,
            TargetTypeId = envelope.TargetTypeId,
            TargetTypeName = envelope.TargetTypeName,
            State = envelope.State,
            Message = envelope.Message,
            Sections = envelope.Sections.Select(InspectorSectionRecord.FromDto).ToArray(),
        };
    }

    public static InspectorDocumentRecord CreatePending(long targetId, string targetName, string? targetTypeName)
    {
        return new InspectorDocumentRecord
        {
            Revision = 0,
            CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TargetId = targetId,
            TargetName = targetName,
            TargetTypeId = 0,
            TargetTypeName = targetTypeName,
            State = InspectorDetailState.Pending,
            Message = "Inspector query is queued.",
            Sections = Array.Empty<InspectorSectionRecord>(),
        };
    }

    public static InspectorDocumentRecord CreateStatus(
        long targetId,
        string targetName,
        string? targetTypeName,
        InspectorDetailState state,
        string message)
    {
        return new InspectorDocumentRecord
        {
            Revision = 0,
            CapturedAtUnixTimeMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            TargetId = targetId,
            TargetName = targetName,
            TargetTypeId = 0,
            TargetTypeName = targetTypeName,
            State = state,
            Message = message,
            Sections = Array.Empty<InspectorSectionRecord>(),
        };
    }
}
