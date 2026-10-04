#nullable enable

using System.Globalization;
using System.Text.Json;

namespace DebugStudio.Cli.Offline;

internal static class ComparisonReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    internal static void Write(ComparisonReport report, string format, TextWriter output)
    {
        if (format == "json")
        {
            output.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
            return;
        }

        output.WriteLine($"Observed comparison: baseline '{Escape(report.BaselineSession)}', candidate '{Escape(report.CandidateSession)}'");
        output.WriteLine(report.OutcomesDescription);
        var counts = report.InputCounts;
        output.WriteLine(FormattableString.Invariant($"Input rows: nonblank={counts.NonblankRows}, blank={counts.BlankRows}, selected retained={counts.SelectedUniqueRows}, exact duplicates={counts.ExactDuplicateRows}, serviceStatus skipped={counts.SkippedServiceStatusRows}, unassigned={counts.UnassignedTelemetryRows}, other sessions={counts.OtherSessionTelemetryRows}"));
        output.WriteLine(FormattableString.Invariant($"Deduplication coverage: selected rows without producerSequence={counts.MissingSequenceRows}"));
        WriteCoverage("baseline", report.BaselineCoverage, output);
        WriteCoverage("candidate", report.CandidateCoverage, output);
        WriteGroups("AppStartup", report.StartupDescription, report.Startup, output);
        WriteGroups("SceneLoad", report.SceneDescription, report.Scenes, output);
        output.WriteLine(FormattableString.Invariant($"Observed Bottleneck telemetry rows: baseline={report.Bottleneck.Baseline}, candidate={report.Bottleneck.Candidate}, delta={report.Bottleneck.Delta}"));
        output.WriteLine(report.CoverageDescription);
        output.WriteLine(FormattableString.Invariant($"Diagnostics: {report.Diagnostics.Count} (written to stderr)"));
    }

    internal static void WriteDiagnostics(IReadOnlyList<ComparisonDiagnostic> diagnostics, TextWriter error)
    {
        foreach (var diagnostic in diagnostics)
        {
            var location = diagnostic.Location is null ? "" : TelemetryNdjsonReader.Format(diagnostic.Location) + ": ";
            var related = diagnostic.RelatedLocation is null ? "" : $" First seen at {TelemetryNdjsonReader.Format(diagnostic.RelatedLocation)}.";
            error.WriteLine($"Warning [{diagnostic.Code}]: {location}{diagnostic.Message}{related}");
        }
    }

    private static void WriteCoverage(string run, RunCoverage coverage, TextWriter output) => output.WriteLine(
        FormattableString.Invariant($"{run} coverage: selected={coverage.SelectedRows}, missing sequence={coverage.MissingSequenceRows}, unknown tags={coverage.UnknownTagRows}, excluded startup={coverage.ExcludedStartupRows}, excluded scene={coverage.ExcludedSceneRows}"));

    private static void WriteGroups(string name, string description, IReadOnlyList<DurationComparison> groups, TextWriter output)
    {
        output.WriteLine(description);
        if (groups.Count == 0)
        {
            output.WriteLine($"{name}: no observations.");
            return;
        }

        foreach (var group in groups)
        {
            output.WriteLine(FormattableString.Invariant($"{name} '{Escape(group.Key)}': baseline count={group.Baseline.Count} medianMs={Number(group.Baseline.MedianMs)} maxMs={Number(group.Baseline.MaxMs)}; candidate count={group.Candidate.Count} medianMs={Number(group.Candidate.MedianMs)} maxMs={Number(group.Candidate.MaxMs)}; delta count={group.Delta.Count} medianMs={Number(group.Delta.MedianMs)} maxMs={Number(group.Delta.MaxMs)}"));
        }
    }

    private static string Number(double? number) => number?.ToString("R", CultureInfo.InvariantCulture) ?? "n/a";
    private static string Escape(string value) => TelemetryNdjsonReader.Escape(value);
}
