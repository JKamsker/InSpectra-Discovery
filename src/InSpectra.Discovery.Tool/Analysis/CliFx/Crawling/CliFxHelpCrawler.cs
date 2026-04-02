namespace InSpectra.Discovery.Tool.Analysis.CliFx.Crawling;

using InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;

using InSpectra.Discovery.Tool.Analysis.CliFx.Execution;
using InSpectra.Discovery.Tool.Help.Crawling;
using InSpectra.Discovery.Tool.Infrastructure.Commands;


using System.Collections.Concurrent;
using System.Text.Json.Nodes;

internal sealed class CliFxHelpCrawler
{
    private readonly CliFxHelpTextParser _parser = new();
    private readonly CommandRuntime _runtime;

    public CliFxHelpCrawler(CommandRuntime runtime)
    {
        _runtime = runtime;
    }

    public async Task<CliFxCrawlResult> CrawlAsync(
        string commandPath,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var queue = new Queue<string[]>();
        queue.Enqueue([]);

        var documents = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase);
        var captures = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        var captureSummaries = new Dictionary<string, CliFxCaptureSummary>(StringComparer.OrdinalIgnoreCase);
        string? guardrailFailureMessage = null;

        while (queue.Count > 0)
        {
            if (captures.Count >= HelpCrawlGuardrailSupport.MaxCapturedCommands)
            {
                guardrailFailureMessage = HelpCrawlGuardrailSupport.BuildCaptureBudgetExceededMessage();
                break;
            }

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
            if (capture.Document.Commands.Count > HelpCrawlGuardrailSupport.MaxChildCommandsPerDocument)
            {
                guardrailFailureMessage = HelpCrawlGuardrailSupport.BuildCommandFanoutExceededMessage(key, capture.Document.Commands.Count);
                break;
            }

            foreach (var child in capture.Document.Commands)
            {
                var childSegments = commandSegments.Concat(
                    child.Key.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToArray();
                if (childSegments.Length > HelpCrawlGuardrailSupport.MaxCommandDepth)
                {
                    continue;
                }

                var childKey = GetKey(childSegments);
                if (!documents.ContainsKey(childKey))
                {
                    queue.Enqueue(childSegments);
                }
            }
        }

        return new CliFxCrawlResult(documents, captures, captureSummaries, guardrailFailureMessage);
    }

    private async Task<CliFxHelpCapture> CaptureHelpAsync(
        string commandPath,
        IReadOnlyList<string> commandSegments,
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        CliFxHelpCapture? fallbackCapture = null;
        foreach (var helpSwitch in new[] { "--help", "-h" })
        {
            var arguments = commandSegments.Concat([helpSwitch]).ToArray();
            var processResult = await DotnetRuntimeCompatibilitySupport.InvokeWithCompatibilityRetriesAsync(
                _runtime,
                commandPath,
                arguments,
                workingDirectory,
                environment,
                timeoutSeconds,
                workingDirectory,
                cancellationToken);
            if (processResult.OutputLimitExceeded)
            {
                fallbackCapture = SelectFallbackCapture(fallbackCapture, new CliFxHelpCapture(helpSwitch, processResult, null));
                continue;
            }

            var payload = SelectBestPayload(processResult);
            if (payload is null)
            {
                fallbackCapture = SelectFallbackCapture(fallbackCapture, new CliFxHelpCapture(helpSwitch, processResult, null));
                continue;
            }

            if (!HelpCrawlGuardrailSupport.TryValidatePayload(payload, out var payloadFailureMessage))
            {
                fallbackCapture = SelectFallbackCapture(
                    fallbackCapture,
                    new CliFxHelpCapture(helpSwitch, processResult, null, payloadFailureMessage));
                continue;
            }

            var document = _parser.Parse(payload);
            if (document.UsageLines.Count == 0 && document.Options.Count == 0 && document.Commands.Count == 0)
            {
                fallbackCapture = SelectFallbackCapture(fallbackCapture, new CliFxHelpCapture(helpSwitch, processResult, null));
                continue;
            }

            return new CliFxHelpCapture(helpSwitch, processResult, document);
        }

        return fallbackCapture ?? new CliFxHelpCapture(null, null, null, null);
    }

