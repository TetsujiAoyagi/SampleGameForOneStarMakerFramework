#nullable enable

namespace DebugStudio.Export.Elastic;

/// <summary>
/// DebugStudio.Export が扱う Elastic ingest 経路。
/// Unity runtime の事情はここへ持ち込まず、export 後の運用導線だけを表す。
/// </summary>
public enum ElasticIngestMode
{
    Filebeat = 0,
    ElasticBulk = 1,
    HttpDirect = 2,
}
