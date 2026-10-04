#nullable enable

using DebugStudio.Contracts.Schema;

namespace DebugStudio.Cli.Offline;

internal sealed record DurationSummary(long Count, double? MedianMs, double? MaxMs);
internal sealed record DurationDelta(long Count, double? MedianMs, double? MaxMs);
internal sealed record DurationComparison(string Key, DurationSummary Baseline, DurationSummary Candidate, DurationDelta Delta);
internal sealed record RunCoverage(long SelectedRows, long MissingSequenceRows, long UnknownTagRows,
    long ExcludedStartupRows, long ExcludedSceneRows);
internal sealed record CountComparison(long Baseline, long Candidate, long Delta);
internal sealed record ComparisonReport(int ReportVersion, string BaselineSession, string CandidateSession,
    InputAccounting InputCounts, RunCoverage BaselineCoverage, RunCoverage CandidateCoverage,
    IReadOnlyList<DurationComparison> Startup, IReadOnlyList<DurationComparison> Scenes,
    CountComparison Bottleneck, IReadOnlyList<ComparisonDiagnostic> Diagnostics,
    string StartupDescription, string SceneDescription, string OutcomesDescription, string CoverageDescription);

internal static class RunComparison
{
    internal const string StartupDescription = "AppStartup whole-span durations grouped by stage label; BeforeSceneLoad/AfterSceneLoad label the whole span, and a failure stage is the last attempted step, not an individual step duration.";
    internal const string SceneDescription = "SceneLoad observed spans/calls, including already-active short-circuit calls; these observations do not establish completed cold-load durations.";
    internal const string OutcomesDescription = "All valid observed durations are included, regardless of isSuccess. Deltas are candidate minus baseline.";
    internal const string CoverageDescription = "Observed rows only. Missing sequences limit deduplication; missing tags mean unknown Bottleneck coverage. Sequence gaps do not establish telemetry loss, and absent observations are not zero durations.";

    internal static ComparisonReport Create(CompareOptions options, TelemetryInput input)
    {
        var diagnostics = input.Diagnostics.ToList();
        var baseline = Summarize(options.BaselineSession, input.SelectedRows, diagnostics);
        var candidate = Summarize(options.CandidateSession, input.SelectedRows, diagnostics);
        return new(1, options.BaselineSession, options.CandidateSession, input.Counts,
            baseline.Coverage, candidate.Coverage, Join(baseline.Startup, candidate.Startup),
            Join(baseline.Scenes, candidate.Scenes),
            new(baseline.Bottleneck, candidate.Bottleneck, candidate.Bottleneck - baseline.Bottleneck),
            diagnostics, StartupDescription, SceneDescription, OutcomesDescription, CoverageDescription);
    }

    private sealed record ObservedRun(Dictionary<string, List<double>> Startup, Dictionary<string, List<double>> Scenes,
        RunCoverage Coverage, long Bottleneck);

    private static ObservedRun Summarize(string session, IReadOnlyList<LocatedTelemetry> rows, List<ComparisonDiagnostic> diagnostics)
    {
        var startup = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        var scenes = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        long selected = 0, missingSequence = 0, unknownTags = 0, excludedStartup = 0, excludedScene = 0, bottleneck = 0;
        foreach (var row in rows.Where(row => row.Record.SessionId == session))
        {
            selected++;
            var record = row.Record;
            if (record.ProducerSequence is null) missingSequence++;
            var tagged = record.Tags?.Contains("Bottleneck", StringComparer.Ordinal) ?? false;
            var bitTagged = record.TagBits is int bits && (bits & (int)DebugTelemetryTagBits.Bottleneck) != 0;
            if (tagged || bitTagged) bottleneck++;
            if (record.Tags is null && record.TagBits is null)
            {
                unknownTags++;
                diagnostics.Add(new("unknown-tags", "Missing tags and tagBits: Bottleneck coverage is unknown for this row.", row.Location));
            }
            else if (record.Tags is not null && record.TagBits is not null && tagged != bitTagged)
            {
                diagnostics.Add(new("tag-disagreement", "tags and tagBits disagree on Bottleneck; the union is counted once.", row.Location));
            }

            if (record.Kind != "span") continue;
            if (record.Name == "AppStartup")
            {
                if (!Add(startup, record.Payload?.Stage, record.ElapsedMs))
                {
                    excludedStartup++;
                    diagnostics.Add(new("excluded-startup", "AppStartup span lacks a nonempty payload.stage or valid elapsedMs and is excluded from duration metrics.", row.Location));
                }
            }
            else if (record.Name == "SceneLoad")
            {
                if (!Add(scenes, record.Payload?.TargetIdentity, record.ElapsedMs))
                {
                    excludedScene++;
                    diagnostics.Add(new("excluded-scene", "SceneLoad span lacks a nonempty payload.targetIdentity or valid elapsedMs and is excluded from duration metrics.", row.Location));
                }
            }
        }

        return new(startup, scenes, new(selected, missingSequence, unknownTags, excludedStartup, excludedScene), bottleneck);
    }

    private static bool Add(Dictionary<string, List<double>> groups, string? key, double? elapsed)
    {
        if (string.IsNullOrEmpty(key) || elapsed is not double value || !double.IsFinite(value) || value < 0) return false;
        if (!groups.TryGetValue(key, out var values)) groups.Add(key, values = []);
        values.Add(value);
        return true;
    }

    private static IReadOnlyList<DurationComparison> Join(Dictionary<string, List<double>> baseline, Dictionary<string, List<double>> candidate)
    {
        return baseline.Keys.Union(candidate.Keys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal)
            .Select(key =>
            {
                var before = Describe(baseline.GetValueOrDefault(key));
                var after = Describe(candidate.GetValueOrDefault(key));
                return new DurationComparison(key, before, after,
                    new(after.Count - before.Count, after.MedianMs - before.MedianMs, after.MaxMs - before.MaxMs));
            }).ToArray();
    }

    private static DurationSummary Describe(List<double>? values)
    {
        if (values is null || values.Count == 0) return new(0, null, null);
        values.Sort();
        var middle = values.Count / 2;
        // Values are nonnegative and ordered. This midpoint cannot overflow like (low + high) / 2.
        var median = values.Count % 2 != 0 ? values[middle] : values[middle - 1] + (values[middle] - values[middle - 1]) / 2;
        return new(values.Count, median, values[^1]);
    }
}