    private static string? SelectBestPayload(CliFxRuntime.ProcessResult processResult)
    {
        var stdout = CommandRuntime.NormalizeConsoleText(processResult.Stdout);
        var stderr = CommandRuntime.NormalizeConsoleText(processResult.Stderr);

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

    private static string GetKey(IReadOnlyList<string> commandSegments)
        => commandSegments.Count == 0 ? string.Empty : string.Join(' ', commandSegments);

    private static CliFxHelpCapture SelectFallbackCapture(CliFxHelpCapture? current, CliFxHelpCapture candidate)
    {
        if (current?.ProcessResult is null)
        {
            return candidate;
        }

        if (candidate.ProcessResult is null)
        {
            return current;
        }

        return Score(candidate.ProcessResult) >= Score(current.ProcessResult)
            ? candidate
            : current;
    }

    private static int Score(CliFxRuntime.ProcessResult result)
    {
        if (result.TimedOut)
        {
            return 3;
        }

        if (result.ExitCode is not 0)
        {
            return 2;
        }

        return string.IsNullOrWhiteSpace(CommandRuntime.NormalizeConsoleText(result.Stdout))
            && string.IsNullOrWhiteSpace(CommandRuntime.NormalizeConsoleText(result.Stderr))
            ? 0
            : 1;
    }

    internal sealed record CliFxCrawlResult(
        IReadOnlyDictionary<string, CliFxHelpDocument> Documents,
        IReadOnlyDictionary<string, JsonObject> Captures,
        IReadOnlyDictionary<string, CliFxCaptureSummary> CaptureSummaries,
        string? GuardrailFailureMessage = null);

    private sealed record CliFxHelpCapture(
        string? HelpSwitch,
        CliFxRuntime.ProcessResult? ProcessResult,
        CliFxHelpDocument? Document,
        string? GuardrailFailureMessage = null)
    {
        public JsonObject ToJsonObject(IReadOnlyList<string> commandSegments)
        {
            var commandName = commandSegments.Count == 0 ? null : string.Join(' ', commandSegments);
            var payload = ProcessResult is null ? null : SelectBestPayload(ProcessResult);
            return new JsonObject
            {
                ["command"] = commandName,
                ["helpSwitch"] = HelpSwitch,
                ["result"] = ProcessResult?.ToJsonObject(),
                ["payload"] = payload,
                ["parsed"] = Document is null
                    ? false
                    : Document.UsageLines.Count > 0 || Document.Options.Count > 0 || Document.Commands.Count > 0,
            };
        }

        public CliFxCaptureSummary ToSummary(IReadOnlyList<string> commandSegments)
        {
            var commandName = commandSegments.Count == 0 ? string.Empty : string.Join(' ', commandSegments);
            return new CliFxCaptureSummary(
                Command: commandName,
                HelpSwitch: HelpSwitch,
                Parsed: Document is not null && (Document.UsageLines.Count > 0 || Document.Options.Count > 0 || Document.Commands.Count > 0),
                TimedOut: ProcessResult?.TimedOut ?? false,
                ExitCode: ProcessResult?.ExitCode,
                Stdout: CommandRuntime.NormalizeConsoleText(ProcessResult?.Stdout),
                Stderr: CommandRuntime.NormalizeConsoleText(ProcessResult?.Stderr),
                OutputLimitExceeded: ProcessResult?.OutputLimitExceeded ?? false,
                GuardrailFailureMessage: GuardrailFailureMessage);
        }
    }
}

internal sealed record CliFxCaptureSummary(
    string Command,
    string? HelpSwitch,
    bool Parsed,
    bool TimedOut,
    int? ExitCode,
    string? Stdout,
    string? Stderr,
    bool OutputLimitExceeded = false,
    string? GuardrailFailureMessage = null);
