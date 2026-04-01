namespace InSpectra.Discovery.Tool.Help.Crawling;

using InSpectra.Discovery.Tool.Help.OpenCli;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Signatures;

using InSpectra.Discovery.Tool.Infrastructure.Commands;

using InSpectra.Discovery.Tool.Help.Parsing;

using System.Text.Json.Nodes;

internal static class CapturePayloadSupport
{
    public static SelectedCapture? SelectBestDocument(
        TextParser parser,
        string rootCommandName,
        JsonObject capture)
    {
        var storedCommand = capture["command"]?.GetValue<string>() ?? string.Empty;
        var helpInvocation = capture["helpInvocation"]?.GetValue<string>();
        SelectedCapture? bestCandidate = null;
        var bestScore = int.MinValue;

        foreach (var payload in EnumeratePayloadCandidates(capture))
        {
            var document = parser.Parse(payload);
            var commandSegments = CommandPathSupport.ResolveStoredCaptureSegments(rootCommandName, storedCommand, document);
            if (!document.HasContent || !DocumentInspector.IsCompatible(commandSegments, document))
            {
                continue;
            }

            if (ShouldRejectNonRootDispatcherEcho(rootCommandName, storedCommand, commandSegments, document))
            {
                continue;
            }

            var score = ScorePayloadCandidate(storedCommand, document, helpInvocation, payload);
            if (score <= bestScore)
            {
                continue;
            }

            var commandKey = commandSegments.Length == 0 ? string.Empty : string.Join(' ', commandSegments);
            bestCandidate = new SelectedCapture(commandKey, document);
            bestScore = score;
        }

        return bestCandidate;
    }

    public static SelectedPayload SelectBestProcessCapture(
        TextParser parser,
        string rootCommandName,
        IReadOnlyList<string> commandSegments,
        IReadOnlyList<string> invokedArguments,
        CommandRuntime.ProcessResult processResult)
    {
        var storedCommand = commandSegments.Count == 0
            ? string.Empty
            : string.Join(' ', commandSegments);
        var helpInvocation = invokedArguments.Count == 0
            ? null
            : string.Join(' ', invokedArguments);
        var candidates = EnumeratePayloadCandidates(processResult);
        Document? bestDocument = null;
        var bestPayload = candidates.FirstOrDefault();
        var bestScore = -1;

        foreach (var payload in candidates)
        {
            var document = parser.Parse(payload);
            var compatibleDocument = document.HasContent && DocumentInspector.IsCompatible(commandSegments, document)
                ? document
                : null;
            if (compatibleDocument is not null
                && ShouldRejectNonRootDispatcherEcho(rootCommandName, storedCommand, commandSegments, compatibleDocument))
            {
                compatibleDocument = null;
            }

            var score = compatibleDocument is not null
                ? DocumentInspector.Score(compatibleDocument) - GetPayloadSelectionPenalty(payload, helpInvocation)
                : 0;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestDocument = compatibleDocument;
            bestPayload = payload;
        }

        var isTerminalNonHelp = bestDocument is null && candidates.Any(DocumentInspector.LooksLikeTerminalNonHelpPayload);
        return new(bestDocument, bestPayload, isTerminalNonHelp);
    }

    private static int ScorePayloadCandidate(
        string storedCommand,
        Document document,
        string? helpInvocation,
        string payload)
    {
        var score = DocumentInspector.Score(document) - GetPayloadSelectionPenalty(payload, helpInvocation);
        if (string.IsNullOrWhiteSpace(storedCommand))
        {
            return score;
        }

        var storedCommandLeaf = CommandPathSupport.SplitSegments(storedCommand).LastOrDefault();
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

    private static bool ShouldRejectNonRootDispatcherEcho(
        string rootCommandName,
        string storedCommand,
        IReadOnlyList<string> commandSegments,
        Document document)
    {
        if (string.IsNullOrWhiteSpace(storedCommand)
            || commandSegments.Count == 0
            || HasLeafSurface(document)
            || document.Commands.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(document.Title)
            && document.Title.Trim().EndsWith(":", StringComparison.Ordinal))
        {
            return true;
        }

        var parentKey = string.Join(' ', commandSegments);
        foreach (var child in document.Commands)
        {
            if (SignatureNormalizer.IsBuiltinAuxiliaryCommand(child.Key))
            {
                continue;
            }

            var childKey = CommandPathSupport.ResolveChildKey(rootCommandName, parentKey, child.Key);
            if (childKey.Length > parentKey.Length
                && childKey.StartsWith(parentKey + " ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasLeafSurface(Document document)
        => document.Options.Count > 0
            || document.Arguments.Count > 0
            || !string.IsNullOrWhiteSpace(document.CommandDescription);

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
