namespace InSpectra.Discovery.Tool.Analysis.CliFx.Execution;

using System.Text.Json.Nodes;

internal sealed class CliFxCoverageClassifier
{
    private readonly CliFxRuntimeCompatibilityDetector _runtimeCompatibilityDetector = new();

    public CliFxCoverageSummary Classify(int metadataCommandCount, CliFxHelpCrawler.CliFxCrawlResult crawl)
    {
        var captures = crawl.CaptureSummaries.Values.ToArray();
        var runtimeIssues = captures
            .Select(capture => _runtimeCompatibilityDetector.Detect(capture))
            .Where(issue => issue is not null)
            .Cast<CliFxRuntimeIssue>()
            .ToArray();
        var parsedCommands = captures.Where(capture => capture.Parsed).Select(capture => ToDisplayCommand(capture.Command)).ToArray();
        var unparsedCommands = captures.Where(capture => !capture.Parsed).Select(capture => ToDisplayCommand(capture.Command)).ToArray();
        var timedOutCommands = captures.Where(capture => capture.TimedOut).Select(capture => ToDisplayCommand(capture.Command)).ToArray();
        var runtimeBlockedCommands = runtimeIssues.Select(issue => issue.Command).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var requiredFrameworks = runtimeIssues
            .Where(issue => issue.Requirement is not null)
            .Select(issue => issue.Requirement!)
            .Distinct()
            .ToArray();

        var helpCoverageMode = GetHelpCoverageMode(metadataCommandCount, parsedCommands.Length, unparsedCommands.Length, runtimeBlockedCommands.Length);
        var runtimeCompatibilityMode = runtimeBlockedCommands.Length > 0
            ? "missing-framework"
            : "compatible";
        var commandGraphMode = parsedCommands.Length == 0
            ? metadataCommandCount > 0 ? "metadata-only" : "help-only"
            : parsedCommands.Length == captures.Length
                ? metadataCommandCount > 0 ? "help-and-metadata" : "help-only"
            : metadataCommandCount > 0
                ? "metadata-augmented"
                : "help-only";

        return new CliFxCoverageSummary(
            HelpCoverageMode: helpCoverageMode,
            CommandGraphMode: commandGraphMode,
            RuntimeCompatibilityMode: runtimeCompatibilityMode,
            MetadataCommandCount: metadataCommandCount,
            CapturedCommandCount: captures.Length,
            HelpDocumentCount: crawl.Documents.Count,
            ParsedCommandCount: parsedCommands.Length,
            UnparsedCommandCount: unparsedCommands.Length,
            TimedOutCommandCount: timedOutCommands.Length,
            RuntimeBlockedCommandCount: runtimeBlockedCommands.Length,
            RequiredFrameworks: requiredFrameworks,
            ParsedCommands: parsedCommands,
            UnparsedCommands: unparsedCommands,
            TimedOutCommands: timedOutCommands,
            RuntimeBlockedCommands: runtimeBlockedCommands);
    }

    private static string GetHelpCoverageMode(int metadataCommandCount, int parsedCommandCount, int unparsedCommandCount, int runtimeBlockedCommandCount)
    {
        if (parsedCommandCount == 0)
        {
            if (metadataCommandCount == 0)
            {
                return "no-command-data";
            }

            return runtimeBlockedCommandCount > 0
                ? "metadata-only-runtime-blocked"
                : "metadata-only";
        }

        return unparsedCommandCount == 0
            ? "full-help"
            : "partial-help";
    }

    private static bool IsRuntimeBlocked(string? text)
        => false;

    private static string ToDisplayCommand(string command)
        => CliFxRuntimeCompatibilityDetector.ToDisplayCommand(command);
}

internal sealed record CliFxCoverageSummary(
    string HelpCoverageMode,
    string CommandGraphMode,
    string RuntimeCompatibilityMode,
    int MetadataCommandCount,
    int CapturedCommandCount,
    int HelpDocumentCount,
    int ParsedCommandCount,
    int UnparsedCommandCount,
    int TimedOutCommandCount,
    int RuntimeBlockedCommandCount,
    IReadOnlyList<CliFxRuntimeRequirement> RequiredFrameworks,
    IReadOnlyList<string> ParsedCommands,
    IReadOnlyList<string> UnparsedCommands,
    IReadOnlyList<string> TimedOutCommands,
    IReadOnlyList<string> RuntimeBlockedCommands)
{
    public JsonObject ToJsonObject()
        => new()
        {
            ["helpCoverageMode"] = HelpCoverageMode,
            ["commandGraphMode"] = CommandGraphMode,
            ["runtimeCompatibilityMode"] = RuntimeCompatibilityMode,
            ["metadataCommandCount"] = MetadataCommandCount,
            ["capturedCommandCount"] = CapturedCommandCount,
            ["helpDocumentCount"] = HelpDocumentCount,
            ["parsedCommandCount"] = ParsedCommandCount,
            ["unparsedCommandCount"] = UnparsedCommandCount,
            ["timedOutCommandCount"] = TimedOutCommandCount,
            ["runtimeBlockedCommandCount"] = RuntimeBlockedCommandCount,
            ["requiredFrameworks"] = ToJsonArray(RequiredFrameworks),
            ["parsedCommands"] = ToJsonArray(ParsedCommands),
            ["unparsedCommands"] = ToJsonArray(UnparsedCommands),
            ["timedOutCommands"] = ToJsonArray(TimedOutCommands),
            ["runtimeBlockedCommands"] = ToJsonArray(RuntimeBlockedCommands),
        };

    private static JsonArray ToJsonArray(IReadOnlyList<string> values)
        => new(values.Select(value => JsonValue.Create(value)).ToArray());

    private static JsonArray ToJsonArray(IReadOnlyList<CliFxRuntimeRequirement> values)
        => new(values.Select(value => new JsonObject
        {
            ["name"] = value.Name,
            ["version"] = value.Version,
        }).ToArray());
}


