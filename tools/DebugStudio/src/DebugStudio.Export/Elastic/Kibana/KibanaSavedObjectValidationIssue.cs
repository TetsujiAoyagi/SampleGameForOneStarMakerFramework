#nullable enable

namespace DebugStudio.Export.Elastic.Kibana;

/// <summary>
/// Kibana saved object bundle の検算指摘 1 件。
/// </summary>
public sealed record KibanaSavedObjectValidationIssue(
    string RuleId,
    int LineNumber,
    string ObjectId,
    string Message);
