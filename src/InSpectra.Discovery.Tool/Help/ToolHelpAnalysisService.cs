using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class ToolHelpAnalysisService
{
    private readonly ToolCommandRuntime _runtime = new();
    private readonly ToolHelpOpenCliBuilder _openCliBuilder = new();

    public Task<int> RunQuietAsync(
        string packageId,
        string version,
        string? commandName,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        string? cliFramework,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
        => RunCoreAsync(packageId, version, commandName, outputRoot, batchId, attempt, source, cliFramework, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, json: false, suppressOutput: true, cancellationToken);

    public async Task<int> RunAsync(
        string packageId,
        string version,
        string? commandName,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        string? cliFramework,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        CancellationToken cancellationToken)
        => await RunCoreAsync(packageId, version, commandName, outputRoot, batchId, attempt, source, cliFramework, installTimeoutSeconds, analysisTimeoutSeconds, commandTimeoutSeconds, json, suppressOutput: false, cancellationToken);

    private async Task<int> RunCoreAsync(
        string packageId,
        string version,
        string? commandName,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        string? cliFramework,
        int installTimeoutSeconds,
        int analysisTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        bool suppressOutput,
        CancellationToken cancellationToken)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"inspectra-help-{packageId.ToLowerInvariant()}-{version.ToLowerInvariant()}-{Guid.NewGuid():N}");
        var outputDirectory = Path.GetFullPath(outputRoot);
        var resultPath = Path.Combine(outputDirectory, "result.json");
        var stopwatch = Stopwatch.StartNew();

        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(tempRoot);

        var result = CreateResult(packageId, version, commandName, batchId, attempt, source, cliFramework, generatedAt);

        try
        {
            using var scope = ToolRuntime.CreateNuGetApiClientScope();
            var (registrationLeaf, catalogLeaf) = await PackageVersionResolver.ResolveAsync(scope.Client, packageId, version, cancellationToken);
            result["packageUrl"] = $"https://www.nuget.org/packages/{packageId}/{version}";
            result["projectUrl"] = catalogLeaf.ProjectUrl;
            result["sourceRepositoryUrl"] = PackageVersionResolver.NormalizeRepositoryUrl(catalogLeaf.Repository?.Url);
            result["registrationLeafUrl"] = registrationLeaf.Id;
            result["catalogEntryUrl"] = registrationLeaf.CatalogEntryUrl;
            result["packageContentUrl"] = registrationLeaf.PackageContent;
            result["publishedAt"] = registrationLeaf.Published?.ToUniversalTime().ToString("O");

            var resolvedCommandName = commandName;
            if (string.IsNullOrWhiteSpace(resolvedCommandName))
            {
                var packageInspection = await new PackageArchiveInspector(scope.Client).InspectAsync(registrationLeaf.PackageContent, cancellationToken);
                resolvedCommandName = packageInspection.ToolCommandNames.FirstOrDefault();
            }
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
                    result["failureMessage"] = $"Help analysis exceeded the overall timeout of {analysisTimeoutSeconds} seconds.";
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
            result["failureMessage"] = ToolCommandRuntime.NormalizeConsoleText(installResult.Stdout)
                ?? ToolCommandRuntime.NormalizeConsoleText(installResult.Stderr)
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
        var crawler = new ToolHelpCrawler(_runtime);
        var crawl = await crawler.CrawlAsync(commandPath, tempRoot, environment.Values, commandTimeoutSeconds, cancellationToken);
        crawlStopwatch.Stop();

        result["timings"]!.AsObject()["crawlMs"] = (int)Math.Round(crawlStopwatch.Elapsed.TotalMilliseconds);
        if (crawl.Documents.Count == 0)
        {
            result["failureMessage"] = "No help documents could be captured from the installed tool.";
            result["disposition"] = "terminal-failure";
            return;
        }

        var openCliDocument = _openCliBuilder.Build(commandName, version, crawl.Documents);
        if (!string.IsNullOrWhiteSpace(result["cliFramework"]?.GetValue<string>()))
        {
            openCliDocument["x-inspectra"]!["cliFramework"] = result["cliFramework"]!.GetValue<string>();
        }

        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliDocument);
        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "crawl.json"), new JsonObject
        {
            ["commandCount"] = crawl.Documents.Count,
            ["commands"] = new JsonArray(crawl.Captures.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).Select(pair => pair.Value).ToArray()),
        });

        result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        result["artifacts"]!.AsObject()["crawlArtifact"] = "crawl.json";
        result["disposition"] = "success";
    }

    private static JsonObject CreateResult(
        string packageId,
        string version,
        string? commandName,
        string batchId,
        int attempt,
        string source,
        string? cliFramework,
        DateTimeOffset generatedAt)
        => new()
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["command"] = commandName,
            ["batchId"] = batchId,
            ["attempt"] = attempt,
            ["source"] = source,
            ["cliFramework"] = cliFramework,
            ["analyzedAt"] = generatedAt.ToString("O"),
            ["disposition"] = "retryable-failure",
            ["failureMessage"] = null,
            ["publishedAt"] = null,
            ["packageUrl"] = null,
            ["projectUrl"] = null,
            ["sourceRepositoryUrl"] = null,
            ["registrationLeafUrl"] = null,
            ["catalogEntryUrl"] = null,
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
            ["artifacts"] = new JsonObject
            {
                ["opencliArtifact"] = null,
                ["crawlArtifact"] = null,
            },
        };

}
