#nullable enable

using System.Collections.Generic;
using System.Text.Json;

namespace DebugStudio.Export.Elastic.Kibana;

/// <summary>
/// NDJSON 1 行分の Kibana saved object。
/// Attributes は型付けせず生の JsonElement として保持する。
/// </summary>
public sealed record KibanaSavedObject(
    string Id,
    string Type,
    JsonElement Attributes,
    IReadOnlyList<KibanaSavedObjectReference> References,
    int LineNumber,
    bool IsParseFailure = false);
