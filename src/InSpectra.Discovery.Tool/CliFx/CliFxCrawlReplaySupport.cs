using System.Text.Json.Nodes;

internal static class CliFxCrawlReplaySupport
{
    public static CliFxReplayedCapture? TryReplayCapture(CliFxHelpTextParser parser, JsonObject capture)
    {
        var payload = ExtractPayload(capture);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var document = parser.Parse(payload);
        if (!HasContent(document))
        {
            return null;
        }

        return new CliFxReplayedCapture(
            capture["command"]?.GetValue<string>() ?? string.Empty,
            document);
    }

    public static CliFxHelpDocument CreateEmptyRootDocument()
        => new(
            Title: null,
            Version: null,
            ApplicationDescription: null,
            CommandDescription: null,
            UsageLines: [],
            Parameters: [],
            Options: [],
            Commands: []);

    private static string? ExtractPayload(JsonObject capture)
    {
        var payload = CommandRuntime.NormalizeConsoleText(capture["payload"]?.GetValue<string>());
        return !string.IsNullOrWhiteSpace(payload)
            ? payload
            : SelectBestPayload(capture["result"] as JsonObject);
    }

    private static string? SelectBestPayload(JsonObject? processResult)
    {
        if (processResult is null)
        {
            return null;
        }

        var stdout = CommandRuntime.NormalizeConsoleText(processResult["stdout"]?.GetValue<string>());
        var stderr = CommandRuntime.NormalizeConsoleText(processResult["stderr"]?.GetValue<string>());

        if (LooksLikeHelp(stdout))
        {
            return stdout;
        }

        if (LooksLikeHelp(stderr))
        {
            return stderr;
        }

        return stdout ?? stderr;
    }

    private static bool LooksLikeHelp(string? text)
        => !string.IsNullOrWhiteSpace(text)
            && (text.Contains("\nUSAGE\n", StringComparison.Ordinal)
                || text.Contains("\nOPTIONS\n", StringComparison.Ordinal)
                || text.Contains("\nCOMMANDS\n", StringComparison.Ordinal));

    private static bool HasContent(CliFxHelpDocument document)
        => !string.IsNullOrWhiteSpace(document.Title)
            || !string.IsNullOrWhiteSpace(document.Version)
            || !string.IsNullOrWhiteSpace(document.ApplicationDescription)
            || !string.IsNullOrWhiteSpace(document.CommandDescription)
            || document.UsageLines.Count > 0
            || document.Parameters.Count > 0
            || document.Options.Count > 0
            || document.Commands.Count > 0;
}

internal sealed record CliFxReplayedCapture(string CommandKey, CliFxHelpDocument Document);
