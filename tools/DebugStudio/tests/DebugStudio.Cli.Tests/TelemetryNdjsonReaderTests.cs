#nullable enable

using System.Text;
using System.Text.Json;
using DebugStudio.Cli.Offline;
using DebugStudio.Export.Models;
using DebugStudio.Export.Writers;

namespace DebugStudio.Cli.Tests;

public sealed class TelemetryNdjsonReaderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public TelemetryNdjsonReaderTests() => Directory.CreateDirectory(directory);
    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public async Task Read_accepts_existing_export_writer_and_disjoint_mixed_accounting()
    {
        var file = Path.Combine(directory, "writer.ndjson");
        await new NdjsonTelemetryExportWriter().WriteAsync(
        [
            Record("base", 1), Record("next", 1), Record("base", 1),
            Record(null, 2), Record("elsewhere", 2), Record("base", null),
            new TelemetryExportRecord { TimestampUtc = "now", TimestampUnixTimeMilliseconds = 1, Stream = "serviceStatus" },
        ], file);
        await File.AppendAllTextAsync(file, "\r\n \r\n");

        var input = TelemetryNdjsonReader.Read(Options(file));

        Assert.Equal(new InputAccounting(7, 2, 3, 1, 1, 1, 1, 1), input.Counts);
        Assert.Equal(input.Counts.NonblankRows, input.Counts.SelectedUniqueRows + input.Counts.ExactDuplicateRows +
            input.Counts.SkippedServiceStatusRows + input.Counts.UnassignedTelemetryRows + input.Counts.OtherSessionTelemetryRows);
        Assert.Contains(input.Diagnostics, diagnostic => diagnostic.Code == "missing-sequence");
        var duplicate = Assert.Single(input.Diagnostics, diagnostic => diagnostic.Code == "exact-duplicate");
        Assert.Equal(3, duplicate.Location!.Line);
        Assert.Equal(1, duplicate.RelatedLocation!.Line);
    }

    [Fact]
    public void Read_accepts_bom_crlf_and_keeps_gaps_and_absent_sequences()
    {
        var file = Write("\uFEFF\r\n" + Json(Record("base", 1)) + "\r\n" + Json(Record("base", 100)) + "\r\n" +
            Json(Record("next", null)) + "\r\n" + Json(Record("next", null)));
        var input = TelemetryNdjsonReader.Read(Options(file));
        Assert.Equal(4, input.Counts.SelectedUniqueRows);
        Assert.Equal(2, input.Counts.MissingSequenceRows);
        Assert.Equal(0, input.Counts.ExactDuplicateRows);
        Assert.Equal(1, input.Counts.BlankRows);
    }

    [Fact]
    public void Read_deduplicates_typed_fields_across_files_property_order_extensions_and_tag_sets()
    {
        var first = Json(Record("base", 1, ["Bottleneck", "x", "x"]));
        using var document = JsonDocument.Parse(Json(Record("base", 1, ["x", "Bottleneck"])));
        var second = "{" + string.Join(",", document.RootElement.EnumerateObject().Reverse().Select(property =>
            JsonSerializer.Serialize(property.Name) + ":" + property.Value.GetRawText())) + ",\"extension\":true}";
        var file1 = Write(first + "\n" + Json(Record("next", 1)), "first.ndjson");
        var file2 = Write(second, "second.ndjson");
        var input = TelemetryNdjsonReader.Read(Options(file1, file2));
        Assert.Equal(2, input.Counts.SelectedUniqueRows);
        Assert.Equal(1, input.Counts.ExactDuplicateRows);
        Assert.Equal(file1, input.Diagnostics[0].RelatedLocation!.File);
        Assert.Equal(file2, input.Diagnostics[0].Location!.File);
    }

    [Theory]
    [InlineData("\"tags\":null", "\"tags\":[]")]
    [InlineData("\"buildVersion\":null", "\"buildVersion\":\"enriched\"")]
    [InlineData("\"elapsedMs\":10", "\"elapsedMs\":11")]
    public void Read_conflicting_identity_reports_both_locations(string original, string replacement)
    {
        var first = Json(Record("base", 1));
        Assert.Contains(original, first, StringComparison.Ordinal);
        var file = Write(first + "\n" + first.Replace(original, replacement, StringComparison.Ordinal) + "\n" + Json(Record("next", 1)));
        var error = Assert.Throws<ComparisonInputException>(() => TelemetryNdjsonReader.Read(Options(file)));
        Assert.Equal(2, error.ExitCode);
        Assert.Contains($"'{file}':2", error.Message, StringComparison.Ordinal);
        Assert.Contains($"'{file}':1", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"@timestamp\":\"now\",\"timestampUnixTimeMilliseconds\":1,\"stream\":\"telemetry\",\"stream\":\"telemetry\"}")]
    [InlineData("{\"@timestamp\":1,\"timestampUnixTimeMilliseconds\":1,\"stream\":\"serviceStatus\"}")]
    [InlineData("{\"@timestamp\":\"now\",\"timestampUnixTimeMilliseconds\":1.5,\"stream\":\"serviceStatus\"}")]
    [InlineData("{\"@timestamp\":\"now\",\"stream\":\"serviceStatus\"}")]
    [InlineData("{\"@timestamp\":\"now\",\"timestampUnixTimeMilliseconds\":1,\"stream\":\"Telemetry\"}")]
    [InlineData("{\"@timestamp\":\"now\",\"timestampUnixTimeMilliseconds\":1,\"stream\":\"telemetry\",\"schemaVersion\":2,\"name\":\"Span\",\"kind\":\"span\"}")]
    [InlineData("{\"@timestamp\":\"now\",\"timestampUnixTimeMilliseconds\":1,\"stream\":\"telemetry\",\"schemaVersion\":3,\"name\":\"Span\",\"kind\":\"SPAN\"}")]
    [InlineData("{\"@timestamp\":\"now\",\"timestampUnixTimeMilliseconds\":1,\"stream\":\"telemetry\",\"schemaVersion\":3,\"kind\":\"span\"}")]
    public void Read_rejects_invalid_json_envelopes_before_skipping(string invalid)
    {
        var file = Write(Json(Record("base", 1)) + "\n" + invalid);
        var error = Assert.Throws<ComparisonInputException>(() => TelemetryNdjsonReader.Read(Options(file)));
        Assert.Equal(2, error.ExitCode);
        Assert.Contains($"'{file}':2", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"elapsedMs\":10", "\"elapsedMs\":-1")]
    [InlineData("\"elapsedMs\":10", "\"elapsedMs\":1e400")]
    [InlineData("\"producerSequence\":1", "\"producerSequence\":0")]
    [InlineData("\"producerSequence\":1", "\"producerSequence\":-1")]
    [InlineData("\"payload\":null", "\"payload\":{\"stage\":\"x\",\"stage\":\"y\"}")]
    public void Read_rejects_selected_invalid_values_and_nested_duplicate_properties(string original, string replacement)
    {
        var file = Write(Json(Record("base", 1)).Replace(original, replacement, StringComparison.Ordinal) + "\n" + Json(Record("next", 1)));
        var error = Assert.Throws<ComparisonInputException>(() => TelemetryNdjsonReader.Read(Options(file)));
        Assert.Equal(2, error.ExitCode);
        Assert.Contains($"'{file}':1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_rejects_invalid_utf8_at_accurate_physical_line()
    {
        var prefix = Encoding.UTF8.GetBytes(Json(Record("base", 1)) + "\n\n" + Json(Record("next", 1)) + "\n");
        var file = Path.Combine(directory, "bad.ndjson");
        File.WriteAllBytes(file, [.. prefix, 0xc3, 0x28]);
        var error = Assert.Throws<ComparisonInputException>(() => TelemetryNdjsonReader.Read(Options(file)));
        Assert.Contains($"'{file}':4", error.Message, StringComparison.Ordinal);
        Assert.Contains("UTF-8", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_missing_session_and_file_errors_have_no_invented_line()
    {
        var file = Write(Json(Record("base", 1)));
        var missingSession = Assert.Throws<ComparisonInputException>(() => TelemetryNdjsonReader.Read(Options(file)));
        Assert.Equal(2, missingSession.ExitCode);
        Assert.Contains("'next'", missingSession.Message, StringComparison.Ordinal);
        var missingFile = Assert.Throws<ComparisonInputException>(() => TelemetryNdjsonReader.Read(Options(Path.Combine(directory, "missing"))));
        Assert.Equal(1, missingFile.ExitCode);
        var folder = Assert.Throws<ComparisonInputException>(() => TelemetryNdjsonReader.Read(Options(directory)));
        Assert.Equal(2, folder.ExitCode);
    }


    [Fact]
    public void Read_repeated_files_are_deduplicated_only_with_positive_selected_identity()
    {
        var file = Write(Json(Record("base", 1)) + "\n" + Json(Record("next", 1)));
        var input = TelemetryNdjsonReader.Read(Options(file, file));
        Assert.Equal(4, input.Counts.NonblankRows);
        Assert.Equal(2, input.Counts.SelectedUniqueRows);
        Assert.Equal(2, input.Counts.ExactDuplicateRows);
    }

    [Fact]
    public void Read_optional_nonmetric_float_overflow_cannot_crash_typed_deduplication()
    {
        var json = Json(Record("base", 1)).Replace("\"cpuTime\":null", "\"cpuTime\":1e100", StringComparison.Ordinal);
        var file = Write(json + "\n" + json + "\n" + Json(Record("next", 1)));
        var input = TelemetryNdjsonReader.Read(Options(file));
        Assert.Equal(1, input.Counts.ExactDuplicateRows);
    }

    [Fact]
    public async Task Command_success_writes_summary_warnings_and_preserves_sources()
    {
        var file = Path.Combine(directory, "command.ndjson");
        await new NdjsonTelemetryExportWriter().WriteAsync([Record("base", null), Record("next", 1)], file);
        var before = await File.ReadAllBytesAsync(file);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CompareCommand.Execute(Arguments(file, "json"), output, error);
        Assert.Equal(0, exit);
        using var report = JsonDocument.Parse(output.ToString());
        Assert.Equal(2, report.RootElement.GetProperty("inputCounts").GetProperty("selectedUniqueRows").GetInt64());
        Assert.Contains("missing-sequence", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, await File.ReadAllBytesAsync(file));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Command_fatal_after_valid_rows_publishes_only_fatal_diagnostic(bool openFailure)
    {
        var valid = Write(Json(Record("base", null)) + "\n" + Json(Record("next", 1)));
        var bad = openFailure ? Path.Combine(directory, "missing.ndjson") : Write("{invalid secret full line}", "bad.ndjson");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var args = Arguments(valid, "text").Concat(new[] { "--input", bad }).ToArray();
        Assert.Equal(openFailure ? 1 : 2, CompareCommand.Execute(args, output, error));
        Assert.Equal("", output.ToString());
        Assert.DoesNotContain("Warning", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret full line", error.ToString(), StringComparison.Ordinal);
        Assert.Single(error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Command_usage_error_has_only_fatal_message_and_help_exits_zero()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, CompareCommand.Execute(["compare"], output, error));
        Assert.Equal("", output.ToString());
        Assert.Single(error.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        error.GetStringBuilder().Clear();
        Assert.Equal(0, CompareCommand.Execute(["compare", "--help"], output, error));
        Assert.Contains(CompareArguments.Synopsis, error.ToString(), StringComparison.Ordinal);
    }

    private static string[] Arguments(string file, string format) => ["compare", "--input", file,
        "--baseline-session", "base", "--candidate-session", "next", "--format", format];

    private string Write(string text, string name = "input.ndjson")
    {
        var file = Path.Combine(directory, name);
        File.WriteAllText(file, text, new UTF8Encoding(false));
        return file;
    }

    internal static CompareOptions Options(params string[] files) => new(files, "base", "next", "text");
    internal static string Json(TelemetryExportRecord record) => JsonSerializer.Serialize(record, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    internal static TelemetryExportRecord Record(string? session, long? sequence, string[]? tags = null) => new()
    {
        TimestampUtc = "2026-10-04T00:00:00Z", TimestampUnixTimeMilliseconds = 1,
        Stream = "telemetry", SchemaVersion = 3, Kind = "span", Name = "AppStartup",
        SessionId = session, ProducerSequence = sequence, ElapsedMs = 10, Tags = tags,
    };
}
