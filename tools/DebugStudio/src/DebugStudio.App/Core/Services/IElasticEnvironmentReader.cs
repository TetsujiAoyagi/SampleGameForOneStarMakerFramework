#nullable enable

namespace DebugStudio.App.Core.Services;

/// <summary>
/// Elastic 関連環境変数の読み取りを抽象化する。
/// テストでは process-global な Environment へ触れず、注入 reader で隔離する。
/// </summary>
public interface IElasticEnvironmentReader
{
    string? ReadElasticUrl();

    string? ReadElasticApiKey();

    string? ReadKibanaUrl();
}
