#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// DebugStudio L0 永続化 NDJSON を tail する Filebeat sample config writer。
/// </summary>
/// <remarks>
/// <para>
/// 監視対象は <c>%LocalAppData%\DebugStudio\telemetry|logs\*.ndjson</c> の flat rolling ファイル。
/// 手動 Export の Documents 配下ディレクトリ tree とは用途が異なる。
/// </para>
/// <para>
/// DebugStudio は Filebeat を起動・監督せず、生成 config に API key 値も書かない。
/// 管理 Elastic 向けの認証は運用側が秘密管理から注入する。
/// </para>
/// </remarks>
public sealed class ElasticFilebeatConfigWriter
{
    public async Task WriteAsync(
        string outputPath,
        ElasticArtifactLayout artifactLayout,
        string inputRootDirectory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        ArgumentNullException.ThrowIfNull(artifactLayout);

        if (string.IsNullOrWhiteSpace(inputRootDirectory))
        {
            throw new ArgumentException("An input root directory is required.", nameof(inputRootDirectory));
        }

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // L0 は日次 rolling の flat directory。`**` 再帰は手動 Export 向けではない。
        var telemetryPath = Path.Combine(inputRootDirectory, "telemetry", "*.ndjson");
        var logPath = Path.Combine(inputRootDirectory, "logs", "*.ndjson");
        var yaml = new StringBuilder()
            .AppendLine("# DebugStudio L2 host Filebeat 向け sample。")
            .AppendLine("# L0 永続化 (%LocalAppData%\\DebugStudio) の flat rolling NDJSON を tail する。")
            .AppendLine("# DebugStudio は Filebeat を起動・監督しない。")
            .AppendLine("#")
            .AppendLine("# 管理 Elastic (security 有効) では API key を平文 config に書かないこと。")
            .AppendLine("# 秘密管理 (Vault / K8s Secret / Elastic Agent policy 等) から")
            .AppendLine("#   output.elasticsearch.api_key")
            .AppendLine("# を注入する。ローカル compose (security 無効) では API key 不要。")
            .AppendLine("filebeat.inputs:")
            .AppendLine("- type: filestream")
            .AppendLine("  id: debugstudio-telemetry")
            .AppendLine("  enabled: true")
            .AppendLine("  paths:")
            .Append("    - \"").Append(telemetryPath).AppendLine("\"")
            .AppendLine("  # NDJSON を root へ復元し、@timestamp / stream 等を message 文字列に閉じ込めない。")
            .AppendLine("  parsers:")
            .AppendLine("    - ndjson:")
            .AppendLine("        target: \"\"")
            .AppendLine("        overwrite_keys: true")
            .AppendLine("        add_error_key: true")
            .AppendLine("  # input ID は event field にならないため、index routing 専用の非衝突 field を明示する。")
            .AppendLine("  fields:")
            .AppendLine("    debugstudio.route: telemetry")
            .AppendLine("  fields_under_root: true")
            .AppendLine("  pipeline: debugstudio-telemetry")
            .AppendLine("- type: filestream")
            .AppendLine("  id: debugstudio-log")
            .AppendLine("  enabled: true")
            .AppendLine("  paths:")
            .Append("    - \"").Append(logPath).AppendLine("\"")
            .AppendLine("  # NDJSON を root へ復元し、Log の既存 JSON field を ingest pipeline へ渡す。")
            .AppendLine("  parsers:")
            .AppendLine("    - ndjson:")
            .AppendLine("        target: \"\"")
            .AppendLine("        overwrite_keys: true")
            .AppendLine("        add_error_key: true")
            .AppendLine("  # stream はデータ契約の field なので、routing には別 field を使う。")
            .AppendLine("  fields:")
            .AppendLine("    debugstudio.route: log")
            .AppendLine("  fields_under_root: true")
            .AppendLine("  pipeline: debugstudio-log")
            .AppendLine()
            .AppendLine("# host 上の Filebeat は loopback Elasticsearch を参照する。")
            .AppendLine("# compose 内 Filebeat は tools/DebugStudio/elastic/filebeat/filebeat.yml を使う。")
            .AppendLine("output.elasticsearch:")
            .AppendLine("  hosts: [\"http://localhost:9200\"]")
            .AppendLine("  # 明示 routing で既存 template / Kibana data view と一致する日次 index に送る。")
            .AppendLine("  indices:")
            .AppendLine("    - index: \"debugstudio-telemetry-%{+yyyy.MM.dd}\"")
            .AppendLine("      when.equals:")
            .AppendLine("        debugstudio.route: telemetry")
            .AppendLine("    - index: \"debugstudio-log-%{+yyyy.MM.dd}\"")
            .AppendLine("      when.equals:")
            .AppendLine("        debugstudio.route: log")
            .AppendLine()
            .AppendLine("setup.template.enabled: false")
            .AppendLine("setup.ilm.enabled: false")
            .ToString();

        await File.WriteAllTextAsync(outputPath, yaml, cancellationToken).ConfigureAwait(false);
    }
}
