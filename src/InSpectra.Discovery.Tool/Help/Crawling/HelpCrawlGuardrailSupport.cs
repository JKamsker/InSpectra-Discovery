namespace InSpectra.Discovery.Tool.Help.Crawling;

internal static class HelpCrawlGuardrailSupport
{
    public const int MaxCommandDepth = 8;
    public const int MaxCapturedCommands = 64;
    public const int MaxChildCommandsPerDocument = 48;
    public const int MaxTimedOutHelpInvocationsPerCommand = 3;
    public const int MaxPayloadCharacters = 262_144;
    public const int MaxPayloadLines = 4_000;

    public static bool TryValidatePayload(string payload, out string? failureMessage)
    {
        if (payload.Length > MaxPayloadCharacters)
        {
            failureMessage =
                $"Help parsing exceeded the per-capture budget of {MaxPayloadCharacters} characters.";
            return false;
        }

        var lineCount = 1;
        foreach (var character in payload)
        {
            if (character == '\n')
            {
                lineCount++;
            }
        }

        if (lineCount > MaxPayloadLines)
        {
            failureMessage =
                $"Help parsing exceeded the per-capture budget of {MaxPayloadLines} lines.";
            return false;
        }

        failureMessage = null;
        return true;
    }

    public static string BuildCaptureBudgetExceededMessage()
        => $"Help crawling exceeded the per-analysis budget of {MaxCapturedCommands} captured commands.";

    public static string BuildCommandFanoutExceededMessage(string commandName, int commandCount)
    {
        var displayName = string.IsNullOrWhiteSpace(commandName) ? "<root>" : commandName;
        return
            $"Help crawling exceeded the per-command child budget of {MaxChildCommandsPerDocument} commands at '{displayName}' ({commandCount} discovered).";
    }

    public static string BuildTimedOutHelpInvocationBudgetExceededMessage(string commandName)
    {
        var displayName = string.IsNullOrWhiteSpace(commandName) ? "<root>" : commandName;
        return
            $"Help crawling exceeded the per-command timeout budget of {MaxTimedOutHelpInvocationsPerCommand} timed-out help invocations at '{displayName}'.";
    }
}
