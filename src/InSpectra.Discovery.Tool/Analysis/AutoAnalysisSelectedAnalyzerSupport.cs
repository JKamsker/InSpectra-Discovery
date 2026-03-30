using System.Text.Json.Nodes;

internal static class AutoAnalysisSelectedAnalyzerSupport
{
    public static async Task<JsonObject> RunAsync(
        Func<CancellationToken, Task> runAnalyzerAsync,
        string packageId,
        string version,
        ToolAnalysisDescriptor descriptor,
        string batchId,
        int attempt,
        string source,
        string resultPath,
        JsonObject? nativeResult,
        string selectedMode,
        CancellationToken cancellationToken)
    {
        await runAnalyzerAsync(cancellationToken);

        var selectedResult = AutoAnalysisResultSupport.LoadResult(resultPath)
            ?? AutoAnalysisResultSupport.CreateFailureResult(
                packageId,
                version,
                batchId,
                attempt,
                source,
                "The selected analyzer did not write result.json.");
        AutoAnalysisResultSupport.ApplyDescriptor(selectedResult, descriptor, selectedMode, nativeResult);
        return selectedResult;
    }
}
