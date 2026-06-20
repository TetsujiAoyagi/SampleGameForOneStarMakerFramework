#nullable enable

namespace DebugStudio.Export.Elastic;

/// <summary>
/// Elastic ingest の既定戦略を返す policy。
///
/// <para>
/// 現段階では「まず DebugStudio export を NDJSON で出し、継続取り込みは Filebeat を主経路にする」
/// という判断を固定する。Bulk は補助線、HTTP direct はまだ責務が重いため非推奨とする。
/// </para>
/// </summary>
public static class ElasticIngestModePolicy
{
    public static ElasticIngestDecision GetDefault()
    {
        return Describe(ElasticIngestMode.Filebeat);
    }

    public static ElasticIngestDecision Describe(ElasticIngestMode mode)
    {
        return mode switch
        {
            ElasticIngestMode.Filebeat => new ElasticIngestDecision(mode, ElasticIngestRecommendation.Recommended),
            ElasticIngestMode.ElasticBulk => new ElasticIngestDecision(mode, ElasticIngestRecommendation.Optional),
            ElasticIngestMode.HttpDirect => new ElasticIngestDecision(mode, ElasticIngestRecommendation.NotRecommended),
            _ => new ElasticIngestDecision(mode, ElasticIngestRecommendation.NotRecommended),
        };
    }
}
