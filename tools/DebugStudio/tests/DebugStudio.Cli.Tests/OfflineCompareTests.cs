#nullable enable

using DebugStudio.Cli.Offline;

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

    [Theory]
    [InlineData("--format", "xml")]
    [InlineData("--unknown", "value")]
    [InlineData("--input", "-")]
    [InlineData("--input", "https://example.com/capture.ndjson")]
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
}
