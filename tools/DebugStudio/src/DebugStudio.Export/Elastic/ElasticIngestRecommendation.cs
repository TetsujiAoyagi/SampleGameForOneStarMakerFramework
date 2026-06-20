#nullable enable

namespace DebugStudio.Export.Elastic;

/// <summary>
/// その ingest mode を現段階でどの程度推奨するかを表す。
/// TDD の最初の目的は、この推奨度を code と test で固定することにある。
/// </summary>
public enum ElasticIngestRecommendation
{
    Recommended = 0,
    Optional = 1,
    NotRecommended = 2,
}
