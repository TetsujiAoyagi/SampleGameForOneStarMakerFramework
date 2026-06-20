#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using DebugStudio.App.Core.Infrastructure;
using DebugStudio.App.Core.Models;
using DebugStudio.App.Core.Stores;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// retained inspector document を normalized export record へ変換して永続化する app service。
/// property 行へ平坦化しつつ、空文書や unsupported 状態も 1 行として残す。
/// </summary>
public sealed class InspectorExportService
{
    private readonly InspectorStore _inspectorStore;
    private readonly IInspectorExportWriter _writer;

    public InspectorExportService(InspectorStore inspectorStore, IInspectorExportWriter writer)
    {
        _inspectorStore = inspectorStore ?? throw new ArgumentNullException(nameof(inspectorStore));
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public Task ExportAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        var retainedSnapshot = _inspectorStore.GetRetainedSnapshot();
        var state = retainedSnapshot.State;
        if (state.TargetId == 0)
        {
            throw new InvalidOperationException("Inspector has no selected target.");
        }

        var document = retainedSnapshot.Document;
        var records = new List<InspectorExportRecord>();

        if (document == null || document.Sections.Length == 0)
        {
            records.Add(CreateBaseRecord(state, document, section: null, property: null));
            return _writer.WriteAsync(records, outputPath, cancellationToken);
        }

        for (var sectionIndex = 0; sectionIndex < document.Sections.Length; sectionIndex++)
        {
            var section = document.Sections[sectionIndex];
            if (section.Properties.Length == 0)
            {
                records.Add(CreateBaseRecord(state, document, section, property: null));
                continue;
            }

            for (var propertyIndex = 0; propertyIndex < section.Properties.Length; propertyIndex++)
            {
                records.Add(CreateBaseRecord(state, document, section, section.Properties[propertyIndex]));
            }
        }

        return _writer.WriteAsync(records, outputPath, cancellationToken);
    }

    private static InspectorExportRecord CreateBaseRecord(
        InspectorStoreSnapshot state,
        InspectorDocumentRecord? document,
        InspectorSectionRecord? section,
        InspectorPropertyRecord? property)
    {
        var timestampUnixTimeMilliseconds = document?.CapturedAtUnixTimeMilliseconds ?? 0;
        return new InspectorExportRecord
        {
            TimestampUtc = FormatTimestampUtc(timestampUnixTimeMilliseconds),
            TimestampUnixTimeMilliseconds = timestampUnixTimeMilliseconds,
            TargetId = state.TargetId,
            TargetName = state.TargetName,
            TargetTypeName = state.TargetTypeName,
            Revision = state.Revision,
            State = state.DetailState.ToString(),
            Message = state.Message,
            SectionId = section?.SectionId,
            SectionKind = section?.Kind.ToString(),
            SectionDisplayName = section?.DisplayName,
            SectionTypeName = section?.TypeName,
            PropertyId = property?.PropertyId,
            PropertyName = property?.DisplayName,
            ValueTypeId = property?.ValueTypeId,
            ValueText = property?.ValueText,
            RawValue = property?.RawValue,
            Unit = property?.Unit,
            Path = property?.Path,
            Flags = property?.Flags.ToString(),
        };
    }

    private static string FormatTimestampUtc(long unixTimeMilliseconds)
    {
        if (unixTimeMilliseconds <= 0)
        {
            return "1970-01-01T00:00:00.0000000Z";
        }

        try
        {
            return DateTimeOffset
                .FromUnixTimeMilliseconds(unixTimeMilliseconds)
                .UtcDateTime
                .ToString("O", CultureInfo.InvariantCulture);
        }
        catch
        {
            return "1970-01-01T00:00:00.0000000Z";
        }
    }
}
