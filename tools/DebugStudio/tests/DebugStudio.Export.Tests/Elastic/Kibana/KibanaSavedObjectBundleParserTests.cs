#nullable enable

using DebugStudio.Export.Elastic.Kibana;

namespace DebugStudio.Export.Tests.Elastic.Kibana;

public sealed class KibanaSavedObjectBundleParserTests
{
    [Fact]
    public void 二行のNDJSONは二オブジェクトになる()
    {
        var ndjson =
            """
            {"id":"a","type":"search","attributes":{},"references":[]}
            {"id":"b","type":"dashboard","attributes":{},"references":[]}
            """;

        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        Assert.Equal(2, bundle.Objects.Count);
        Assert.Equal("a", bundle.Objects[0].Id);
        Assert.Equal("b", bundle.Objects[1].Id);
    }

    [Fact]
    public void 末尾の空行を読み飛ばしてもオブジェクト数は増えない()
    {
        var ndjson =
            "{\"id\":\"a\",\"type\":\"search\",\"attributes\":{},\"references\":[]}\n\n";

        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        Assert.Single(bundle.Objects);
        Assert.Equal("a", bundle.Objects[0].Id);
    }

    [Fact]
    public void LineNumberは1始まりで付く()
    {
        var ndjson =
            """
            {"id":"a","type":"search","attributes":{},"references":[]}
            {"id":"b","type":"dashboard","attributes":{},"references":[]}
            """;

        var bundle = KibanaSavedObjectBundleParser.Parse(ndjson);

        Assert.Equal(1, bundle.Objects[0].LineNumber);
        Assert.Equal(2, bundle.Objects[1].LineNumber);
    }
}
