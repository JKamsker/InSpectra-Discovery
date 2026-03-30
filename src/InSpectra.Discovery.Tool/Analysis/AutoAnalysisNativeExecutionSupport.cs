using System.Text.Json.Nodes;

internal static class AutoAnalysisNativeExecutionSupport
{
    public static async Task<NativeAnalysisOutcome> TryRunAsync(
        IAutoAnalysisNativeRunner nativeRunner,
        string packageId,
        string version,
        ToolAnalysisDescriptor descriptor,
        string outputDirectory,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        string resultPath,
        bool json,
        bool suppressOutput,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(descriptor.PreferredAnalysisMode, "native", StringComparison.OrdinalIgnoreCase))
        {
            return NativeAnalysisOutcome.Continue(null);
        }

        await nativeRunner.RunAsync(
            packageId,
            version,
            outputDirectory,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            commandTimeoutSeconds,
            cancellationToken);

        var nativeResult = AutoAnalysisResultSupport.LoadResult(resultPath);
        if (nativeResult is null)
        {
            return NativeAnalysisOutcome.Continue(null);
        }

        AutoAnalysisResultSupport.ApplyDescriptor(nativeResult, descriptor, "native", null);
        RepositoryPathResolver.WriteJsonFile(resultPath, nativeResult);
        if (!AutoAnalysisResultInspector.ShouldTryHelpFallback(nativeResult))
        {
            return NativeAnalysisOutcome.Return(
                await AutoAnalysisResultSupport.WriteResultAsync(
                    packageId,
                    version,
                    resultPath,
                    nativeResult,
                    json,
                    suppressOutput,
                    cancellationToken));
        }

        return NativeAnalysisOutcome.Continue(nativeResult);
    }
}
