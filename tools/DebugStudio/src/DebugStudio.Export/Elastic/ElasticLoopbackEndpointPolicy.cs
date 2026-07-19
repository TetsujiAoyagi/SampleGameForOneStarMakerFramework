#nullable enable

using System;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// L1 Verify 用 Elastic / Kibana endpoint を loopback のみに限定する policy。
///
/// <para>
/// DebugStudio は localhost への明示的な疎通確認だけを行い、
/// 誤設定や将来の UI 拡張で外部 endpoint へ telemetry を流す事故を防ぐ。
/// </para>
/// </summary>
public static class ElasticLoopbackEndpointPolicy
{
    public const string DefaultElasticUrl = "http://localhost:9200";

    public const string DefaultKibanaUrl = "http://localhost:5601";

    /// <summary>
    /// 環境変数や既定値から読んだ URL を検証し、安全な base URI を返す。
    /// userinfo / query / fragment を含む URL は外部送信や秘密漏えいの温床になるため拒否する。
    /// </summary>
    public static bool TryValidate(string? rawUrl, string defaultUrl, out Uri validated, out string errorMessage)
    {
        validated = null!;
        errorMessage = string.Empty;

        var candidate = string.IsNullOrWhiteSpace(rawUrl) ? defaultUrl : rawUrl.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            errorMessage = "endpoint URL の形式が不正です。";
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "endpoint は http または https のみ許可されます。";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            errorMessage = "endpoint URL に userinfo を含めないでください。認証は DEBUGSTUDIO_ELASTIC_API_KEY を使います。";
            return false;
        }

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
        {
            errorMessage = "endpoint URL に query や fragment を含めないでください。";
            return false;
        }

        // L1 は Elastic/Kibana root の固定 API path をここで組み立てる。
        // 利用者指定の path を黙って捨てると、意図しない接続先を見逃すため安全側で拒否する。
        if (!string.Equals(uri.AbsolutePath, "/", StringComparison.Ordinal))
        {
            errorMessage = "L1 endpoint URL の path は空または '/' のみ許可されます。";
            return false;
        }

        if (!IsLoopbackHost(uri.Host))
        {
            errorMessage = "endpoint は localhost / 127.0.0.1 / ::1 の loopback のみ許可されます。";
            return false;
        }

        validated = new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri;
        return true;
    }

    /// <summary>
    /// IPv4 / IPv6 loopback 以外は L1 Verify では許可しない。
    /// </summary>
    public static bool IsLoopbackHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(host, "127.0.0.1", StringComparison.Ordinal) ||
            string.Equals(host, "::1", StringComparison.Ordinal) ||
            string.Equals(host, "[::1]", StringComparison.Ordinal);
    }
}
