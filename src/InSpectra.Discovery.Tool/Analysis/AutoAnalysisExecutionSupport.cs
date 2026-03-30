using System.Text.Json.Nodes;

internal static class AutoAnalysisExecutionSupport
{
    public static async Task<NativeAnalysisOutcome> TryRunNativeAnalysisAsync(
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
            return NativeAnalysisOutcome.Return(await AutoAnalysisResultSupport.WriteResultAsync(packageId, version, resultPath, nativeResult, json, suppressOutput, cancellationToken));
        }

        return NativeAnalysisOutcome.Continue(nativeResult);
    }

    public static Task<JsonObject> RunSelectedAnalyzerAsync(
        string selectedMode,
        IAutoAnalysisHelpRunner helpRunner,
        IAutoAnalysisCliFxRunner cliFxRunner,
        IAutoAnalysisStaticRunner staticRunner,
        string packageId,
        string version,
        ToolAnalysisDescriptor descriptor,
        string outputDirectory,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        string resultPath,
        JsonObject? nativeResult,
        CancellationToken cancellationToken)
        => selectedMode switch
        {
            "clifx" => RunCliFxAsync(
                cliFxRunner,
                packageId,
                version,
                descriptor,
                outputDirectory,
                batchId,
                attempt,
                source,
                installTimeoutSeconds,
                analysisTimeoutSeconds,
                commandTimeoutSeconds,
                resultPath,
                nativeResult,
                cancellationToken),
            "static" => RunStaticAsync(
                staticRunner,
                packageId,
                version,
                descriptor,
                outputDirectory,
                batchId,
                attempt,
                source,
                installTimeoutSeconds,
                analysisTimeoutSeconds,
                commandTimeoutSeconds,
                resultPath,
                nativeResult,
                cancellationToken),
            _ => RunHelpAsync(
                helpRunner,
                packageId,
                version,
                descriptor,
                outputDirectory,
                batchId,
                attempt,
                source,
                installTimeoutSeconds,
                analysisTimeoutSeconds,
                commandTimeoutSeconds,
                resultPath,
                nativeResult,
                cancellationToken),
        };

    private static async Task<JsonObject> RunCliFxAsync(
        IAutoAnalysisCliFxRunner cliFxRunner,
        string packageId,
        string version,
        ToolAnalysisDescriptor descriptor,
        string outputDirectory,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        string resultPath,
        JsonObject? nativeResult,
        CancellationToken cancellationToken)
    {
        await cliFxRunner.RunAsync(
            packageId,
            version,
            descriptor.CommandName,
            descriptor.CliFramework,
            outputDirectory,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            cancellationToken);

        var cliFxResult = AutoAnalysisResultSupport.LoadResult(resultPath)
            ?? AutoAnalysisResultSupport.CreateFailureResult(packageId, version, batchId, attempt, source, "The selected analyzer did not write result.json.");
        AutoAnalysisResultSupport.ApplyDescriptor(cliFxResult, descriptor, "clifx", nativeResult);
        return cliFxResult;
    }

    private static async Task<JsonObject> RunStaticAsync(
        IAutoAnalysisStaticRunner staticRunner,
        string packageId,
        string version,
        ToolAnalysisDescriptor descriptor,
        string outputDirectory,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        string resultPath,
        JsonObject? nativeResult,
        CancellationToken cancellationToken)
    {
        await staticRunner.RunAsync(
            packageId,
            version,
            descriptor.CommandName,
            descriptor.CliFramework,
            outputDirectory,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            cancellationToken);

        var staticResult = AutoAnalysisResultSupport.LoadResult(resultPath)
            ?? AutoAnalysisResultSupport.CreateFailureResult(packageId, version, batchId, attempt, source, "The selected analyzer did not write result.json.");
        AutoAnalysisResultSupport.ApplyDescriptor(staticResult, descriptor, "static", nativeResult);
        return staticResult;
    }

    private static async Task<JsonObject> RunHelpAsync(
        IAutoAnalysisHelpRunner helpRunner,
        string packageId,
        string version,
        ToolAnalysisDescriptor descriptor,
        string outputDirectory,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        string resultPath,
        JsonObject? nativeResult,
        CancellationToken cancellationToken)
    {
        await helpRunner.RunAsync(
            packageId,
            version,
            descriptor.CommandName,
            outputDirectory,
            batchId,
            attempt,
            source,
            descriptor.CliFramework,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            cancellationToken);

        var helpResult = AutoAnalysisResultSupport.LoadResult(resultPath)
            ?? AutoAnalysisResultSupport.CreateFailureResult(packageId, version, batchId, attempt, source, "The selected analyzer did not write result.json.");
        AutoAnalysisResultSupport.ApplyDescriptor(helpResult, descriptor, "help", nativeResult);
        return helpResult;
    }
}

internal sealed record NativeAnalysisOutcome(bool ShouldReturnImmediately, int ExitCode, JsonObject? Result)
{
    public static NativeAnalysisOutcome Continue(JsonObject? result)
        => new(false, 0, result);

    public static NativeAnalysisOutcome Return(int exitCode)
        => new(true, exitCode, null);
}
