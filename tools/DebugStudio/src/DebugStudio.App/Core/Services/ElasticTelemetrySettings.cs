#nullable enable

using System;
using DebugStudio.Export.Elastic;

namespace DebugStudio.App.Core.Services;

/// <summary>
/// L1 Verify で使う Elastic / Kibana 設定。
/// 秘密値は保持しても UI やログへは出さない。
/// </summary>
public sealed class ElasticTelemetrySettings
{
    private ElasticTelemetrySettings(Uri elasticUrl, Uri kibanaUrl, string? apiKeyBase64Value)
    {
        ElasticUrl = elasticUrl;
        KibanaUrl = kibanaUrl;
        ApiKeyBase64Value = apiKeyBase64Value;
    }

    public Uri ElasticUrl { get; }

    public Uri KibanaUrl { get; }

    /// <summary>
    /// Elastic が発行した Base64 済み ApiKey 値。Authorization ヘッダ組み立て専用。
    /// </summary>
    public string? ApiKeyBase64Value { get; }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(ApiKeyBase64Value);

    public bool UsesDefaultElasticUrl { get; private init; }

    public bool UsesDefaultKibanaUrl { get; private init; }

    /// <summary>
    /// 環境変数 reader から設定を構築する。loopback 以外は invalid として扱う。
    /// </summary>
    public static bool TryCreate(IElasticEnvironmentReader reader, out ElasticTelemetrySettings? settings, out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(reader);

        settings = null;
        errorMessage = string.Empty;

        var rawElasticUrl = reader.ReadElasticUrl();
        var rawKibanaUrl = reader.ReadKibanaUrl();

        if (!ElasticLoopbackEndpointPolicy.TryValidate(
                rawElasticUrl,
                ElasticLoopbackEndpointPolicy.DefaultElasticUrl,
                out var elasticUrl,
                out errorMessage))
        {
            return false;
        }

        if (!ElasticLoopbackEndpointPolicy.TryValidate(
                rawKibanaUrl,
                ElasticLoopbackEndpointPolicy.DefaultKibanaUrl,
                out var kibanaUrl,
                out errorMessage))
        {
            return false;
        }

        settings = new ElasticTelemetrySettings(elasticUrl, kibanaUrl, reader.ReadElasticApiKey())
        {
            UsesDefaultElasticUrl = string.IsNullOrWhiteSpace(rawElasticUrl),
            UsesDefaultKibanaUrl = string.IsNullOrWhiteSpace(rawKibanaUrl),
        };
        return true;
    }

    /// <summary>
    /// UI 向けの設定概要。URL は host/port まで、ApiKey は有無だけを示す。
    /// </summary>
    public string DescribeConfigurationForUi()
    {
        return
            $"Elastic={ElasticUrl.Host}:{ElasticUrl.Port} (default={(UsesDefaultElasticUrl ? "yes" : "no")}), " +
            $"Kibana={KibanaUrl.Host}:{KibanaUrl.Port} (default={(UsesDefaultKibanaUrl ? "yes" : "no")}), " +
            $"ApiKey={(HasApiKey ? "configured" : "not configured")}";
    }
}
