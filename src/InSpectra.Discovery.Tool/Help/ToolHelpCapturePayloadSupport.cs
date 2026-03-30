using System.Text.Json.Nodes;

internal static class ToolHelpCapturePayloadSupport
{
    public static SelectedToolHelpCapture? SelectBestDocument(
        ToolHelpTextParser parser,
        string rootCommandName,
        JsonObject capture)
    {
        var storedCommand = capture["command"]?.GetValue<string>() ?? string.Empty;
        var helpInvocation = capture["helpInvocation"]?.GetValue<string>();
        SelectedToolHelpCapture? bestCandidate = null;
        var bestScore = int.MinValue;

        foreach (var payload in EnumeratePayloadCandidates(capture))
        {
            var document = parser.Parse(payload);
            var commandSegments = ToolHelpCommandPathSupport.ResolveStoredCaptureSegments(rootCommandName, storedCommand, document);
            if (!document.HasContent || !ToolHelpDocumentInspector.IsCompatible(commandSegments, document))
            {
                continue;
            }

            var score = ScorePayloadCandidate(storedCommand, document, helpInvocation, payload);
            if (score <= bestScore)
            {
                continue;
            }

            var commandKey = commandSegments.Length == 0 ? string.Empty : string.Join(' ', commandSegments);
            bestCandidate = new SelectedToolHelpCapture(commandKey, document);
            bestScore = score;
        }

        return bestCandidate;
    }

    public static SelectedToolHelpPayload SelectBestProcessCapture(
        ToolHelpTextParser parser,
        IReadOnlyList<string> commandSegments,
        IReadOnlyList<string> invokedArguments,
        CommandRuntime.ProcessResult processResult)
    {
        var helpInvocation = invokedArguments.Count == 0
            ? null
            : string.Join(' ', invokedArguments);
        var candidates = EnumeratePayloadCandidates(processResult);
        ToolHelpDocument? bestDocument = null;
        var bestPayload = candidates.FirstOrDefault();
        var bestScore = -1;

        foreach (var payload in candidates)
        {
            var document = parser.Parse(payload);
            var compatibleDocument = document.HasContent && ToolHelpDocumentInspector.IsCompatible(commandSegments, document)
                ? document
                : null;
            var score = compatibleDocument is not null
                ? ToolHelpDocumentInspector.Score(compatibleDocument) - GetPayloadSelectionPenalty(payload, helpInvocation)
                : 0;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestDocument = compatibleDocument;
            bestPayload = payload;
        }

        var isTerminalNonHelp = bestDocument is null && candidates.Any(ToolHelpDocumentInspector.LooksLikeTerminalNonHelpPayload);
        return new(bestDocument, bestPayload, isTerminalNonHelp);
    }

    private static int ScorePayloadCandidate(
        string storedCommand,
        ToolHelpDocument document,
        string? helpInvocation,
        string payload)
    {
        var score = ToolHelpDocumentInspector.Score(document) - GetPayloadSelectionPenalty(payload, helpInvocation);
        if (string.IsNullOrWhiteSpace(storedCommand))
        {
            return score;
        }

        var storedCommandLeaf = ToolHelpCommandPathSupport.SplitSegments(storedCommand).LastOrDefault();
        var hasLeafSurface = document.Options.Count > 0 || document.Arguments.Count > 0;
        if (!hasLeafSurface
            && document.Commands.Count > 0
            && (string.Equals(storedCommandLeaf, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(storedCommandLeaf, "version", StringComparison.OrdinalIgnoreCase)))
        {
            return int.MinValue;
        }

        if (hasLeafSurface)
        {
            score += 20;
        }

        if (!hasLeafSurface && document.Commands.Count > 0)
        {
            score -= 10;
        }

        return score;
    }

    private static int GetPayloadSelectionPenalty(string payload, string? helpInvocation)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(helpInvocation))
        {
            return 0;
        }

        var lines = payload
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
        var edgeLines = lines
            .Take(4)
            .Concat(lines.TakeLast(4))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return edgeLines.Any(line => string.Equals(line, helpInvocation, StringComparison.OrdinalIgnoreCase))
            ? 50
            : 0;
    }

    private static IReadOnlyList<string> EnumeratePayloadCandidates(CommandRuntime.ProcessResult processResult)
        => EnumeratePayloadCandidates(
            storedPayload: null,
            stdout: CommandRuntime.NormalizeConsoleText(processResult.Stdout),
            stderr: CommandRuntime.NormalizeConsoleText(processResult.Stderr));

    private static IReadOnlyList<string> EnumeratePayloadCandidates(JsonObject capture)
        => EnumeratePayloadCandidates(
            storedPayload: CommandRuntime.NormalizeConsoleText(capture["payload"]?.GetValue<string>()),
            stdout: capture["result"] is JsonObject processResult
                ? CommandRuntime.NormalizeConsoleText(processResult["stdout"]?.GetValue<string>())
                : null,
            stderr: capture["result"] is JsonObject processResultValue
                ? CommandRuntime.NormalizeConsoleText(processResultValue["stderr"]?.GetValue<string>())
                : null);

    private static IReadOnlyList<string> EnumeratePayloadCandidates(string? storedPayload, string? stdout, string? stderr)
    {
        var payloads = new List<string>();
        if (!string.IsNullOrWhiteSpace(storedPayload))
        {
            payloads.Add(storedPayload);
        }

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            payloads.Add(stdout);
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            payloads.Add(stderr);
        }

        if (!string.IsNullOrWhiteSpace(stdout) && !string.IsNullOrWhiteSpace(stderr))
        {
            payloads.Add($"{stdout}\n{stderr}");
            payloads.Add($"{stderr}\n{stdout}");
        }

        return payloads.Distinct(StringComparer.Ordinal).ToArray();
    }
}

internal sealed record SelectedToolHelpCapture(string CommandKey, ToolHelpDocument Document);
internal sealed record SelectedToolHelpPayload(ToolHelpDocument? Document, string? Payload, bool IsTerminalNonHelp);
