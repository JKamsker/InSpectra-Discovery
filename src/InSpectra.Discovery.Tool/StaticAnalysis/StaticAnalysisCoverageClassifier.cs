using System.Text.Json.Nodes;

internal sealed class StaticAnalysisCoverageClassifier
{
    public StaticAnalysisCoverageSummary Classify(
        int staticCommandCount,
        int helpDocumentCount,
        IReadOnlyDictionary<string, JsonObject> captures)
    {
        var captureEntries = captures.Values.ToArray();
        var timedOutCount = captureEntries.Count(c => c["result"]?["timedOut"]?.GetValue<bool>() == true);
        var parsedCount = captureEntries.Count(c => c["parsed"]?.GetValue<bool>() == true);
        var unparsedCount = captureEntries.Count(c => c["parsed"]?.GetValue<bool>() != true);

        var coverageMode = GetCoverageMode(staticCommandCount, helpDocumentCount, parsedCount);
        var commandGraphMode = GetCommandGraphMode(staticCommandCount, helpDocumentCount, parsedCount);

        return new StaticAnalysisCoverageSummary(
            CoverageMode: coverageMode,
            CommandGraphMode: commandGraphMode,
            StaticCommandCount: staticCommandCount,
            HelpDocumentCount: helpDocumentCount,
            CapturedCommandCount: captureEntries.Length,
            ParsedCommandCount: parsedCount,
            UnparsedCommandCount: unparsedCount,
            TimedOutCommandCount: timedOutCount);
    }

    private static string GetCoverageMode(int staticCommandCount, int helpDocumentCount, int parsedCommandCount)
    {
        if (staticCommandCount > 0 && parsedCommandCount > 0)
        {
            return "full-static-and-help";
        }

        if (staticCommandCount > 0)
        {
            return "static-only";
        }

        if (parsedCommandCount > 0)
        {
            return "help-only";
        }

        return "no-data";
    }

    private static string GetCommandGraphMode(int staticCommandCount, int helpDocumentCount, int parsedCommandCount)
    {
        if (parsedCommandCount == 0)
        {
            return staticCommandCount > 0 ? "metadata-only" : "no-data";
        }

        return staticCommandCount > 0 ? "help-and-metadata" : "help-only";
    }
}

internal sealed record StaticAnalysisCoverageSummary(
    string CoverageMode,
    string CommandGraphMode,
    int StaticCommandCount,
    int HelpDocumentCount,
    int CapturedCommandCount,
    int ParsedCommandCount,
    int UnparsedCommandCount,
    int TimedOutCommandCount)
{
    public JsonObject ToJsonObject()
        => new()
        {
            ["coverageMode"] = CoverageMode,
            ["commandGraphMode"] = CommandGraphMode,
            ["staticCommandCount"] = StaticCommandCount,
            ["helpDocumentCount"] = HelpDocumentCount,
            ["capturedCommandCount"] = CapturedCommandCount,
            ["parsedCommandCount"] = ParsedCommandCount,
            ["unparsedCommandCount"] = UnparsedCommandCount,
            ["timedOutCommandCount"] = TimedOutCommandCount,
        };
}
