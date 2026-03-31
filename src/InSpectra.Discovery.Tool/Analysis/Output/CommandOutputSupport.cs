namespace InSpectra.Discovery.Tool.Analysis.Output;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.Infrastructure.Host;

using InSpectra.Discovery.Tool.App.Machine;


internal static class CommandOutputSupport
{
    public static Task<int> WriteResultAsync(
        string packageId,
        string version,
        string resultPath,
        string? disposition,
        bool json,
        CancellationToken cancellationToken,
        string? analysisMode = null,
        string? selectionReason = null,
        string? fallbackFrom = null)
    {
        var resultSummary = LoadResultSummary(resultPath, analysisMode);
        var output = Runtime.CreateOutput();
        return output.WriteSuccessAsync(
            new AnalysisCommandResult(
                packageId,
                version,
                resultSummary.AnalysisMode,
                resultSummary.Command,
                resultSummary.CliFramework,
                selectionReason,
                fallbackFrom,
                disposition,
                resultPath),
            BuildSummaryRows(packageId, version, resultPath, disposition, resultSummary, selectionReason, fallbackFrom),
            json,
            cancellationToken);
    }

    internal static IReadOnlyList<SummaryRow> BuildSummaryRows(
        string packageId,
        string version,
        string resultPath,
        string? disposition,
        AnalysisCommandResultSummary resultSummary,
        string? selectionReason,
        string? fallbackFrom)
    {
        var rows = new List<SummaryRow>
        {
            new("Package", $"{packageId} {version}"),
        };

        if (!string.IsNullOrWhiteSpace(resultSummary.AnalysisMode))
        {
            rows.Add(new SummaryRow("Mode", resultSummary.AnalysisMode));
        }

        if (!string.IsNullOrWhiteSpace(resultSummary.Command))
        {
            rows.Add(new SummaryRow("Command", resultSummary.Command));
        }

        if (!string.IsNullOrWhiteSpace(resultSummary.CliFramework))
        {
            rows.Add(new SummaryRow("Framework", resultSummary.CliFramework));
        }

        if (!string.IsNullOrWhiteSpace(fallbackFrom))
        {
            rows.Add(new SummaryRow("Fallback from", fallbackFrom));
        }

        if (!string.IsNullOrWhiteSpace(selectionReason))
        {
            rows.Add(new SummaryRow("Selection reason", selectionReason));
        }

        rows.Add(new SummaryRow("Disposition", disposition ?? string.Empty));
        rows.Add(new SummaryRow("Result artifact", resultPath));
        return rows;
    }

    private static AnalysisCommandResultSummary LoadResultSummary(string resultPath, string? analysisMode)
    {
        var result = JsonNodeFileLoader.TryLoadJsonObject(resultPath);
        return new AnalysisCommandResultSummary(
            !string.IsNullOrWhiteSpace(analysisMode)
                ? analysisMode
                : result?["analysisMode"]?.GetValue<string>(),
            result?["command"]?.GetValue<string>(),
            result?["cliFramework"]?.GetValue<string>());
    }

    private sealed record AnalysisCommandResult(
        string PackageId,
        string Version,
        string? AnalysisMode,
        string? Command,
        string? CliFramework,
        string? SelectionReason,
        string? FallbackFrom,
        string? Disposition,
        string ResultPath);

    internal sealed record AnalysisCommandResultSummary(
        string? AnalysisMode,
        string? Command,
        string? CliFramework);
}


