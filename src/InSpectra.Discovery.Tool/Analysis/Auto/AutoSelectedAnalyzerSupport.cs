namespace InSpectra.Discovery.Tool.Analysis.Auto;

using System.Text.Json.Nodes;

internal static class AutoSelectedAnalyzerSupport
{
    public static async Task<JsonObject> RunAsync(
        Func<CancellationToken, Task> runAnalyzerAsync,
        string packageId,
        string version,
        ToolDescriptor descriptor,
        string batchId,
        int attempt,
        string source,
        string resultPath,
        JsonObject? nativeResult,
        string selectedMode,
        CancellationToken cancellationToken)
    {
        await runAnalyzerAsync(cancellationToken);

        var selectedResult = AutoResultSupport.LoadResult(resultPath)
            ?? AutoResultSupport.CreateFailureResult(
                packageId,
                version,
                batchId,
                attempt,
                source,
                "The selected analyzer did not write result.json.");
        AutoResultSupport.ApplyDescriptor(selectedResult, descriptor, selectedMode, nativeResult);
        return selectedResult;
    }
}


