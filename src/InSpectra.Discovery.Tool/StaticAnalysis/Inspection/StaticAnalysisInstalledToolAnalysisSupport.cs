namespace InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

using InSpectra.Discovery.Tool.Frameworks;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using InSpectra.Discovery.Tool.Analysis.NonSpectre;

using InSpectra.Discovery.Tool.StaticAnalysis.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Artifacts;

using InSpectra.Discovery.Tool.Help.Crawling;

using InSpectra.Discovery.Tool.Infrastructure.Commands;

using InSpectra.Discovery.Tool.StaticAnalysis.OpenCli;

using InSpectra.Discovery.Tool.Analysis;
using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class StaticInstalledToolAnalysisSupport
{
    private readonly StaticAnalysisRuntime _runtime;
    private readonly StaticAnalysisAssemblyInspectionSupport _assemblyInspectionSupport;
    private readonly StaticAnalysisOpenCliBuilder _openCliBuilder;
    private readonly StaticAnalysisCoverageClassifier _coverageClassifier;

    public StaticInstalledToolAnalysisSupport(
        StaticAnalysisRuntime runtime,
        StaticAnalysisAssemblyInspectionSupport assemblyInspectionSupport,
        StaticAnalysisOpenCliBuilder openCliBuilder,
        StaticAnalysisCoverageClassifier coverageClassifier)
    {
        _runtime = runtime;
        _assemblyInspectionSupport = assemblyInspectionSupport;
        _openCliBuilder = openCliBuilder;
        _coverageClassifier = coverageClassifier;
    }

    public async Task AnalyzeAsync(
        JsonObject result,
        string packageId,
        string version,
        string commandName,
        string cliFramework,
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
        var inspection = _assemblyInspectionSupport.InspectAssemblies(installedTool.InstallDirectory, cliFramework);
        if (ApplyInspectionFailure(result, inspection))
        {
            return;
        }

        var crawler = new Crawler(_runtime);
        var crawl = await crawler.CrawlAsync(installedTool.CommandPath, tempRoot, installedTool.Environment, commandTimeoutSeconds, cancellationToken);
        crawlStopwatch.Stop();

        var staticCommands = inspection.Commands;
        var coverage = _coverageClassifier.Classify(staticCommands.Count, crawl.Documents.Count, crawl.Captures);
        var coverageJson = coverage.ToJsonObject();

        result["timings"]!.AsObject()["crawlMs"] = (int)Math.Round(crawlStopwatch.Elapsed.TotalMilliseconds);
        result["coverage"] = coverageJson;
        if (!CommandInstallationSupport.TryWriteCrawlArtifactOrApplyFailure(
            outputDirectory,
            result,
            CrawlArtifactBuilder.Build(
                crawl.Documents.Count,
                crawl.Captures,
                StaticAnalysisCrawlArtifactSupport.BuildMetadata(staticCommands, coverageJson))))
        {
            return;
        }

        if (crawl.Documents.Count == 0 && staticCommands.Count == 0)
        {
            NonSpectreResultSupport.ApplyTerminalFailure(
                result,
                phase: "crawl",
                classification: "static-crawl-empty",
                "No help documents or static metadata could be captured from the installed tool.");
            return;
        }

        var resolvedFramework = ResolveFrameworkName(cliFramework);
        var openCliDocument = _openCliBuilder.Build(commandName, version, resolvedFramework, staticCommands, crawl.Documents);
        if (!string.IsNullOrWhiteSpace(result["cliFramework"]?.GetValue<string>()))
        {
            openCliDocument["x-inspectra"]!.AsObject()["cliFramework"] = result["cliFramework"]!.GetValue<string>();
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
        NonSpectreResultSupport.ApplySuccess(result, classification: "static-crawl", artifactSource: "static-analysis");
    }

    private static bool ApplyInspectionFailure(JsonObject result, StaticAnalysisAssemblyInspectionResult inspection)
    {
        if (inspection.InspectionOutcome is "framework-not-found" or "no-reader")
        {
            result["inspectionOutcome"] = inspection.InspectionOutcome;
            result["cliFramework"] = null;
            NonSpectreResultSupport.ApplyTerminalFailure(
                result,
                phase: "static-analysis",
                classification: "custom-parser",
                $"Claimed framework '{inspection.ClaimedFramework}' was not found in any assembly. Tool likely uses a custom argument parser.");
            return true;
        }

        if (inspection.InspectionOutcome is "no-attributes")
        {
            result["inspectionOutcome"] = inspection.InspectionOutcome;
            NonSpectreResultSupport.ApplyTerminalFailure(
                result,
                phase: "static-analysis",
                classification: "custom-parser-no-attributes",
                $"Framework '{inspection.ClaimedFramework}' assembly found in {inspection.ScannedModuleCount} module(s) but no recognizable attributes detected. Tool may use fluent API or non-standard configuration.");
            return true;
        }

        return false;
    }

    private static string ResolveFrameworkName(string cliFramework)
        => CliFrameworkProviderRegistry.ResolveStaticAnalysisAdapter(cliFramework)?.FrameworkName ?? cliFramework;
}
