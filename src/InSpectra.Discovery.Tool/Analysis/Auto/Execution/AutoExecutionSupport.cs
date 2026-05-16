namespace InSpectra.Discovery.Tool.Analysis.Auto.Execution;

using InSpectra.Discovery.Tool.Analysis.Auto.Selection;

using InSpectra.Discovery.Tool.Analysis.Tools;

using InSpectra.Discovery.Tool.Analysis.Auto.Runners;


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
        AutoAnalysisAttempt selectedAttempt,
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
        => selectedAttempt.Mode switch
        {
            "hook" => RunHookAsync(
                selectedAttempt,
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
                selectedAttempt,
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
                selectedAttempt,
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
                selectedAttempt,
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
        AutoAnalysisAttempt selectedAttempt,
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
                    selectedAttempt.Framework ?? descriptor.CliFramework,
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
            selectedMode: selectedAttempt.Mode,
            selectedFramework: selectedAttempt.Framework,
            cancellationToken);

    private static Task<JsonObject> RunStaticAsync(
        AutoAnalysisAttempt selectedAttempt,
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
                    selectedAttempt.Framework ?? descriptor.CliFramework,
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
            selectedMode: selectedAttempt.Mode,
            selectedFramework: selectedAttempt.Framework,
            cancellationToken);

    private static Task<JsonObject> RunHookAsync(
        AutoAnalysisAttempt selectedAttempt,
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
                    selectedAttempt.Framework ?? descriptor.CliFramework,
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
            selectedMode: selectedAttempt.Mode,
            selectedFramework: selectedAttempt.Framework,
            cancellationToken);

    private static Task<JsonObject> RunHelpAsync(
        AutoAnalysisAttempt selectedAttempt,
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
            selectedMode: selectedAttempt.Mode,
            selectedFramework: selectedAttempt.Framework,
            cancellationToken);
}

internal sealed record NativeAnalysisOutcome(bool ShouldReturnImmediately, int ExitCode, JsonObject? Result)
{
    public static NativeAnalysisOutcome Continue(JsonObject? result)
        => new(false, 0, result);

    public static NativeAnalysisOutcome Return(int exitCode)
        => new(true, exitCode, null);
}

