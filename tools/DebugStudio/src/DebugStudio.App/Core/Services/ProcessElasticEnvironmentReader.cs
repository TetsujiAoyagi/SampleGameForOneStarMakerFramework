#nullable enable

namespace DebugStudio.App.Core.Services;

/// <summary>
/// 本番 process の環境変数から Elastic 設定を読む既定 reader。
/// </summary>
public sealed class ProcessElasticEnvironmentReader : IElasticEnvironmentReader
{
    public const string ElasticUrlVariableName = "DEBUGSTUDIO_ELASTIC_URL";

    public const string ElasticApiKeyVariableName = "DEBUGSTUDIO_ELASTIC_API_KEY";

    public const string KibanaUrlVariableName = "DEBUGSTUDIO_KIBANA_URL";

    public string? ReadElasticUrl() => Environment.GetEnvironmentVariable(ElasticUrlVariableName);

    public string? ReadElasticApiKey() => Environment.GetEnvironmentVariable(ElasticApiKeyVariableName);

    public string? ReadKibanaUrl() => Environment.GetEnvironmentVariable(KibanaUrlVariableName);
}
