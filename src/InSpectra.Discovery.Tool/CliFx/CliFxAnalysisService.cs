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

        var result = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["command"] = commandName,
            ["batchId"] = batchId,
            ["attempt"] = attempt,
            ["source"] = source,
            ["analyzedAt"] = generatedAt.ToString("O"),
            ["disposition"] = "retryable-failure",
            ["failureMessage"] = null,
            ["projectUrl"] = null,
            ["sourceRepositoryUrl"] = null,
            ["packageContentUrl"] = null,
            ["timings"] = new JsonObject
            {
                ["totalMs"] = null,
                ["installMs"] = null,
                ["crawlMs"] = null,
            },
            ["steps"] = new JsonObject
            {
                ["install"] = null,
            },
            ["coverage"] = null,
            ["artifacts"] = new JsonObject
            {
                ["opencliArtifact"] = null,
                ["crawlArtifact"] = null,
            },
        };

        try
        {
            using var scope = ToolRuntime.CreateNuGetApiClientScope();
            var (registrationLeaf, catalogLeaf) = await PackageVersionResolver.ResolveAsync(scope.Client, packageId, version, cancellationToken);
            result["projectUrl"] = catalogLeaf.ProjectUrl;
            result["sourceRepositoryUrl"] = PackageVersionResolver.NormalizeRepositoryUrl(catalogLeaf.Repository?.Url);
            result["packageContentUrl"] = registrationLeaf.PackageContent;

            var packageInspection = await new PackageArchiveInspector(scope.Client).InspectAsync(registrationLeaf.PackageContent, cancellationToken);
            var resolvedCommandName = string.IsNullOrWhiteSpace(commandName) ? packageInspection.ToolCommandNames.FirstOrDefault() : commandName;
            if (string.IsNullOrWhiteSpace(resolvedCommandName))
            {
                result["failureMessage"] = $"No tool command could be resolved for package '{packageId}' version '{version}'.";
                result["disposition"] = "terminal-failure";
            }
            else
            {
                result["command"] = resolvedCommandName;
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
                    result["failureMessage"] = $"CliFx analysis exceeded the overall timeout of {analysisTimeoutSeconds} seconds.";
                    result["disposition"] = "retryable-failure";
                }
            }
        }
        catch (Exception ex)
        {
            result["failureMessage"] = ex.Message;
            result["disposition"] = "retryable-failure";
        }
        finally
        {
            stopwatch.Stop();
            result["timings"]!.AsObject()["totalMs"] = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);
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

        var output = ToolRuntime.CreateOutput();
        return await output.WriteSuccessAsync(
            new
            {
                packageId,
                version,
                disposition = result["disposition"]?.GetValue<string>(),
                resultPath,
            },
            [
                new SummaryRow("Package", $"{packageId} {version}"),
                new SummaryRow("Disposition", result["disposition"]?.GetValue<string>() ?? string.Empty),
                new SummaryRow("Result artifact", resultPath),
            ],
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
            result["failureMessage"] = CliFxToolRuntime.NormalizeConsoleText(installResult.Stdout)
                ?? CliFxToolRuntime.NormalizeConsoleText(installResult.Stderr)
                ?? "Tool installation failed.";
            result["disposition"] = "terminal-failure";
            return;
        }

        var commandPath = _runtime.ResolveInstalledCommandPath(installDirectory, commandName);
        if (commandPath is null)
        {
            result["failureMessage"] = $"Installed tool command '{commandName}' was not found.";
            result["disposition"] = "terminal-failure";
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
            result["failureMessage"] = "No CliFx help documents or metadata commands could be captured from the installed tool.";
            result["disposition"] = "terminal-failure";
            return;
        }

        var openCliDocument = _openCliBuilder.Build(commandName, version, staticCommands, crawl.Documents);
        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliDocument);
        result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        result["disposition"] = "success";
    }

    private static Dictionary<string, CliFxCommandDefinition> NormalizeCommandLookup(IReadOnlyDictionary<string, CliFxCommandDefinition> commands)
        => new(commands, StringComparer.OrdinalIgnoreCase);

    private static void WriteCrawlArtifact(string outputDirectory, JsonObject result, JsonObject crawlArtifact)
    {
        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "crawl.json"), crawlArtifact);
        result["artifacts"]!.AsObject()["crawlArtifact"] = "crawl.json";
    }
}
