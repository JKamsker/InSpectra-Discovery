using System.Text.Json.Nodes;

internal sealed class ToolHelpCrawler
{
    private const int MaxCommandDepth = 8;
    private readonly ToolHelpTextParser _parser = new();
    private readonly CommandRuntime _runtime;

    public ToolHelpCrawler(CommandRuntime runtime)
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
            var key = ToolHelpInvocationSupport.GetCommandKey(commandSegments);
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
                var childKey = ToolHelpInvocationSupport.GetCommandKey(childSegments);
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
        foreach (var candidate in ToolHelpInvocationSupport.BuildHelpInvocations(commandSegments))
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
        CommandRuntime.ProcessResult processResult)
    {
        var helpInvocation = invokedArguments.Count == 0
            ? null
            : string.Join(' ', invokedArguments);
        var selection = ToolHelpCapturePayloadSupport.SelectBestProcessCapture(
            _parser,
            commandSegments,
            invokedArguments,
            processResult);
        return new ToolHelpCapture(helpInvocation, processResult, selection.Document, selection.Payload, selection.IsTerminalNonHelp);
    }

    internal static IReadOnlyList<string[]> BuildHelpInvocations(IReadOnlyList<string> commandSegments)
        => ToolHelpInvocationSupport.BuildHelpInvocations(commandSegments);

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

    internal sealed record ToolHelpCrawlResult(
        IReadOnlyDictionary<string, ToolHelpDocument> Documents,
        IReadOnlyDictionary<string, JsonObject> Captures,
        IReadOnlyDictionary<string, ToolHelpCaptureSummary> CaptureSummaries);

    private sealed record ToolHelpCapture(
        string? HelpInvocation,
        CommandRuntime.ProcessResult? ProcessResult,
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
                Stdout: CommandRuntime.NormalizeConsoleText(ProcessResult?.Stdout),
                Stderr: CommandRuntime.NormalizeConsoleText(ProcessResult?.Stderr));
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
