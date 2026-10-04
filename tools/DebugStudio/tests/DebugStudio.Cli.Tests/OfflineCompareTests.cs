#nullable enable

using System.Globalization;
using CompareOptions = DebugStudio.Cli.Offline.CompareOptions;
using System.Text.Json;
using DebugStudio.Cli.Offline;
using DebugStudio.Export.Models;

namespace DebugStudio.Cli.Tests;

public sealed class OfflineCompareTests
{
    [Fact]
    public void Arguments_preserve_exact_ids_and_allow_only_repeated_inputs()
    {
        var result = CompareArguments.Parse(["compare", "--input", "a.ndjson", "--input", "a.ndjson",
            "--baseline-session", " base ", "--candidate-session", "Base", "--format", "json"]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(" base ", result.Options!.BaselineSession);
        Assert.Equal("Base", result.Options.CandidateSession);
        Assert.Equal(new[] { "a.ndjson", "a.ndjson" }, result.Options.InputFiles);
    }

    [Fact]
    public void Arguments_reject_identical_selected_ids_and_keep_default_text_format()
    {
        var args = new[] { "compare", "--input", "capture.ndjson", "--baseline-session", "same", "--candidate-session", "same" };
        Assert.Equal(2, CompareArguments.Parse(args).ExitCode);
        args[^1] = "Same";
        Assert.Equal("text", CompareArguments.Parse(args).Options!.Format);
    }

    [Theory]
    [InlineData("--format", "xml")]
    [InlineData("--unknown", "value")]
    [InlineData("--input", "-")]
    [InlineData("--input", "https://example.com/capture.ndjson")]
    [InlineData("--input", "file:///tmp/capture.ndjson")]
    [InlineData("--candidate-session", "base")]
    [InlineData("--baseline-session", "")]
    public void Arguments_reject_invalid_values_or_repeated_singletons(string option, string value)
    {
        var result = CompareArguments.Parse(["compare", "--input", "a", "--baseline-session", "base",
            "--candidate-session", "next", option, value]);
        Assert.Equal(2, result.ExitCode);
        Assert.Null(result.Options);
    }

    [Theory]
    [InlineData(@"\\server\share\capture.ndjson")]
    [InlineData(@"\\?\UNC\server\share\capture.ndjson")]
    [InlineData(@"\\?\C:\capture.ndjson")]
    [InlineData(@"\\.\pipe\capture")]
    [InlineData(@"\??\C:\capture.ndjson")]
    [InlineData(@"\\??\C:\capture.ndjson")]
    public void Arguments_and_command_reject_unc_and_device_paths_before_file_access(string path)
    {
        var args = new[] { "compare", "--input", path, "--baseline-session", "base", "--candidate-session", "next" };
        var parsed = CompareArguments.Parse(args);
        // Assert rejection first: a parser regression must fail without attempting to open the path.
        Assert.Equal(2, parsed.ExitCode);
        Assert.Null(parsed.Options);
        Assert.Contains("explicit local file", parsed.Error!, StringComparison.Ordinal);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(2, CompareCommand.Execute(args, output, error));
        Assert.Empty(output.ToString());
        Assert.Equal(parsed.Error + Environment.NewLine, error.ToString());
    }

    [Theory]
    [InlineData("//server/share/capture.ndjson")]
    [InlineData(@"/\server/share/capture.ndjson")]
    [InlineData(@"\/server/share/capture.ndjson")]
    public void Arguments_leading_slash_paths_follow_host_path_semantics(string path)
    {
        var result = CompareArguments.Parse(["compare", "--input", path, "--baseline-session", "base", "--candidate-session", "next"]);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(2, result.ExitCode);
            Assert.Null(result.Options);
        }
        else
        {
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(path, Assert.Single(result.Options!.InputFiles));
        }
    }

