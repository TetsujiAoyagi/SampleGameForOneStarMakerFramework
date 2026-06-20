#nullable enable

using System;
using System.Globalization;
using DebugStudio.Client;

namespace DebugStudio.Cli;

public static class CliArgumentParser
{
    private static readonly Uri DefaultControlUri = DebugStudioControlPlaneDefaults.DefaultControlUri;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    public static CliParseResult Parse(string[] args)
    {
        if (args is null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (args.Length == 0)
        {
            return CliParseResult.Usage("A subcommand is required.", exitCode: 2);
        }

        if (IsHelpToken(args[0]))
        {
            return CliParseResult.Usage(exitCode: 0);
        }

        if (!string.Equals(args[0], "send", StringComparison.OrdinalIgnoreCase))
        {
            return CliParseResult.Usage($"Unsupported subcommand '{args[0]}'.", exitCode: 2);
        }

        Uri controlUri = DefaultControlUri;
        string? commandType = null;
        var payloadJson = "{}";
        var timeout = DefaultTimeout;

        for (var index = 1; index < args.Length; index++)
        {
            var token = args[index];
            if (IsHelpToken(token))
            {
                return CliParseResult.Usage(exitCode: 0);
            }

            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                return CliParseResult.Usage($"Unexpected argument '{token}'.", exitCode: 2);
            }

            if (index + 1 >= args.Length)
            {
                return CliParseResult.Usage($"Missing value for option '{token}'.", exitCode: 2);
            }

            var value = args[++index];
            switch (token)
            {
                case "--control-uri":
                case "--uri":
                    if (!Uri.TryCreate(value, UriKind.Absolute, out var parsedUri) ||
                        parsedUri.Scheme is not ("ws" or "wss"))
                    {
                        return CliParseResult.Usage("The --control-uri value must be an absolute ws:// or wss:// URI.", exitCode: 2);
                    }

                    controlUri = parsedUri;
                    break;

                case "--command":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return CliParseResult.Usage("The --command value cannot be empty.", exitCode: 2);
                    }

                    commandType = value.Trim();
                    break;

                case "--payload":
                    payloadJson = value ?? "{}";
                    break;

                case "--timeout-seconds":
                    if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ||
                        seconds <= 0)
                    {
                        return CliParseResult.Usage("The --timeout-seconds value must be a positive number.", exitCode: 2);
                    }

                    timeout = TimeSpan.FromSeconds(seconds);
                    break;

                default:
                    return CliParseResult.Usage($"Unsupported option '{token}'.", exitCode: 2);
            }
        }

        if (string.IsNullOrWhiteSpace(commandType))
        {
            return CliParseResult.Usage("The --command option is required.", exitCode: 2);
        }

        return CliParseResult.Ok(new SendCommandCliOptions(controlUri, commandType, payloadJson, timeout));
    }

    public static string GetUsageText()
    {
        return """
Usage:
  debugstudio-cli send --command <name> [--control-uri <ws-uri>] [--payload <json>] [--timeout-seconds <seconds>]

Example:
  debugstudio-cli send --control-uri ws://127.0.0.1:5012/cli-control/ --command debugsocket.ping --payload "{}"
""";
    }

    private static bool IsHelpToken(string token)
    {
        return string.Equals(token, "--help", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(token, "-h", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(token, "help", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record SendCommandCliOptions(
    Uri ControlUri,
    string CommandType,
    string PayloadJson,
    TimeSpan Timeout);

public sealed class CliParseResult
{
    private CliParseResult(bool success, bool showUsage, int exitCode, string? errorMessage, SendCommandCliOptions? options)
    {
        Success = success;
        ShowUsage = showUsage;
        ExitCode = exitCode;
        ErrorMessage = errorMessage;
        Options = options;
    }

    public bool Success { get; }

    public bool ShowUsage { get; }

    public int ExitCode { get; }

    public string? ErrorMessage { get; }

    public SendCommandCliOptions? Options { get; }

    public static CliParseResult Ok(SendCommandCliOptions options)
    {
        return new CliParseResult(success: true, showUsage: false, exitCode: 0, errorMessage: null, options);
    }

    public static CliParseResult Usage(string? errorMessage = null, int exitCode = 0)
    {
        return new CliParseResult(success: false, showUsage: true, exitCode, errorMessage, options: null);
    }
}
