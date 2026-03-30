using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class StaticAnalysisService
{
    private readonly StaticAnalysisToolRuntime _runtime = new();
    private readonly StaticAnalysisInstalledToolAnalysisSupport _installedToolAnalyzer;

    public StaticAnalysisService()
    {
        _installedToolAnalyzer = new StaticAnalysisInstalledToolAnalysisSupport(
            _runtime,
            new StaticAnalysisAssemblyInspectionSupport(new DnlibAssemblyScanner()),
            new StaticAnalysisOpenCliBuilder(),
            new StaticAnalysisCoverageClassifier());
    }

    public Task<int> RunQuietAsync(
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

    public async Task<int> RunAsync(
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
        => await RunCoreAsync(
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

    private async Task<int> RunCoreAsync(
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
        var tempRoot = Path.Combine(Path.GetTempPath(), $"inspectra-static-{packageId.ToLowerInvariant()}-{version.ToLowerInvariant()}-{Guid.NewGuid():N}");
        var outputDirectory = Path.GetFullPath(outputRoot);
        var resultPath = Path.Combine(outputDirectory, "result.json");
        var stopwatch = Stopwatch.StartNew();

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(tempRoot);
        var resolvedCliFramework = string.IsNullOrWhiteSpace(cliFramework) ? "CommandLineParser" : cliFramework;

        var result = NonSpectreAnalysisResultSupport.CreateInitialResult(
            packageId,
            version,
            commandName,
            batchId,
            attempt,
            source,
            cliFramework: resolvedCliFramework,
            analysisMode: "static",
            analyzedAt: generatedAt);
        result["coverage"] = null;

        try
        {
            await PopulateAndAnalyzeAsync(
                result,
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
            CleanupTempRoot(tempRoot);
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

    private async Task PopulateAndAnalyzeAsync(
        JsonObject result,
        string packageId,
        string version,
        string? commandName,
        string cliFramework,
        string outputDirectory,
        string tempRoot,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var scope = ToolRuntime.CreateNuGetApiClientScope();
        var bootstrap = await NonSpectreAnalysisBootstrapSupport.PopulateResultAsync(
            result,
            scope.Client,
            packageId,
            version,
            commandName,
            cancellationToken);
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
            await _installedToolAnalyzer.AnalyzeAsync(
                result,
                packageId,
                version,
                resolvedCommandName,
                cliFramework,
                outputDirectory,
                tempRoot,
                installTimeoutSeconds,
                commandTimeoutSeconds,
                analysisTimeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && analysisTimeout.IsCancellationRequested)
        {
            NonSpectreAnalysisResultSupport.ApplyRetryableFailure(
                result,
                phase: "analysis",
                classification: "analysis-timeout",
                $"Static analysis exceeded the overall timeout of {analysisTimeoutSeconds} seconds.");
        }
    }

    private void CleanupTempRoot(string tempRoot)
    {
        _runtime.TerminateSandboxProcesses(tempRoot);
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
