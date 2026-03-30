using InSpectra.Discovery.Tool.Analysis;
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
        var staticCommands = NormalizeCommandLookup(_metadataInspector.Inspect(installedTool.InstallDirectory));
        var crawler = new CliFxHelpCrawler(_runtime);
        var crawl = await crawler.CrawlAsync(installedTool.CommandPath, tempRoot, installedTool.Environment, commandTimeoutSeconds, cancellationToken);
        crawlStopwatch.Stop();
        var coverage = _coverageClassifier.Classify(staticCommands.Count, crawl);
        var coverageJson = coverage.ToJsonObject();

        result["timings"]!.AsObject()["crawlMs"] = (int)Math.Round(crawlStopwatch.Elapsed.TotalMilliseconds);
        result["coverage"] = coverageJson;
        ToolCommandInstallationSupport.WriteCrawlArtifact(
            outputDirectory,
            result,
            CrawlArtifactBuilder.Build(
                crawl.Documents.Count,
                crawl.Captures,
                CliFxCrawlArtifactSupport.BuildMetadata(staticCommands, coverageJson)));
        if (crawl.Documents.Count == 0 && staticCommands.Count == 0)
        {
            NonSpectreResultSupport.ApplyTerminalFailure(
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
        NonSpectreResultSupport.ApplySuccess(result, classification: "clifx-crawl", artifactSource: "crawled-from-clifx-help");
    }

    private static Dictionary<string, CliFxCommandDefinition> NormalizeCommandLookup(IReadOnlyDictionary<string, CliFxCommandDefinition> commands)
        => new(commands, StringComparer.OrdinalIgnoreCase);
}
