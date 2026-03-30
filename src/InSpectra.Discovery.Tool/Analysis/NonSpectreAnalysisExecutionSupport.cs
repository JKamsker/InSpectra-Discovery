using System.Diagnostics;
using System.Text.Json.Nodes;

internal static class NonSpectreAnalysisExecutionSupport
{
    public static Task<int> RunQuietAsync(
        ToolCommandRuntime runtime,
        NonSpectreAnalysisExecutionDefinition definition,
        Func<JsonObject, string, string, string?, CancellationToken, Task<NonSpectreAnalysisBootstrapResult>> bootstrapAsync,
        Func<NonSpectreInstalledToolAnalysisRequest, CancellationToken, Task> analyzeAsync,
        string packageId,
        string version,
        string? commandName,
        string? cliFramework,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
        => RunCoreAsync(
            runtime,
            definition,
            bootstrapAsync,
            analyzeAsync,
            packageId,
            version,
            commandName,
            cliFramework,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            json: false,
            suppressOutput: true,
            cancellationToken);

    public static Task<int> RunAsync(
        ToolCommandRuntime runtime,
        NonSpectreAnalysisExecutionDefinition definition,
        Func<JsonObject, string, string, string?, CancellationToken, Task<NonSpectreAnalysisBootstrapResult>> bootstrapAsync,
        Func<NonSpectreInstalledToolAnalysisRequest, CancellationToken, Task> analyzeAsync,
        string packageId,
        string version,
        string? commandName,
        string? cliFramework,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        CancellationToken cancellationToken)
        => RunCoreAsync(
            runtime,
            definition,
            bootstrapAsync,
            analyzeAsync,
            packageId,
            version,
            commandName,
            cliFramework,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            analysisTimeoutSeconds,
            commandTimeoutSeconds,
            json,
            suppressOutput: false,
            cancellationToken);

    private static async Task<int> RunCoreAsync(
        ToolCommandRuntime runtime,
        NonSpectreAnalysisExecutionDefinition definition,
        Func<JsonObject, string, string, string?, CancellationToken, Task<NonSpectreAnalysisBootstrapResult>> bootstrapAsync,
        Func<NonSpectreInstalledToolAnalysisRequest, CancellationToken, Task> analyzeAsync,
        string packageId,
        string version,
        string? commandName,
        string? cliFramework,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        bool suppressOutput,
        CancellationToken cancellationToken)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"{definition.TempRootPrefix}-{packageId.ToLowerInvariant()}-{version.ToLowerInvariant()}-{Guid.NewGuid():N}");
        var outputDirectory = Path.GetFullPath(outputRoot);
        var resultPath = Path.Combine(outputDirectory, "result.json");
        var stopwatch = Stopwatch.StartNew();

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(tempRoot);

        var resolvedCliFramework = ResolveCliFramework(cliFramework, definition.DefaultCliFramework);
        var result = NonSpectreAnalysisResultSupport.CreateInitialResult(
            packageId,
            version,
            commandName,
            batchId,
            attempt,
            source,
            cliFramework: resolvedCliFramework,
            analysisMode: definition.AnalysisMode,
            analyzedAt: generatedAt);
        if (definition.InitializeCoverage)
        {
            result["coverage"] = null;
        }

        try
        {
            await PopulateAndAnalyzeAsync(
                result,
                definition,
                bootstrapAsync,
                analyzeAsync,
                packageId,
                version,
                commandName,
                resolvedCliFramework,
                outputDirectory,
                tempRoot,
                installTimeoutSeconds,
                analysisTimeoutSeconds,
                commandTimeoutSeconds,
                cancellationToken);
        }
        catch (Exception ex)
        {
            NonSpectreAnalysisResultSupport.ApplyUnexpectedRetryableFailure(result, ex.Message);
        }
        finally
        {
            stopwatch.Stop();
            result["timings"]!.AsObject()["totalMs"] = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);
            NonSpectreAnalysisResultSupport.FinalizeFailureSignature(result);
            RepositoryPathResolver.WriteJsonFile(resultPath, result);
            CleanupTempRoot(runtime, tempRoot);
        }

        if (suppressOutput)
        {
            return 0;
        }

        return await AnalysisCommandOutputSupport.WriteResultAsync(
            packageId,
            version,
            resultPath,
            result["disposition"]?.GetValue<string>(),
            json,
            cancellationToken);
    }

    private static async Task PopulateAndAnalyzeAsync(
        JsonObject result,
        NonSpectreAnalysisExecutionDefinition definition,
        Func<JsonObject, string, string, string?, CancellationToken, Task<NonSpectreAnalysisBootstrapResult>> bootstrapAsync,
        Func<NonSpectreInstalledToolAnalysisRequest, CancellationToken, Task> analyzeAsync,
        string packageId,
        string version,
        string? commandName,
        string? cliFramework,
        string outputDirectory,
        string tempRoot,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var bootstrap = await bootstrapAsync(result, packageId, version, commandName, cancellationToken);
        var resolvedCommandName = bootstrap.CommandName;
        result["command"] = resolvedCommandName;
        result["entryPoint"] = bootstrap.EntryPointPath;
        result["toolSettingsPath"] = bootstrap.ToolSettingsPath;
        if (string.IsNullOrWhiteSpace(resolvedCommandName))
        {
            NonSpectreAnalysisResultSupport.ApplyRetryableFailure(
                result,
                phase: "bootstrap",
                classification: "tool-command-missing",
                $"No tool command could be resolved for package '{packageId}' version '{version}'.");
            return;
        }

        using var analysisTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        analysisTimeout.CancelAfter(TimeSpan.FromSeconds(analysisTimeoutSeconds));

        try
        {
            await analyzeAsync(
                new NonSpectreInstalledToolAnalysisRequest(
                    Result: result,
                    PackageId: packageId,
                    Version: version,
                    CommandName: resolvedCommandName,
                    CliFramework: cliFramework,
                    OutputDirectory: outputDirectory,
                    TempRoot: tempRoot,
                    InstallTimeoutSeconds: installTimeoutSeconds,
                    CommandTimeoutSeconds: commandTimeoutSeconds),
                analysisTimeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && analysisTimeout.IsCancellationRequested)
        {
            NonSpectreAnalysisResultSupport.ApplyRetryableFailure(
                result,
                phase: "analysis",
                classification: "analysis-timeout",
                $"{definition.TimeoutLabel} exceeded the overall timeout of {analysisTimeoutSeconds} seconds.");
        }
    }

    private static string? ResolveCliFramework(string? cliFramework, string? defaultCliFramework)
        => !string.IsNullOrWhiteSpace(cliFramework) ? cliFramework : defaultCliFramework;

    private static void CleanupTempRoot(ToolCommandRuntime runtime, string tempRoot)
    {
        runtime.TerminateSandboxProcesses(tempRoot);
        if (!Directory.Exists(tempRoot))
        {
            return;
        }

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal sealed record NonSpectreAnalysisExecutionDefinition(
    string AnalysisMode,
    string TempRootPrefix,
    string TimeoutLabel,
    string? DefaultCliFramework = null,
    bool InitializeCoverage = false);

internal sealed record NonSpectreInstalledToolAnalysisRequest(
    JsonObject Result,
    string PackageId,
    string Version,
    string CommandName,
    string? CliFramework,
    string OutputDirectory,
    string TempRoot,
    int InstallTimeoutSeconds,
    int CommandTimeoutSeconds);
