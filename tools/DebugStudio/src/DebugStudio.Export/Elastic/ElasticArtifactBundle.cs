#nullable enable

namespace DebugStudio.Export.Elastic;

/// <summary>
/// 一括生成した Elastic artifact 群をまとめる。
/// </summary>
public sealed class ElasticArtifactBundle
{
    public required ElasticArtifactLayout Layout { get; init; }
}
