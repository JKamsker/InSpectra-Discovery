using System.Text.Json.Nodes;

internal sealed class ToolHelpCrawler
{
    private const int MaxCommandDepth = 8;
    private readonly ToolHelpTextParser _parser = new();
    private readonly ToolCommandRuntime _runtime;

    public ToolHelpCrawler(ToolCommandRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task<ToolHelpCrawlResult> CrawlAsync(
        string commandPath,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var queue = new Queue<string[]>();
        queue.Enqueue([]);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            string.Empty,
        };

        var documents = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase);
        var captures = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var captureSummaries = new Dictionary<string, ToolHelpCaptureSummary>(StringComparer.OrdinalIgnoreCase);

        while (queue.Count > 0)
        {
            var commandSegments = queue.Dequeue();
            var key = GetKey(commandSegments);
            if (documents.ContainsKey(key))
            {
                continue;
            }

            var capture = await CaptureHelpAsync(commandPath, commandSegments, workingDirectory, environment, timeoutSeconds, cancellationToken);
            captures[key] = capture.ToJsonObject(commandSegments);
            captureSummaries[key] = capture.ToSummary(commandSegments);

            if (capture.Document is null)
            {
                continue;
            }

            documents[key] = capture.Document;
            if (commandSegments.Length >= MaxCommandDepth
                || ToolHelpDocumentInspector.IsBuiltinAuxiliaryInventoryEcho(key, capture.Document))
            {
                continue;
            }

            foreach (var child in capture.Document.Commands)
            {
                var resolvedChildKey = ToolHelpCommandPathSupport.ResolveChildKey(commandPath, key, child.Key);
                var childSegments = ToolHelpCommandPathSupport.SplitSegments(resolvedChildKey);
                var childKey = GetKey(childSegments);
                if (ToolHelpDocumentInspector.IsBuiltinAuxiliaryCommandPath(childKey))
                {
                    continue;
                }

                if (childSegments.Length <= MaxCommandDepth && seen.Add(childKey))
                {
                    queue.Enqueue(childSegments);
                }
            }
        }

