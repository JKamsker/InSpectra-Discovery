using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class ToolHelpInstalledToolAnalysisSupport
{
    private readonly ToolCommandRuntime _runtime;
    private readonly ToolHelpOpenCliBuilder _openCliBuilder;

    public ToolHelpInstalledToolAnalysisSupport(ToolCommandRuntime runtime, ToolHelpOpenCliBuilder openCliBuilder)
    {
        _runtime = runtime;
        _openCliBuilder = openCliBuilder;
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
        var installedTool = await ToolCommandInstallationSupport.InstallToolAsync(
            _runtime,
            result,
            packageId,
            version,
            commandName,
            tempRoot,
            installTimeoutSeconds,
            cancellationToken);
        if (installedTool is null)
        {
            return;
        }

        var crawlStopwatch = Stopwatch.StartNew();
        var crawler = new ToolHelpCrawler(_runtime);
        var crawl = await crawler.CrawlAsync(installedTool.CommandPath, tempRoot, installedTool.Environment, commandTimeoutSeconds, cancellationToken);
        crawlStopwatch.Stop();

        result["timings"]!.AsObject()["crawlMs"] = (int)Math.Round(crawlStopwatch.Elapsed.TotalMilliseconds);
        ToolCommandInstallationSupport.WriteCrawlArtifact(outputDirectory, result, CrawlArtifactBuilder.Build(crawl.Documents.Count, crawl.Captures));
        if (crawl.Documents.Count == 0)
        {
            NonSpectreAnalysisResultSupport.ApplyTerminalFailure(
                result,
                phase: "crawl",
                classification: "help-crawl-empty",
                "No help documents could be captured from the installed tool.");
            return;
        }

        var openCliDocument = _openCliBuilder.Build(commandName, version, crawl.Documents);
        if (!string.IsNullOrWhiteSpace(result["cliFramework"]?.GetValue<string>()))
        {
            openCliDocument["x-inspectra"]!["cliFramework"] = result["cliFramework"]!.GetValue<string>();
        }

        OpenCliDocumentSanitizer.ApplyNuGetMetadata(
            openCliDocument,
            result["nugetTitle"]?.GetValue<string>(),
            result["nugetDescription"]?.GetValue<string>());

        if (!OpenCliDocumentValidator.TryValidateDocument(openCliDocument, out var validationError))
        {
            NonSpectreAnalysisResultSupport.ApplyTerminalFailure(
                result,
                phase: "opencli",
                classification: "invalid-opencli-artifact",
                validationError ?? "Generated OpenCLI artifact is not publishable.");
            return;
        }

        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliDocument);
        result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        NonSpectreAnalysisResultSupport.ApplySuccess(result, classification: "help-crawl", artifactSource: "crawled-from-help");
    }
}
