#nullable enable

namespace DebugStudio.Export.Elastic;

/// <summary>
/// ingest mode と、その mode に対する現時点の判断をまとめる。
/// まずは最小限の情報だけを持たせ、後で理由文言や artifact 種別を足せるようにする。
/// </summary>
public readonly record struct ElasticIngestDecision(
    ElasticIngestMode Mode,
    ElasticIngestRecommendation Recommendation);
