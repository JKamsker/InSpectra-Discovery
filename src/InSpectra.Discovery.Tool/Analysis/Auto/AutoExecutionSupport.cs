namespace InSpectra.Discovery.Tool.Analysis.Auto;

using System.Text.Json.Nodes;

internal static class AutoExecutionSupport
{
    public static Task<NativeAnalysisOutcome> TryRunNativeAnalysisAsync(
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
        => AutoNativeExecutionSupport.TryRunAsync(
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
        IAutoHelpRunner helpRunner,
        IAutoCliFxRunner cliFxRunner,
        IAutoStaticRunner staticRunner,
        IAutoHookRunner hookRunner,
        string packageId,
        string version,
        ToolDescriptor descriptor,
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
            "hook" => RunHookAsync(
                hookRunner,
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
        IAutoCliFxRunner cliFxRunner,
        string packageId,
        string version,
        ToolDescriptor descriptor,
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
        => AutoSelectedAnalyzerSupport.RunAsync(
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
        IAutoStaticRunner staticRunner,
        string packageId,
        string version,
        ToolDescriptor descriptor,
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
        => AutoSelectedAnalyzerSupport.RunAsync(
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

    private static Task<JsonObject> RunHookAsync(
        IAutoHookRunner hookRunner,
        string packageId,
        string version,
        ToolDescriptor descriptor,
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
        => AutoSelectedAnalyzerSupport.RunAsync(
            async token =>
            {
                await hookRunner.RunAsync(
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
            selectedMode: "hook",
            cancellationToken);

    private static Task<JsonObject> RunHelpAsync(
        IAutoHelpRunner helpRunner,
        string packageId,
        string version,
        ToolDescriptor descriptor,
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
        => AutoSelectedAnalyzerSupport.RunAsync(
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
