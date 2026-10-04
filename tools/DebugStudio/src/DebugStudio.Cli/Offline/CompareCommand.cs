#nullable enable

namespace DebugStudio.Cli.Offline;

internal static class CompareCommand
{
    internal static int Execute(string[] args, TextWriter output, TextWriter error)
    {
        var parsed = CompareArguments.Parse(args);
        if (parsed.Options is null)
        {
            if (parsed.Error is not null) error.WriteLine(TelemetryNdjsonReader.Escape(parsed.Error));
            else if (parsed.ShowUsage) error.WriteLine(CompareArguments.Synopsis);
            return parsed.ExitCode;
        }

        try
        {
            // Nothing is published until every supplied file has passed admission and duplicate checks.
            var input = TelemetryNdjsonReader.Read(parsed.Options);
            var report = RunComparison.Create(parsed.Options, input);
            ComparisonReportWriter.Write(report, parsed.Options.Format, output);
            ComparisonReportWriter.WriteDiagnostics(report.Diagnostics, error);
            return 0;
        }
        catch (ComparisonInputException ex)
        {
            error.WriteLine(ex.Message);
            return ex.ExitCode;
        }
    }
}