    [Theory]
    [InlineData(@"C:\captures\capture.ndjson")]
    [InlineData("C:/captures/capture.ndjson")]
    [InlineData("capture.ndjson")]
    [InlineData("./captures/capture.ndjson")]
    [InlineData(@"captures\capture.ndjson")]
    [InlineData("/tmp/captures/capture.ndjson")]
    public void Arguments_preserve_normal_drive_relative_and_posix_paths(string path)
    {
        var result = CompareArguments.Parse(["compare", "--input", path, "--baseline-session", "base", "--candidate-session", "next"]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(path, Assert.Single(result.Options!.InputFiles));
    }

    [Theory]
    [InlineData("compare")]
    [InlineData("compare", "--input")]
    [InlineData("compare", "--input", "--baseline-session", "base")]
    [InlineData("compare", "unexpected")]
    public void Arguments_reject_missing_values_and_positional_extras(params string[] args)
    {
        Assert.Equal(2, CompareArguments.Parse(args).ExitCode);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void Arguments_compare_help_succeeds(string token)
    {
        var result = CompareArguments.Parse(["compare", token]);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ShowUsage);
    }

    [Fact]
    public void Metrics_keep_stage_target_population_and_all_outcomes_separate()
    {
        var report = Report(
            Span("base", "AppStartup", "AfterSceneLoad", 0, success: false),
            Span("base", "AppStartup", "AfterSceneLoad", 10),
            Span("base", "AppStartup", "AfterSceneLoad", 20),
            Span("next", "AppStartup", "AfterSceneLoad", 30),
            Span("next", "AppStartup", "AfterSceneLoad", 40),
            Span("next", "AppStartup", "BeforeSceneLoad", 2),
            Span("base", "SceneLoad", "scene-a", 4),
            Span("next", "SceneLoad", "scene-b", 0),
            Span("base", "AppStartup", "excluded-key", null),
            Span("next", "SceneLoad", null, 6),
            Span("base", "AppStartup", "sample-only", 900, kind: "sample"),
            Span("base", "appstartup", "wrong-case", 900));

        Assert.Equal(new[] { "AfterSceneLoad", "BeforeSceneLoad" }, report.Startup.Select(group => group.Key));
        Assert.Equal(new DurationSummary(3, 10, 20), report.Startup[0].Baseline);
        Assert.Equal(new DurationSummary(2, 35, 40), report.Startup[0].Candidate);
        Assert.Equal(new DurationDelta(-1, 25, 20), report.Startup[0].Delta);
        Assert.Equal(new DurationSummary(0, null, null), report.Startup[1].Baseline);
        Assert.Equal(new DurationDelta(1, null, null), report.Startup[1].Delta);
        Assert.Equal(new[] { "scene-a", "scene-b" }, report.Scenes.Select(group => group.Key));
        Assert.Equal(1, report.BaselineCoverage.ExcludedStartupRows);
        Assert.Equal(1, report.CandidateCoverage.ExcludedSceneRows);
        Assert.Contains("whole-span", report.StartupDescription, StringComparison.Ordinal);
        Assert.Contains("last attempted step", report.StartupDescription, StringComparison.Ordinal);
        Assert.Contains("already-active", report.SceneDescription, StringComparison.Ordinal);
        Assert.Contains("regardless of isSuccess", report.OutcomesDescription, StringComparison.Ordinal);
    }

    [Fact]
    public void Metrics_optional_null_keys_durations_and_empty_keys_are_exclusions_not_new_input_categories()
    {
        var report = Report(Span("base", "AppStartup", null, 10), Span("next", "AppStartup", "", 10),
            Span("base", "SceneLoad", "scene", null), Span("next", "SceneLoad", null, null));
        Assert.Empty(report.Startup);
        Assert.Empty(report.Scenes);
        Assert.Equal(1, report.BaselineCoverage.ExcludedStartupRows);
        Assert.Equal(1, report.CandidateCoverage.ExcludedStartupRows);
        Assert.Equal(1, report.BaselineCoverage.ExcludedSceneRows);
        Assert.Equal(1, report.CandidateCoverage.ExcludedSceneRows);
        Assert.Equal(4, report.InputCounts.SelectedUniqueRows);
        Assert.Equal(4, report.Diagnostics.Count(diagnostic => diagnostic.Code.StartsWith("excluded-", StringComparison.Ordinal)));
    }

    [Fact]
    public void Metrics_even_median_is_overflow_safe_and_groups_sort_ordinal()
    {
        var report = Report(Span("base", "AppStartup", "a", double.MaxValue),
            Span("base", "AppStartup", "a", double.MaxValue),
            Span("next", "AppStartup", "a", 0), Span("next", "AppStartup", "a", double.MaxValue),
            Span("next", "AppStartup", "Z", 1));
        Assert.Equal(new[] { "Z", "a" }, report.Startup.Select(group => group.Key));
        Assert.Equal(double.MaxValue, report.Startup[1].Baseline.MedianMs);
        Assert.Equal(double.MaxValue / 2, report.Startup[1].Candidate.MedianMs);
        Assert.True(double.IsFinite(report.Startup[1].Delta.MedianMs!.Value));
    }

    [Fact]
    public void Bottleneck_counts_all_telemetry_kinds_once_by_union_and_marks_unknown_coverage()
    {
        var report = Report(Span("base", "other", null, null, kind: "sample", tags: ["Bottleneck"], bits: 1),
            Span("base", "other", null, null, kind: "event", tags: ["Bottleneck"], bits: 0),
            Span("next", "other", null, null, kind: "event", tags: [], bits: 1),
            Span("next", "other", null, null, tags: ["bottleneck"], bits: 0),
            Span("next", "other", null, null));
        Assert.Equal(new CountComparison(2, 1, -1), report.Bottleneck);
        Assert.Equal(2, report.Diagnostics.Count(diagnostic => diagnostic.Code == "tag-disagreement"));
        Assert.Equal(0, report.BaselineCoverage.UnknownTagRows);
        Assert.Equal(1, report.CandidateCoverage.UnknownTagRows);
        Assert.Single(report.Diagnostics, diagnostic => diagnostic.Code == "unknown-tags");
    }

    [Fact]
    public void Writer_uses_invariant_numbers_escapes_controls_and_keeps_json_nulls()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var options = new CompareOptions([], "base\n", "next\t", "text");
            var report = Report(options, Span("base\n", "SceneLoad", "scene\r", 1.5),
                Span("next\t", "other", null, null));
            using var text = new StringWriter();
            ComparisonReportWriter.Write(report, "text", text);
            Assert.Contains("base\\u000a", text.ToString(), StringComparison.Ordinal);
            Assert.Contains("next\\u0009", text.ToString(), StringComparison.Ordinal);
            Assert.Contains("scene\\u000d", text.ToString(), StringComparison.Ordinal);
            Assert.Contains("medianMs=1.5", text.ToString(), StringComparison.Ordinal);
            Assert.Contains("medianMs=n/a", text.ToString(), StringComparison.Ordinal);
            Assert.Contains("AppStartup: no observations", text.ToString(), StringComparison.Ordinal);
            using var warnings = new StringWriter();
            ComparisonReportWriter.WriteDiagnostics(report.Diagnostics, warnings);
            Assert.Contains("capture\\u000a.ndjson", warnings.ToString(), StringComparison.Ordinal);
            using var repeated = new StringWriter();
            ComparisonReportWriter.Write(report, "text", repeated);
            Assert.Equal(text.ToString(), repeated.ToString());
            using var json = new StringWriter();
            ComparisonReportWriter.Write(report, "json", json);
            using var document = JsonDocument.Parse(json.ToString());
            Assert.Equal(1, document.RootElement.GetProperty("reportVersion").GetInt32());
            Assert.Equal("base\n", document.RootElement.GetProperty("baselineSession").GetString());
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("scenes")[0].GetProperty("candidate").GetProperty("medianMs").ValueKind);
            Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("scenes")[0].GetProperty("delta").GetProperty("maxMs").ValueKind);
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    [Fact]
    public void Writer_large_finite_values_remain_valid_json()
    {
        var report = Report(Span("base", "AppStartup", "stage", double.MaxValue),
            Span("next", "AppStartup", "stage", double.MaxValue));
        using var output = new StringWriter();
        ComparisonReportWriter.Write(report, "json", output);
        using var document = JsonDocument.Parse(output.ToString());
        Assert.Equal(double.MaxValue, document.RootElement.GetProperty("startup")[0].GetProperty("baseline").GetProperty("medianMs").GetDouble());
    }

    private static ComparisonReport Report(params TelemetryExportRecord[] records) => Report(TelemetryNdjsonReaderTests.Options(), records);
    private static ComparisonReport Report(CompareOptions options, params TelemetryExportRecord[] records) => RunComparison.Create(options,
        new(records.Select((record, index) => new LocatedTelemetry(record, new("capture\n.ndjson", index + 1))).ToArray(),
            new(records.Length, 0, records.Length, 0, 0, 0, 0, records.Length), []));

    private static TelemetryExportRecord Span(string session, string name, string? key, double? duration,
        bool success = true, string kind = "span", string[]? tags = null, int? bits = null) => new()
    {
        TimestampUtc = "now", TimestampUnixTimeMilliseconds = 1, Stream = "telemetry", SchemaVersion = 3,
        SessionId = session, Name = name, Kind = kind, IsSuccess = success, ElapsedMs = duration,
        Payload = new() { Stage = key, TargetIdentity = key }, Tags = tags, TagBits = bits,
    };

}
