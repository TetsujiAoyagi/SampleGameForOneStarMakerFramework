#nullable enable

using DebugStudio.Export.Elastic;

namespace DebugStudio.Export.Tests.Elastic;

/// <summary>
/// Elastic ingest の既定戦略を先に固定する。
/// ここがぶれると後続の artifact writer や schema 設計が全部揺れるので、
/// まずは「何を推奨するのか」だけを失敗テストで押さえる。
/// </summary>
public sealed class ElasticIngestModePolicyTests
{
    [Fact]
    public void DefaultはFilebeatを主経路として返す()
    {
        var decision = ElasticIngestModePolicy.GetDefault();

        Assert.Equal(ElasticIngestMode.Filebeat, decision.Mode);
        Assert.Equal(ElasticIngestRecommendation.Recommended, decision.Recommendation);
    }

    [Fact]
    public void ElasticBulkは明示選択時のみ許可される補助線として扱う()
    {
        var decision = ElasticIngestModePolicy.Describe(ElasticIngestMode.ElasticBulk);

        Assert.Equal(ElasticIngestMode.ElasticBulk, decision.Mode);
        Assert.Equal(ElasticIngestRecommendation.Optional, decision.Recommendation);
    }

    [Fact]
    public void HttpDirectは現段階では非推奨として扱う()
    {
        var decision = ElasticIngestModePolicy.Describe(ElasticIngestMode.HttpDirect);

        Assert.Equal(ElasticIngestMode.HttpDirect, decision.Mode);
        Assert.Equal(ElasticIngestRecommendation.NotRecommended, decision.Recommendation);
    }
}
