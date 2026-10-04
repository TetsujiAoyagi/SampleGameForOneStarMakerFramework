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

    [Theory]
    [InlineData("\"cpuTime\":null", "\"cpuTime\":1e400")]
    [InlineData("\"payload\":null", "\"payload\":{\"fps\":1e400}")]
    public void Read_optional_nonmetric_float_overflow_cannot_crash_typed_deduplication(string original, string replacement)
    {
        var json = Json(Record("base", 1)).Replace(original, replacement, StringComparison.Ordinal);
        var file = Write(json + "\n" + json + "\n" + Json(Record("next", 1)));
        var input = TelemetryNdjsonReader.Read(Options(file));
        Assert.Equal(1, input.Counts.ExactDuplicateRows);
        var withoutSequence = Write(json.Replace("\"producerSequence\":1", "\"producerSequence\":null", StringComparison.Ordinal) + "\n" + Json(Record("next", 1)), "missing-sequence.ndjson");
        Assert.Equal(2, TelemetryNdjsonReader.Read(Options(withoutSequence)).Counts.SelectedUniqueRows);
    }

    [Theory]
    [InlineData("\"elapsedMs\":10", "\"elapsedMs\":0", "\"elapsedMs\":-0")]
    [InlineData("\"cpuTime\":null", "\"cpuTime\":0", "\"cpuTime\":-0")]
    [InlineData("\"payload\":null", "\"payload\":{\"fps\":0}", "\"payload\":{\"fps\":-0}")]
    public void Read_signed_zero_is_equal_in_all_typed_floating_fields(string original, string positive, string negative)
    {
        var first = Json(Record("base", 1)).Replace(original, positive, StringComparison.Ordinal);
        var second = Json(Record("base", 1)).Replace(original, negative, StringComparison.Ordinal);
        var file = Write(first + "\n" + second + "\n" + Json(Record("next", 1)));
        Assert.Equal(1, TelemetryNdjsonReader.Read(Options(file)).Counts.ExactDuplicateRows);
    }

    [Theory]
    [InlineData("\"stream\":\"telemetry\"", "\"stream\":\"\\uD800\"")]
    [InlineData("\"sessionId\":\"base\"", "\"sessionId\":\"\\uD800\"")]
    [InlineData("\"payload\":null", "\"payload\":{\"stage\":\"\\uD800\"}")]
    public void Command_malformed_surrogate_escapes_get_contextual_input_error(string original, string replacement)
    {
        var file = Write(Json(Record("base", 1)).Replace(original, replacement, StringComparison.Ordinal));
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, CompareCommand.Execute(Arguments(file, "json"), output, error));
        Assert.Empty(output.ToString());
        Assert.Contains($"'{file}':1", error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(null, -1)]
    [InlineData("", 0)]
    [InlineData("", -1)]
    [InlineData("elsewhere", 0)]
    [InlineData("elsewhere", -1)]
    [InlineData("base", 0)]
    [InlineData("base", -1)]
    [InlineData("next", 0)]
    [InlineData("next", -1)]
    public void Read_rejects_nonpositive_telemetry_sequences_before_session_selection(string? session, long sequence)
    {
        var file = Write(Json(Record("base", 1)) + "\n" + Json(Record("next", 1)) + "\n" + Json(Record(session, sequence)));
        var error = Assert.Throws<ComparisonInputException>(() => TelemetryNdjsonReader.Read(Options(file)));
        Assert.Equal(2, error.ExitCode);
        Assert.Contains($"'{file}':3", error.Message, StringComparison.Ordinal);
        Assert.Contains("producerSequence must be positive", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Read_service_status_is_exempt_from_telemetry_sequence_validation(long sequence)
    {
        var service = Json(Record(null, sequence)).Replace("\"stream\":\"telemetry\"", "\"stream\":\"serviceStatus\"", StringComparison.Ordinal);
        var file = Write(service + "\n" + Json(Record("base", 1)) + "\n" + Json(Record("next", 1)));
        Assert.Equal(1, TelemetryNdjsonReader.Read(Options(file)).Counts.SkippedServiceStatusRows);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1e400")]
    public void Read_validates_envelopes_before_skipping_but_duration_and_deduplication_stay_selected(string elapsed)
    {
        var other = Json(Record("elsewhere", 1)).Replace("\"elapsedMs\":10", $"\"elapsedMs\":{elapsed}", StringComparison.Ordinal);
        var unassigned = Json(Record(null, null)).Replace("\"elapsedMs\":10", $"\"elapsedMs\":{elapsed}", StringComparison.Ordinal);
        // Different fields on a repeated unselected identity do not enter selected deduplication.
        var file = Write(other + "\n" + Json(Record("elsewhere", 1)) + "\n" + unassigned + "\n" +
            Json(Record("base", 1)) + "\n" + Json(Record("next", 1)));
        var input = TelemetryNdjsonReader.Read(Options(file));
        Assert.Equal(new InputAccounting(5, 0, 2, 0, 0, 1, 2, 0), input.Counts);
        Assert.Single(input.Diagnostics, diagnostic => diagnostic.Code == "unassigned-session");
        var invalid = Write(other.Replace("\"schemaVersion\":3", "\"schemaVersion\":2", StringComparison.Ordinal), "wrong-schema.ndjson");
        Assert.Equal(2, Assert.Throws<ComparisonInputException>(() => TelemetryNdjsonReader.Read(Options(invalid))).ExitCode);
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
        var before = File.ReadAllBytes(valid);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var args = Arguments(valid, "text").Concat(new[] { "--input", bad }).ToArray();
        Assert.Equal(openFailure ? 1 : 2, CompareCommand.Execute(args, output, error));
        Assert.Equal("", output.ToString());
        Assert.DoesNotContain("Warning", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("secret full line", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(valid));
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
