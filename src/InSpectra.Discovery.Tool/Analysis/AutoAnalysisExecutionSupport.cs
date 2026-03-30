using System.Text.Json.Nodes;

internal static class AutoAnalysisExecutionSupport
{
    public static Task<NativeAnalysisOutcome> TryRunNativeAnalysisAsync(
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
        => AutoAnalysisNativeExecutionSupport.TryRunAsync(
            nativeRunner,
            packageId,
            version,
            descriptor,
            outputDirectory,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            commandTimeoutSeconds,
            resultPath,
            json,
            suppressOutput,
            cancellationToken);

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

    private static Task<JsonObject> RunCliFxAsync(
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
        => AutoAnalysisSelectedAnalyzerSupport.RunAsync(
            async token =>
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
                    token);
            },
            packageId,
            version,
            descriptor,
            batchId,
            attempt,
            source,
            resultPath,
            nativeResult,
            selectedMode: "clifx",
            cancellationToken);

    private static Task<JsonObject> RunStaticAsync(
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
        => AutoAnalysisSelectedAnalyzerSupport.RunAsync(
            async token =>
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
                    token);
            },
            packageId,
            version,
            descriptor,
            batchId,
            attempt,
            source,
            resultPath,
            nativeResult,
            selectedMode: "static",
            cancellationToken);

    private static Task<JsonObject> RunHelpAsync(
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
        => AutoAnalysisSelectedAnalyzerSupport.RunAsync(
            async token =>
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
                    token);
            },
            packageId,
            version,
            descriptor,
            batchId,
            attempt,
            source,
            resultPath,
            nativeResult,
            selectedMode: "help",
            cancellationToken);
}

internal sealed record NativeAnalysisOutcome(bool ShouldReturnImmediately, int ExitCode, JsonObject? Result)
{
    public static NativeAnalysisOutcome Continue(JsonObject? result)
        => new(false, 0, result);

    public static NativeAnalysisOutcome Return(int exitCode)
        => new(true, exitCode, null);
}
