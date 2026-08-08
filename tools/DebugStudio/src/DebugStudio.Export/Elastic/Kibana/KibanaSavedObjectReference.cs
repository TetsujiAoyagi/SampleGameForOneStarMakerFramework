#nullable enable

namespace DebugStudio.Export.Elastic.Kibana;

/// <summary>
/// Kibana saved object の references 1 件。
/// </summary>
public sealed record KibanaSavedObjectReference(string Id, string Name, string Type);
