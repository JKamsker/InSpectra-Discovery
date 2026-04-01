namespace InSpectra.Discovery.Tool.Help.Crawling;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;

using InSpectra.Discovery.Tool.Infrastructure.Artifacts;

using InSpectra.Discovery.Tool.Help.OpenCli;

using InSpectra.Discovery.Tool.Infrastructure.Commands;

using InSpectra.Discovery.Tool.Analysis;
using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class InstalledToolAnalyzer
{
    private readonly CommandRuntime _runtime;
    private readonly OpenCliBuilder _openCliBuilder;

    public InstalledToolAnalyzer(CommandRuntime runtime, OpenCliBuilder openCliBuilder)
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
        var installedTool = await CommandInstallationSupport.InstallToolAsync(
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
        var crawler = new Crawler(_runtime);
        var crawl = await crawler.CrawlAsync(installedTool.CommandPath, commandName, tempRoot, installedTool.Environment, commandTimeoutSeconds, cancellationToken);
        crawlStopwatch.Stop();

        result["timings"]!.AsObject()["crawlMs"] = (int)Math.Round(crawlStopwatch.Elapsed.TotalMilliseconds);
        if (!CommandInstallationSupport.TryWriteCrawlArtifactOrApplyFailure(
            outputDirectory,
            result,
            CrawlArtifactBuilder.Build(crawl.Documents.Count, crawl.Captures)))
        {
            return;
        }

        if (crawl.Documents.Count == 0)
        {
            var runtimeIssues = crawl.CaptureSummaries.Values
                .Select(summary => DotnetRuntimeCompatibilitySupport.DetectMissingFramework(
                    summary.Command,
                    summary.Stdout,
                    summary.Stderr))
                .Where(issue => issue is not null)
                .Cast<DotnetRuntimeIssue>()
                .ToArray();
            if (runtimeIssues.Length > 0)
            {
                NonSpectreResultSupport.ApplyTerminalFailure(
                    result,
                    phase: "crawl",
                    classification: "help-crawl-runtime-blocked",
                    DotnetRuntimeCompatibilitySupport.BuildMissingFrameworkFailureMessage(
                        runtimeIssues.Select(issue => issue.Command).ToArray(),
                        runtimeIssues
                            .Where(issue => issue.Requirement is not null)
                            .Select(issue => issue.Requirement!)
                            .Distinct()
                            .ToArray()));
                return;
            }

            NonSpectreResultSupport.ApplyTerminalFailure(
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
            NonSpectreResultSupport.ApplyTerminalFailure(
                result,
                phase: "opencli",
                classification: "invalid-opencli-artifact",
                validationError ?? "Generated OpenCLI artifact is not publishable.");
            return;
        }

        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliDocument);
        result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        NonSpectreResultSupport.ApplySuccess(result, classification: "help-crawl", artifactSource: "crawled-from-help");
    }
}
