namespace InSpectra.Discovery.Tool.Help.Crawling;

using InSpectra.Discovery.Tool.Help.OpenCli;

using InSpectra.Discovery.Tool.Help.Signatures;
using InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.Documents;

using InSpectra.Discovery.Tool.Help.Parsing;

using InSpectra.Discovery.Tool.Infrastructure.Commands;


using System.Text.Json.Nodes;

internal sealed class Crawler
{
    private readonly TextParser _parser = new();
    private readonly CommandRuntime _runtime;

    public Crawler(CommandRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task<CrawlResult> CrawlAsync(
        string commandPath,
        string rootCommandName,
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

        var documents = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
        var captures = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var captureSummaries = new Dictionary<string, CaptureSummary>(StringComparer.OrdinalIgnoreCase);
        string? guardrailFailureMessage = null;

        while (queue.Count > 0)
        {
            if (captures.Count >= HelpCrawlGuardrailSupport.MaxCapturedCommands)
            {
                guardrailFailureMessage = HelpCrawlGuardrailSupport.BuildCaptureBudgetExceededMessage();
                break;
            }

            var commandSegments = queue.Dequeue();
            var key = InvocationSupport.GetCommandKey(commandSegments);
            if (documents.ContainsKey(key))
            {
                continue;
            }

            var capture = await CaptureHelpAsync(commandPath, rootCommandName, commandSegments, workingDirectory, environment, timeoutSeconds, cancellationToken);
            captures[key] = capture.ToJsonObject(commandSegments);
            captureSummaries[key] = capture.ToSummary(commandSegments);

            if (capture.Document is null)
            {
                continue;
            }

            documents[key] = capture.Document;
            if (commandSegments.Length >= HelpCrawlGuardrailSupport.MaxCommandDepth
                || DocumentInspector.IsBuiltinAuxiliaryInventoryEcho(key, capture.Document))
            {
                continue;
            }

            var childKeys = new List<string>();
            foreach (var child in capture.Document.Commands)
            {
                var resolvedChildKey = CommandPathSupport.ResolveChildKey(rootCommandName, key, child.Key);
                childKeys.Add(resolvedChildKey);
            }

            if (capture.Document.Commands.Count == 0)
            {
                foreach (var inferredChildKey in UsageCommandInferenceSupport.InferChildCommands(
                    rootCommandName,
                    commandSegments,
                    capture.Document.UsageLines))
                {
                    childKeys.Add(inferredChildKey);
                }
            }

            if (childKeys.Count > HelpCrawlGuardrailSupport.MaxChildCommandsPerDocument)
            {
                guardrailFailureMessage = HelpCrawlGuardrailSupport.BuildCommandFanoutExceededMessage(key, childKeys.Count);
                break;
            }

            foreach (var childKey in childKeys)
            {
                EnqueueChild(childKey, queue, seen);
            }
        }

        return new CrawlResult(documents, captures, captureSummaries, guardrailFailureMessage);
    }

    private async Task<Capture> CaptureHelpAsync(
        string commandPath,
        string rootCommandName,
        IReadOnlyList<string> commandSegments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        Capture? bestCapture = null;
        var timedOutInvocationCount = 0;
        var commandKey = InvocationSupport.GetCommandKey(commandSegments);
        foreach (var candidate in InvocationSupport.BuildHelpInvocations(commandSegments))
        {
            var processResult = await DotnetRuntimeCompatibilitySupport.InvokeWithCompatibilityRetriesAsync(
                _runtime,
                commandPath,
                candidate,
                workingDirectory,
                environment,
                timeoutSeconds,
                workingDirectory,
                cancellationToken);

            var capture = BuildCapture(rootCommandName, commandSegments, candidate, processResult);
            if (bestCapture is null || Score(capture) > Score(bestCapture))
            {
                bestCapture = capture;
            }

            if (processResult.TimedOut)
            {
                timedOutInvocationCount++;
                if (timedOutInvocationCount >= HelpCrawlGuardrailSupport.MaxTimedOutHelpInvocationsPerCommand)
                {
                    return ApplyGuardrailFailure(
                        bestCapture,
                        HelpCrawlGuardrailSupport.BuildTimedOutHelpInvocationBudgetExceededMessage(commandKey));
                }
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

        return bestCapture ?? new Capture(null, null, null, null, false, null);
    }

    private static Capture ApplyGuardrailFailure(Capture? capture, string failureMessage)
    {
        if (capture is null)
        {
            return new Capture(null, null, null, null, false, failureMessage);
        }

        return capture with
        {
            GuardrailFailureMessage = failureMessage,
        };
    }

    private static void EnqueueChild(string childKey, Queue<string[]> queue, ISet<string> seen)
    {
        var childSegments = CommandPathSupport.SplitSegments(childKey);
        var normalizedChildKey = InvocationSupport.GetCommandKey(childSegments);
        if (DocumentInspector.IsBuiltinAuxiliaryCommandPath(normalizedChildKey))
        {
            return;
        }

        if (childSegments.Length <= HelpCrawlGuardrailSupport.MaxCommandDepth && seen.Add(normalizedChildKey))
        {
            queue.Enqueue(childSegments);
        }
    }

    private Capture BuildCapture(
        string rootCommandName,
        IReadOnlyList<string> commandSegments,
        IReadOnlyList<string> invokedArguments,
        CommandRuntime.ProcessResult processResult)
    {
        var helpInvocation = invokedArguments.Count == 0
            ? null
            : string.Join(' ', invokedArguments);
        var selection = CapturePayloadSupport.SelectBestProcessCapture(
            _parser,
            rootCommandName,
            commandSegments,
            invokedArguments,
            processResult);
        if (processResult.OutputLimitExceeded)
        {
            return new Capture(helpInvocation, processResult, null, null, false, null);
        }

        return new Capture(
            helpInvocation,
            processResult,
            selection.Document,
            selection.Payload,
            selection.IsTerminalNonHelp,
            selection.GuardrailFailureMessage);
    }

    internal static IReadOnlyList<string[]> BuildHelpInvocations(IReadOnlyList<string> commandSegments)
        => InvocationSupport.BuildHelpInvocations(commandSegments);

    private static int Score(Capture capture)
    {
        if (capture.Document is not null)
        {
            return 100 + DocumentInspector.Score(capture.Document);
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

    private sealed record Capture(
        string? HelpInvocation,
        CommandRuntime.ProcessResult? ProcessResult,
        Document? Document,
        string? ParsedPayload,
        bool IsTerminalNonHelp,
        string? GuardrailFailureMessage)
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

        public CaptureSummary ToSummary(IReadOnlyList<string> commandSegments)
        {
            var commandName = commandSegments.Count == 0 ? string.Empty : string.Join(' ', commandSegments);
            return new CaptureSummary(
                Command: commandName,
                HelpInvocation: HelpInvocation,
                Parsed: Document?.HasContent ?? false,
                TerminalNonHelp: IsTerminalNonHelp,
                TimedOut: ProcessResult?.TimedOut ?? false,
                ExitCode: ProcessResult?.ExitCode,
                Stdout: CommandRuntime.NormalizeConsoleText(ProcessResult?.Stdout),
                Stderr: CommandRuntime.NormalizeConsoleText(ProcessResult?.Stderr),
                OutputLimitExceeded: ProcessResult?.OutputLimitExceeded ?? false,
                GuardrailFailureMessage: GuardrailFailureMessage);
        }
    }
}
