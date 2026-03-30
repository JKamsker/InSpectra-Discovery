using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class CliFxAnalysisService
{
    private readonly CliFxToolRuntime _runtime = new();
    private readonly CliFxMetadataInspector _metadataInspector = new();
    private readonly CliFxOpenCliBuilder _openCliBuilder = new();
    private readonly CliFxCoverageClassifier _coverageClassifier = new();

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
        var tempRoot = Path.Combine(Path.GetTempPath(), $"inspectra-clifx-{packageId.ToLowerInvariant()}-{version.ToLowerInvariant()}-{Guid.NewGuid():N}");
        var outputDirectory = Path.GetFullPath(outputRoot);
        var resultPath = Path.Combine(outputDirectory, "result.json");
        var stopwatch = Stopwatch.StartNew();

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(tempRoot);
        var resolvedCliFramework = string.IsNullOrWhiteSpace(cliFramework) ? "CliFx" : cliFramework;

        var result = NonSpectreAnalysisResultSupport.CreateInitialResult(
            packageId,
            version,
            commandName,
            batchId,
            attempt,
            source,
            cliFramework: resolvedCliFramework,
            analysisMode: "clifx",
            analyzedAt: generatedAt);
        result["coverage"] = null;

        try
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
            }
            else
            {
                using var analysisTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                analysisTimeout.CancelAfter(TimeSpan.FromSeconds(analysisTimeoutSeconds));

                try
                {
                    await AnalyzeInstalledToolAsync(
                        result,
                        packageId,
                        version,
                        resolvedCommandName,
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
                        $"CliFx analysis exceeded the overall timeout of {analysisTimeoutSeconds} seconds.");
                }
            }
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

            _runtime.TerminateSandboxProcesses(tempRoot);
            if (Directory.Exists(tempRoot))
            {
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

    private async Task AnalyzeInstalledToolAsync(
        JsonObject result,
        string packageId,
        string version,
        string commandName,
        string outputDirectory,
        string tempRoot,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var environment = _runtime.CreateSandboxEnvironment(tempRoot);
        foreach (var directory in environment.Directories)
        {
            Directory.CreateDirectory(directory);
        }

        var installDirectory = Path.Combine(tempRoot, "tool");
        var installResult = await _runtime.InvokeProcessCaptureAsync(
            "dotnet",
            ["tool", "install", packageId, "--version", version, "--tool-path", installDirectory],
            tempRoot,
            environment.Values,
            installTimeoutSeconds,
            tempRoot,
            cancellationToken);

        result["steps"]!.AsObject()["install"] = installResult.ToJsonObject();
        result["timings"]!.AsObject()["installMs"] = installResult.DurationMs;

        if (installResult.TimedOut || installResult.ExitCode != 0)
        {
            NonSpectreAnalysisResultSupport.ApplyRetryableFailure(
                result,
                phase: "install",
                classification: installResult.TimedOut ? "install-timeout" : "install-failed",
                CliFxToolRuntime.NormalizeConsoleText(installResult.Stdout)
                ?? CliFxToolRuntime.NormalizeConsoleText(installResult.Stderr)
                ?? "Tool installation failed.");
            return;
        }

        var commandPath = _runtime.ResolveInstalledCommandPath(installDirectory, commandName);
        if (commandPath is null)
        {
            NonSpectreAnalysisResultSupport.ApplyRetryableFailure(
                result,
                phase: "install",
                classification: "installed-command-missing",
                $"Installed tool command '{commandName}' was not found.");
            return;
        }

        var crawlStopwatch = Stopwatch.StartNew();
        var staticCommands = NormalizeCommandLookup(_metadataInspector.Inspect(installDirectory));
        var crawler = new CliFxHelpCrawler(_runtime);
        var crawl = await crawler.CrawlAsync(commandPath, tempRoot, environment.Values, commandTimeoutSeconds, cancellationToken);
        crawlStopwatch.Stop();
        var coverage = _coverageClassifier.Classify(staticCommands.Count, crawl);
        var coverageJson = coverage.ToJsonObject();

        result["timings"]!.AsObject()["crawlMs"] = (int)Math.Round(crawlStopwatch.Elapsed.TotalMilliseconds);
        result["coverage"] = coverageJson;
        WriteCrawlArtifact(
            outputDirectory,
            result,
            CrawlArtifactBuilder.Build(
                crawl.Documents.Count,
                crawl.Captures,
                CliFxCrawlArtifactSupport.BuildMetadata(staticCommands, coverageJson)));
        if (crawl.Documents.Count == 0 && staticCommands.Count == 0)
        {
            NonSpectreAnalysisResultSupport.ApplyTerminalFailure(
                result,
                phase: "crawl",
                classification: "clifx-crawl-empty",
                "No CliFx help documents or metadata commands could be captured from the installed tool.");
            return;
        }

        var openCliDocument = _openCliBuilder.Build(commandName, version, staticCommands, crawl.Documents);
        if (!string.IsNullOrWhiteSpace(result["cliFramework"]?.GetValue<string>()))
        {
            openCliDocument["x-inspectra"]!.AsObject()["cliFramework"] = result["cliFramework"]!.GetValue<string>();
        }

        OpenCliDocumentSanitizer.ApplyNuGetMetadata(
            openCliDocument,
            result["nugetTitle"]?.GetValue<string>(),
            result["nugetDescription"]?.GetValue<string>());

        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliDocument);
        result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        NonSpectreAnalysisResultSupport.ApplySuccess(result, classification: "clifx-crawl", artifactSource: "crawled-from-clifx-help");
    }

    private static Dictionary<string, CliFxCommandDefinition> NormalizeCommandLookup(IReadOnlyDictionary<string, CliFxCommandDefinition> commands)
        => new(commands, StringComparer.OrdinalIgnoreCase);

    private static void WriteCrawlArtifact(string outputDirectory, JsonObject result, JsonObject crawlArtifact)
    {
        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "crawl.json"), crawlArtifact);
        result["artifacts"]!.AsObject()["crawlArtifact"] = "crawl.json";
    }
}
