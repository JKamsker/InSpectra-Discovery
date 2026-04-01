namespace InSpectra.Discovery.Tool.Analysis.Auto.Execution;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.Analysis.Auto.Results;

using InSpectra.Discovery.Tool.Analysis.Tools;

using InSpectra.Discovery.Tool.Analysis.Auto.Runners;

using System.Text.Json.Nodes;

internal static class AutoNativeExecutionSupport
{
    public static async Task<NativeAnalysisOutcome> TryRunAsync(
        IAutoNativeRunner nativeRunner,
        string packageId,
        string version,
        ToolDescriptor descriptor,
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

        var nativeResult = AutoResultSupport.LoadResult(resultPath);
        if (nativeResult is null)
        {
            return NativeAnalysisOutcome.Continue(null);
        }

        AutoResultSupport.ApplyDescriptor(nativeResult, descriptor, "native", null, descriptor.CliFramework);
        RepositoryPathResolver.WriteJsonFile(resultPath, nativeResult);
        if (!AutoResultInspector.ShouldTryHelpFallback(nativeResult))
        {
            return NativeAnalysisOutcome.Return(
                await AutoResultSupport.WriteResultAsync(
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

