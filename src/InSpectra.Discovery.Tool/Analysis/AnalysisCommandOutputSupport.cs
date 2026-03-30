internal static class AnalysisCommandOutputSupport
{
    public static Task<int> WriteResultAsync(
        string packageId,
        string version,
        string resultPath,
        string? disposition,
        bool json,
        CancellationToken cancellationToken,
        string? analysisMode = null)
    {
        var output = ToolRuntime.CreateOutput();
        return output.WriteSuccessAsync(
            new AnalysisCommandResult(
                packageId,
                version,
                analysisMode,
                disposition,
                resultPath),
            BuildSummaryRows(packageId, version, resultPath, disposition, analysisMode),
            json,
            cancellationToken);
    }

    private static IReadOnlyList<SummaryRow> BuildSummaryRows(
        string packageId,
        string version,
        string resultPath,
        string? disposition,
        string? analysisMode)
    {
        var rows = new List<SummaryRow>
        {
            new("Package", $"{packageId} {version}"),
        };

        if (!string.IsNullOrWhiteSpace(analysisMode))
        {
            rows.Add(new SummaryRow("Mode", analysisMode));
        }

        rows.Add(new SummaryRow("Disposition", disposition ?? string.Empty));
        rows.Add(new SummaryRow("Result artifact", resultPath));
        return rows;
    }

    private sealed record AnalysisCommandResult(
        string PackageId,
        string Version,
        string? AnalysisMode,
        string? Disposition,
        string ResultPath);
}
