using DebugStudio.Client;
using DebugStudio.Contracts.Protocol;

namespace DebugStudio.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var parseResult = CliArgumentParser.Parse(args);
        if (!parseResult.Success || parseResult.ShowUsage)
        {
            if (!string.IsNullOrWhiteSpace(parseResult.ErrorMessage))
            {
                Console.Error.WriteLine(parseResult.ErrorMessage);
                Console.Error.WriteLine();
            }

            Console.Error.WriteLine(CliArgumentParser.GetUsageText());
            return parseResult.ExitCode;
        }

        var roundtripClient = new DebugCommandControlPlaneClient();

        var result = await roundtripClient.SendAsync(
                new DebugCommandRequest
                {
                    ServerUri = parseResult.Options!.ControlUri,
                    CommandType = parseResult.Options.CommandType,
                    PayloadJson = parseResult.Options.PayloadJson,
                    Timeout = parseResult.Options.Timeout,
                })
            .ConfigureAwait(false);

        return WriteResult(result);
    }

    private static int WriteResult(DebugCommandRoundtripResult result)
    {
        if (result.CommandResult is DebugCommandResultEnvelopeV1 commandResult)
        {
            var writer = commandResult.Success ? Console.Out : Console.Error;
            writer.WriteLine(
                $"CommandResult request={commandResult.RequestId} success={commandResult.Success.ToString().ToLowerInvariant()} message={FormatMessage(commandResult.Message)}");

            if (!string.IsNullOrWhiteSpace(commandResult.PayloadJson))
            {
                writer.WriteLine(commandResult.PayloadJson);
            }

            return commandResult.Success ? 0 : 1;
        }

        Console.Error.WriteLine(result.Detail);
        return result.Status == DebugCommandRoundtripStatus.TimedOut ? 2 : 1;
    }

    private static string FormatMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? "(no message)" : message.Trim();
    }
}
