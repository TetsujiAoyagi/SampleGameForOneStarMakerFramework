#nullable enable

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DebugStudio.Export.Elastic;

/// <summary>
/// Kibana import 用の saved objects bundle を出力する。
/// まずは data view / saved search / overview dashboard の最小セットを固定する。
/// </summary>
public sealed class ElasticKibanaSavedObjectsWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    public async Task WriteAsync(string outputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("An output path is required.", nameof(outputPath));
        }

        var directoryPath = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        var lines = new[]
        {
            JsonSerializer.Serialize(CreateDataView("debugstudio-telemetry-dataview", "debugstudio-telemetry-*"), SerializerOptions),
            JsonSerializer.Serialize(CreateDataView("debugstudio-log-dataview", "debugstudio-log-*"), SerializerOptions),
            JsonSerializer.Serialize(CreateSearch("debugstudio-telemetry-timeline", "DebugStudio Telemetry Timeline", "debugstudio-telemetry-dataview"), SerializerOptions),
            JsonSerializer.Serialize(CreateSearch("debugstudio-log-warnings", "DebugStudio Log Warnings", "debugstudio-log-dataview"), SerializerOptions),
            JsonSerializer.Serialize(CreateDashboard(), SerializerOptions),
        };

        var payload = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        await File.WriteAllTextAsync(outputPath, payload, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }

    private static object CreateDataView(string id, string title)
    {
        return new
        {
            id,
            type = "index-pattern",
            attributes = new
            {
                title,
                timeFieldName = "@timestamp",
            },
            references = Array.Empty<object>(),
        };
    }

    private static object CreateSearch(string id, string title, string dataViewId)
    {
        return new
        {
            id,
            type = "search",
            attributes = new
            {
                title,
                columns = Array.Empty<string>(),
                sort = "[[\"@timestamp\",\"desc\"]]",
            },
            references = new object[]
            {
                new
                {
                    id = dataViewId,
                    name = "kibanaSavedObjectMeta.searchSourceJSON.index",
                    type = "index-pattern",
                }
            }
        };
    }

    private static object CreateDashboard()
    {
        return new
        {
            id = "debugstudio-overview-dashboard",
            type = "dashboard",
            attributes = new
            {
                title = "DebugStudio Overview",
                description = "Telemetry and log overview for DebugStudio exports.",
                optionsJSON = "{\"useMargins\":true,\"syncColors\":false}",
                panelsJSON = "[]",
            },
            references = new object[]
            {
                new
                {
                    id = "debugstudio-telemetry-timeline",
                    name = "panel_0",
                    type = "search",
                },
                new
                {
                    id = "debugstudio-log-warnings",
                    name = "panel_1",
                    type = "search",
                }
            }
        };
    }
}
