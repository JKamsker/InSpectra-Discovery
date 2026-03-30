using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class CliFxInstalledToolAnalysisSupport
{
    private readonly CliFxToolRuntime _runtime;
    private readonly CliFxMetadataInspector _metadataInspector;
    private readonly CliFxOpenCliBuilder _openCliBuilder;
    private readonly CliFxCoverageClassifier _coverageClassifier;

    public CliFxInstalledToolAnalysisSupport(
        CliFxToolRuntime runtime,
        CliFxMetadataInspector metadataInspector,
        CliFxOpenCliBuilder openCliBuilder,
        CliFxCoverageClassifier coverageClassifier)
    {
        _runtime = runtime;
        _metadataInspector = metadataInspector;
        _openCliBuilder = openCliBuilder;
        _coverageClassifier = coverageClassifier;
    }

    public async Task AnalyzeAsync(
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