        return new ToolHelpCrawlResult(documents, captures, captureSummaries);
    }

    private async Task<ToolHelpCapture> CaptureHelpAsync(
        string commandPath,
        IReadOnlyList<string> commandSegments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        ToolHelpCapture? bestCapture = null;
        foreach (var candidate in BuildHelpInvocations(commandSegments))
        {
            var processResult = await _runtime.InvokeProcessCaptureAsync(
                commandPath,
                candidate,
                workingDirectory,
                environment,
                timeoutSeconds,
                workingDirectory,
                cancellationToken);

            var capture = BuildCapture(commandSegments, candidate, processResult);
            if (bestCapture is null || Score(capture) > Score(bestCapture))
            {
                bestCapture = capture;
            }

            if (capture.Document?.HasContent == true)
            {
                return capture;
            }

            if (capture.IsTerminalNonHelp)
            {
                return capture;
            }
        }

        return bestCapture ?? new ToolHelpCapture(null, null, null, null, false);
    }

    private ToolHelpCapture BuildCapture(
        IReadOnlyList<string> commandSegments,
        IReadOnlyList<string> invokedArguments,
        ToolCommandRuntime.ProcessResult processResult)
    {
        var candidates = SelectPayloadCandidates(processResult);
        ToolHelpDocument? bestDocument = null;
        var bestPayload = candidates.FirstOrDefault();
        var bestScore = -1;
        var helpInvocation = invokedArguments.Count == 0
            ? null
            : string.Join(' ', invokedArguments);

        foreach (var payload in candidates)
        {
            var document = _parser.Parse(payload);
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
        return new ToolHelpCapture(helpInvocation, processResult, bestDocument, bestPayload, isTerminalNonHelp);
    }

    internal static IReadOnlyList<string[]> BuildHelpInvocations(IReadOnlyList<string> commandSegments)
    {
        var invocations = new List<string[]>
        {
            commandSegments.Concat(new[] { "--help" }).ToArray(),
            commandSegments.Concat(new[] { "-h" }).ToArray(),
            commandSegments.Concat(new[] { "-?" }).ToArray(),
            commandSegments.Concat(new[] { "/help" }).ToArray(),
            commandSegments.Concat(new[] { "/?" }).ToArray(),
        };

        invocations.AddRange(BuildKeywordHelpInvocations(commandSegments));
        invocations.Add(commandSegments.ToArray());

        return invocations
            .Distinct(new ToolHelpInvocationComparer())
            .ToArray();
    }

    private static IEnumerable<string[]> BuildKeywordHelpInvocations(IReadOnlyList<string> commandSegments)
    {
        if (commandSegments.Count == 0)
        {
            yield return ["help"];
            yield break;
        }

        yield return (new[] { "help" }).Concat(commandSegments).ToArray();

        for (var index = 1; index < commandSegments.Count; index++)
        {
            yield return commandSegments.Take(index)
                .Concat(new[] { "help" })
                .Concat(commandSegments.Skip(index))
                .ToArray();
        }

        yield return commandSegments.Concat(new[] { "help" }).ToArray();
    }

    private static IReadOnlyList<string> SelectPayloadCandidates(ToolCommandRuntime.ProcessResult processResult)
    {
        var stdout = ToolCommandRuntime.NormalizeConsoleText(processResult.Stdout);
        var stderr = ToolCommandRuntime.NormalizeConsoleText(processResult.Stderr);
        var payloads = new List<string>();

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

    private static int Score(ToolHelpCapture capture)
    {
        if (capture.Document is not null)
        {
            return 100 + ToolHelpDocumentInspector.Score(capture.Document);
        }

        if (capture.IsTerminalNonHelp)
        {
            return 4;
        }

        if (capture.ProcessResult?.TimedOut == true)
        {
            return 3;
        }

        return capture.ProcessResult?.ExitCode is 0 ? 1 : 2;
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

    private static string GetKey(IReadOnlyList<string> commandSegments)
        => commandSegments.Count == 0 ? string.Empty : string.Join(' ', commandSegments);

    internal sealed record ToolHelpCrawlResult(
        IReadOnlyDictionary<string, ToolHelpDocument> Documents,
        IReadOnlyDictionary<string, JsonObject> Captures,
        IReadOnlyDictionary<string, ToolHelpCaptureSummary> CaptureSummaries);

    private sealed record ToolHelpCapture(
        string? HelpInvocation,
        ToolCommandRuntime.ProcessResult? ProcessResult,
        ToolHelpDocument? Document,
        string? ParsedPayload,
        bool IsTerminalNonHelp)
    {
        public JsonObject ToJsonObject(IReadOnlyList<string> commandSegments)
        {
            var commandName = commandSegments.Count == 0 ? null : string.Join(' ', commandSegments);
            return new JsonObject
            {
                ["command"] = commandName,
                ["helpInvocation"] = HelpInvocation,
                ["result"] = ProcessResult?.ToJsonObject(),
                ["parsed"] = Document?.HasContent ?? false,
                ["payload"] = ParsedPayload,
                ["terminalNonHelp"] = IsTerminalNonHelp,
            };
        }

        public ToolHelpCaptureSummary ToSummary(IReadOnlyList<string> commandSegments)
        {
            var commandName = commandSegments.Count == 0 ? string.Empty : string.Join(' ', commandSegments);
            return new ToolHelpCaptureSummary(
                Command: commandName,
                HelpInvocation: HelpInvocation,
                Parsed: Document?.HasContent ?? false,
                TerminalNonHelp: IsTerminalNonHelp,
                TimedOut: ProcessResult?.TimedOut ?? false,
                ExitCode: ProcessResult?.ExitCode,
                Stdout: ToolCommandRuntime.NormalizeConsoleText(ProcessResult?.Stdout),
                Stderr: ToolCommandRuntime.NormalizeConsoleText(ProcessResult?.Stderr));
        }
    }

}

internal sealed record ToolHelpCaptureSummary(
    string Command,
    string? HelpInvocation,
    bool Parsed,
    bool TerminalNonHelp,
    bool TimedOut,
    int? ExitCode,
    string? Stdout,
    string? Stderr);
