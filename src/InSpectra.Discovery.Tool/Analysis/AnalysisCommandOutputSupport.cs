internal static class AnalysisCommandOutputSupport
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
        var output = ToolRuntime.CreateOutput();
        return output.WriteSuccessAsync(
            new AnalysisCommandResult(
                packageId,
                version,
                analysisMode,
                selectionReason,
                fallbackFrom,
                disposition,
                resultPath),
            BuildSummaryRows(packageId, version, resultPath, disposition, analysisMode, selectionReason, fallbackFrom),
            json,
            cancellationToken);
    }

    private static IReadOnlyList<SummaryRow> BuildSummaryRows(
        string packageId,
        string version,
        string resultPath,
        string? disposition,
        string? analysisMode,
        string? selectionReason,
        string? fallbackFrom)
    {
        var rows = new List<SummaryRow>
        {
            new("Package", $"{packageId} {version}"),
        };

        if (!string.IsNullOrWhiteSpace(analysisMode))
        {
            rows.Add(new SummaryRow("Mode", analysisMode));
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

    private sealed record AnalysisCommandResult(
        string PackageId,
        string Version,
        string? AnalysisMode,
        string? SelectionReason,
        string? FallbackFrom,
        string? Disposition,
        string ResultPath);
}
