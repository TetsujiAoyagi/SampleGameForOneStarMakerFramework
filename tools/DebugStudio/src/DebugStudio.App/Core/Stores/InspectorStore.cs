#nullable enable

using System;
using DebugStudio.App.Core.Models;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.App.Core.Stores;

/// <summary>
/// inspector detail の最新状態を保持する store。
///
/// <para>
/// 「問い合わせ中」「Unity 側未対応」「最新 detail 済み」を同じ保管場所に集約することで、
/// InspectorViewModel は transport の事情を知らずに状態遷移を描画できる。
/// </para>
/// </summary>
public sealed class InspectorStore
{
    private readonly object _gate = new();
    private InspectorDocumentRecord? _document;

    public event Action<InspectorStoreSnapshot>? Changed;

    public InspectorStoreSnapshot BeginQuery(long targetId, string targetName, string? targetTypeName)
    {
        return UpdateDocument(InspectorDocumentRecord.CreatePending(targetId, targetName, targetTypeName));
    }

    public InspectorStoreSnapshot ApplyDetail(InspectorDetailEnvelopeV1 detail)
    {
        ArgumentNullException.ThrowIfNull(detail);

        InspectorStoreSnapshot snapshot;
        lock (_gate)
        {
            // hierarchy の選択がすでに別 target へ進んでいるなら、
            // 遅延到着した detail で inspector パネルを巻き戻さない。
            if (_document == null || _document.TargetId != detail.TargetId)
            {
                return CreateSnapshotUnsafe();
            }

            // 同一 target でも古い revision を上書き採用すると表示が巻き戻るため捨てる。
            if (_document.Revision > detail.Revision && detail.Revision > 0)
            {
                return CreateSnapshotUnsafe();
            }

            _document = InspectorDocumentRecord.FromEnvelope(detail);
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public InspectorStoreSnapshot SetUnsupported(long targetId, string targetName, string? targetTypeName, string message)
    {
        return UpdateDocument(InspectorDocumentRecord.CreateStatus(
            targetId,
            targetName,
            targetTypeName,
            InspectorDetailState.Unsupported,
            message));
    }

    public InspectorStoreSnapshot SetFaulted(long targetId, string targetName, string? targetTypeName, string message)
    {
        return UpdateDocument(InspectorDocumentRecord.CreateStatus(
            targetId,
            targetName,
            targetTypeName,
            InspectorDetailState.Faulted,
            message));
    }

    public InspectorStoreSnapshot Clear()
    {
        InspectorStoreSnapshot snapshot;
        lock (_gate)
        {
            _document = null;
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    public InspectorStoreSnapshot GetSnapshotState()
    {
        lock (_gate)
        {
            return CreateSnapshotUnsafe();
        }
    }

    public InspectorDocumentRecord? GetDocument()
    {
        lock (_gate)
        {
            return _document;
        }
    }

    /// <summary>
    /// inspector export 用に state と document を同一 lock で複製して返す。
    /// 1 回の export 中に state と section/property 内容がずれないようにする。
    /// </summary>
    public InspectorRetainedSnapshot GetRetainedSnapshot()
    {
        lock (_gate)
        {
            return new InspectorRetainedSnapshot(
                CreateSnapshotUnsafe(),
                CloneDocumentUnsafe(_document));
        }
    }

    private InspectorStoreSnapshot UpdateDocument(InspectorDocumentRecord document)
    {
        InspectorStoreSnapshot snapshot;
        lock (_gate)
        {
            _document = document;
            snapshot = CreateSnapshotUnsafe();
        }

        Changed?.Invoke(snapshot);
        return snapshot;
    }

    private InspectorStoreSnapshot CreateSnapshotUnsafe()
    {
        return _document == null
            ? new InspectorStoreSnapshot(0, "No selection", null, InspectorDetailState.Unknown, 0, 0, 0, "Inspector is idle.")
            : new InspectorStoreSnapshot(
                _document.TargetId,
                _document.TargetName,
                _document.TargetTypeName,
                _document.State,
                _document.Revision,
                _document.Sections.Length,
                _document.PropertyCount,
                _document.Message);
    }

    private static InspectorDocumentRecord? CloneDocumentUnsafe(InspectorDocumentRecord? document)
    {
        if (document == null)
        {
            return null;
        }

        var sections = new InspectorSectionRecord[document.Sections.Length];
        for (var sectionIndex = 0; sectionIndex < document.Sections.Length; sectionIndex++)
        {
            var sourceSection = document.Sections[sectionIndex];
            var properties = new InspectorPropertyRecord[sourceSection.Properties.Length];
            for (var propertyIndex = 0; propertyIndex < sourceSection.Properties.Length; propertyIndex++)
            {
                var sourceProperty = sourceSection.Properties[propertyIndex];
                properties[propertyIndex] = new InspectorPropertyRecord
                {
                    PropertyId = sourceProperty.PropertyId,
                    ValueTypeId = sourceProperty.ValueTypeId,
                    Flags = sourceProperty.Flags,
                    DisplayName = sourceProperty.DisplayName,
                    ValueText = sourceProperty.ValueText,
                    RawValue = sourceProperty.RawValue,
                    Unit = sourceProperty.Unit,
                    Path = sourceProperty.Path,
                };
            }

            sections[sectionIndex] = new InspectorSectionRecord
            {
                SectionId = sourceSection.SectionId,
                Kind = sourceSection.Kind,
                TypeId = sourceSection.TypeId,
                DisplayName = sourceSection.DisplayName,
                TypeName = sourceSection.TypeName,
                Properties = properties,
            };
        }

        return new InspectorDocumentRecord
        {
            Revision = document.Revision,
            CapturedAtUnixTimeMilliseconds = document.CapturedAtUnixTimeMilliseconds,
            TargetId = document.TargetId,
            TargetName = document.TargetName,
            TargetTypeId = document.TargetTypeId,
            TargetTypeName = document.TargetTypeName,
            State = document.State,
            Message = document.Message,
            Sections = sections,
        };
    }
}
