#nullable enable

namespace DebugStudio.Cli.Offline;

internal sealed record CompareOptions(string[] InputFiles, string BaselineSession, string CandidateSession, string Format);

internal sealed record CompareParseResult(CompareOptions? Options, int ExitCode, string? Error, bool ShowUsage);

internal static class CompareArguments
{
    internal const string Synopsis = "debugstudio-cli compare --input <file.ndjson> [--input <other.ndjson> ...] --baseline-session <id> --candidate-session <id> [--format text|json]";

    internal static CompareParseResult Parse(string[] args)
    {
        var files = new List<string>();
        var singletons = new HashSet<string>(StringComparer.Ordinal);
        string? baseline = null;
        string? candidate = null;
        var format = "text";

        for (var index = 1; index < args.Length; index++)
        {
            var option = args[index];
            if (option.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
                option.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                return new(null, 0, null, true);
            }

            if (option is not ("--input" or "--baseline-session" or "--candidate-session" or "--format"))
            {
                return Error($"Unsupported compare argument '{option}'.");
            }

            if (option != "--input" && !singletons.Add(option))
            {
                return Error($"Option '{option}' cannot be repeated.");
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                return Error($"Missing value for option '{option}'.");
            }

            var value = args[++index];
            if (value.Length == 0)
            {
                return Error($"The '{option}' value cannot be empty.");
            }

            switch (option)
            {
                case "--input":
                    if (IsRemoteOrDevicePath(value) || value == "-" || value.Contains("://", StringComparison.Ordinal) ||
                        Uri.TryCreate(value, UriKind.Absolute, out var uri) && !uri.IsFile)
                    {
                        return Error("The --input value must name an explicit local file, not stdin, a URL, UNC path or Windows device namespace.");
                    }

                    files.Add(value);
                    break;
                case "--baseline-session": baseline = value; break;
                case "--candidate-session": candidate = value; break;
                case "--format":
                    if (value is not ("text" or "json"))
                    {
                        return Error("The --format value must be text or json.");
                    }

                    format = value;
                    break;
            }
        }

        if (files.Count == 0 || baseline is null || candidate is null)
        {
            return Error("Compare requires --input, --baseline-session and --candidate-session.");
        }

        if (string.Equals(baseline, candidate, StringComparison.Ordinal))
        {
            return Error("Baseline and candidate session IDs must be distinct.");
        }

        return new(new(files.ToArray(), baseline, candidate, format), 0, null, false);
    }

    private static bool IsRemoteOrDevicePath(string value)
    {
        // Reject Windows UNC/device spelling before any file-system call, even on another platform.
        if (value.StartsWith(@"\\", StringComparison.Ordinal) || value.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            return true;
        }

        // Windows also treats forward or mixed leading separators as UNC. POSIX permits // paths.
        return OperatingSystem.IsWindows() && value.Length >= 2 &&
            IsWindowsSeparator(value[0]) && IsWindowsSeparator(value[1]);
    }

    private static bool IsWindowsSeparator(char value) => value is '\\' or '/';

    private static CompareParseResult Error(string message) => new(null, 2, message, true);
}
