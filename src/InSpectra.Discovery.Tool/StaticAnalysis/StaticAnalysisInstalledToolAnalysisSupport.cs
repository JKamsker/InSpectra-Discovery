using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class StaticAnalysisInstalledToolAnalysisSupport
{
    private readonly StaticAnalysisToolRuntime _runtime;
    private readonly StaticAnalysisAssemblyInspectionSupport _assemblyInspectionSupport;
    private readonly StaticAnalysisOpenCliBuilder _openCliBuilder;
    private readonly StaticAnalysisCoverageClassifier _coverageClassifier;

    public StaticAnalysisInstalledToolAnalysisSupport(
        StaticAnalysisToolRuntime runtime,
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
                StaticAnalysisToolRuntime.NormalizeConsoleText(installResult.Stdout)
                ?? StaticAnalysisToolRuntime.NormalizeConsoleText(installResult.Stderr)
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
        var inspection = _assemblyInspectionSupport.InspectAssemblies(installDirectory, cliFramework);
        if (ApplyInspectionFailure(result, inspection))
        {
            return;
        }

        var crawler = new ToolHelpCrawler(_runtime);
        var crawl = await crawler.CrawlAsync(commandPath, tempRoot, environment.Values, commandTimeoutSeconds, cancellationToken);
        crawlStopwatch.Stop();

        var staticCommands = inspection.Commands;
        var coverage = _coverageClassifier.Classify(staticCommands.Count, crawl.Documents.Count, crawl.Captures);
        var coverageJson = coverage.ToJsonObject();

        result["timings"]!.AsObject()["crawlMs"] = (int)Math.Round(crawlStopwatch.Elapsed.TotalMilliseconds);
        result["coverage"] = coverageJson;
        WriteCrawlArtifact(
            outputDirectory,
            result,
            CrawlArtifactBuilder.Build(
                crawl.Documents.Count,
                crawl.Captures,
                StaticAnalysisCrawlArtifactSupport.BuildMetadata(staticCommands, coverageJson)));

        if (crawl.Documents.Count == 0 && staticCommands.Count == 0)
        {
            NonSpectreAnalysisResultSupport.ApplyTerminalFailure(
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
            NonSpectreAnalysisResultSupport.ApplyTerminalFailure(
                result,
                phase: "opencli",
                classification: "invalid-opencli-artifact",
                validationError ?? "Generated OpenCLI artifact is not publishable.");
            return;
        }

        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "opencli.json"), openCliDocument);
        result["artifacts"]!.AsObject()["opencliArtifact"] = "opencli.json";
        NonSpectreAnalysisResultSupport.ApplySuccess(result, classification: "static-crawl", artifactSource: "static-analysis");
    }

    private static bool ApplyInspectionFailure(JsonObject result, StaticAnalysisAssemblyInspectionResult inspection)
    {
        if (inspection.InspectionOutcome is "framework-not-found" or "no-reader")
        {
            result["inspectionOutcome"] = inspection.InspectionOutcome;
            result["cliFramework"] = null;
            NonSpectreAnalysisResultSupport.ApplyTerminalFailure(
                result,
                phase: "static-analysis",
                classification: "custom-parser",
                $"Claimed framework '{inspection.ClaimedFramework}' was not found in any assembly. Tool likely uses a custom argument parser.");
            return true;
        }

        if (inspection.InspectionOutcome is "no-attributes")
        {
            result["inspectionOutcome"] = inspection.InspectionOutcome;
            NonSpectreAnalysisResultSupport.ApplyTerminalFailure(
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

    private static void WriteCrawlArtifact(string outputDirectory, JsonObject result, JsonObject crawlArtifact)
    {
        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "crawl.json"), crawlArtifact);
        result["artifacts"]!.AsObject()["crawlArtifact"] = "crawl.json";
    }
}
