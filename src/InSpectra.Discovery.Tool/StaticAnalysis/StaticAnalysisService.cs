using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class StaticAnalysisService
{
    private readonly StaticAnalysisToolRuntime _runtime = new();
    private readonly DnlibAssemblyScanner _assemblyScanner = new();
    private readonly StaticAnalysisOpenCliBuilder _openCliBuilder = new();
    private readonly StaticAnalysisCoverageClassifier _coverageClassifier = new();

    private static readonly Dictionary<string, IStaticAttributeReader> AttributeReaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CommandLineParser"] = new CmdParserAttributeReader(),
        ["System.CommandLine"] = new SystemCommandLineAttributeReader(),
        ["McMaster.Extensions.CommandLineUtils"] = new McMasterAttributeReader(),
        ["Microsoft.Extensions.CommandLineUtils"] = new McMasterAttributeReader(),
        ["Cocona"] = new CoconaAttributeReader(),
        ["PowerArgs"] = new PowerArgsAttributeReader(),
        ["CommandDotNet"] = new CommandDotNetAttributeReader(),
        ["Argu"] = new ArguAttributeReader(),
    };

    private static readonly Dictionary<string, string> AssemblyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CommandLineParser"] = "CommandLine",
        ["System.CommandLine"] = "System.CommandLine",
        ["McMaster.Extensions.CommandLineUtils"] = "McMaster.Extensions.CommandLineUtils",
        ["Microsoft.Extensions.CommandLineUtils"] = "Microsoft.Extensions.CommandLineUtils",
        ["Cocona"] = "Cocona",
        ["PowerArgs"] = "PowerArgs",
        ["CommandDotNet"] = "CommandDotNet",
        ["Argu"] = "Argu",
        ["DocoptNet"] = "DocoptNet",
        ["ConsoleAppFramework"] = "ConsoleAppFramework",
        ["Oakton"] = "Oakton",
        ["ManyConsole"] = "ManyConsole",
        ["Mono.Options"] = "Mono.Options",
        ["NDesk.Options"] = "NDesk.Options",
        ["Mono.Options / NDesk.Options"] = "Mono.Options",
    };

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
            using var scope = ToolRuntime.CreateNuGetApiClientScope();
            var (registrationLeaf, catalogLeaf) = await PackageVersionResolver.ResolveAsync(scope.Client, packageId, version, cancellationToken);
            result["packageUrl"] = $"https://www.nuget.org/packages/{packageId}/{version}";
            result["projectUrl"] = catalogLeaf.ProjectUrl;
            result["sourceRepositoryUrl"] = PackageVersionResolver.NormalizeRepositoryUrl(catalogLeaf.Repository?.Url);
            result["registrationLeafUrl"] = registrationLeaf.Id;
            result["catalogEntryUrl"] = registrationLeaf.CatalogEntryUrl;
            result["packageContentUrl"] = registrationLeaf.PackageContent;
            result["publishedAt"] = registrationLeaf.Published?.ToUniversalTime().ToString("O");
            result["nugetTitle"] = catalogLeaf.Title;
            result["nugetDescription"] = catalogLeaf.Description;

            var packageInspection = await new PackageArchiveInspector(scope.Client).InspectAsync(registrationLeaf.PackageContent, cancellationToken);
            var resolvedCommandName = string.IsNullOrWhiteSpace(commandName) ? packageInspection.ToolCommandNames.FirstOrDefault() : commandName;
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
                        resolvedCliFramework,
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

        var inspection = InspectAssemblies(installDirectory, cliFramework);
        var staticCommands = inspection.Commands;

        if (inspection.InspectionOutcome is "framework-not-found" or "no-reader")
        {
            result["inspectionOutcome"] = inspection.InspectionOutcome;
            result["cliFramework"] = null;
            NonSpectreAnalysisResultSupport.ApplyTerminalFailure(
                result,
                phase: "static-analysis",
                classification: "custom-parser",
                $"Claimed framework '{inspection.ClaimedFramework}' was not found in any assembly. Tool likely uses a custom argument parser.");
            return;
        }

        if (inspection.InspectionOutcome is "no-attributes")
        {
            result["inspectionOutcome"] = inspection.InspectionOutcome;
            NonSpectreAnalysisResultSupport.ApplyTerminalFailure(
                result,
                phase: "static-analysis",
                classification: "custom-parser-no-attributes",
                $"Framework '{inspection.ClaimedFramework}' assembly found in {inspection.ScannedModuleCount} module(s) but no recognizable attributes detected. Tool may use fluent API or non-standard configuration.");
            return;
        }

        var crawler = new ToolHelpCrawler(_runtime);
        var crawl = await crawler.CrawlAsync(commandPath, tempRoot, environment.Values, commandTimeoutSeconds, cancellationToken);
        crawlStopwatch.Stop();

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

    private AssemblyInspectionResult InspectAssemblies(string installDirectory, string cliFramework)
    {
        var assemblyName = ResolveAssemblyName(cliFramework);
        if (assemblyName is null)
        {
            return AssemblyInspectionResult.NoReader(cliFramework);
        }

        var modules = _assemblyScanner.ScanForFramework(installDirectory, assemblyName);
        if (modules.Count == 0)
        {
            return AssemblyInspectionResult.FrameworkNotFound(cliFramework);
        }

        try
        {
            var reader = ResolveAttributeReader(cliFramework);
            if (reader is null)
            {
                return AssemblyInspectionResult.NoReader(cliFramework);
            }

            var commands = new Dictionary<string, StaticCommandDefinition>(reader.Read(modules), StringComparer.OrdinalIgnoreCase);
            if (commands.Count == 0)
            {
                return AssemblyInspectionResult.NoAttributes(cliFramework, modules.Count);
            }

            return AssemblyInspectionResult.Ok(cliFramework, modules.Count, commands);
        }
        finally
        {
            foreach (var module in modules)
            {
                module.Dispose();
            }
        }
    }

    private sealed record AssemblyInspectionResult(
        string InspectionOutcome,
        string? ClaimedFramework,
        int ScannedModuleCount,
        Dictionary<string, StaticCommandDefinition> Commands)
    {
        public static AssemblyInspectionResult Ok(string framework, int moduleCount, Dictionary<string, StaticCommandDefinition> commands)
            => new("ok", framework, moduleCount, commands);

        public static AssemblyInspectionResult FrameworkNotFound(string claimedFramework)
            => new("framework-not-found", claimedFramework, 0, new(StringComparer.OrdinalIgnoreCase));

        public static AssemblyInspectionResult NoAttributes(string framework, int moduleCount)
            => new("no-attributes", framework, moduleCount, new(StringComparer.OrdinalIgnoreCase));

        public static AssemblyInspectionResult NoReader(string framework)
            => new("no-reader", framework, 0, new(StringComparer.OrdinalIgnoreCase));
    }

    private static IStaticAttributeReader? ResolveAttributeReader(string cliFramework)
    {
        foreach (var part in cliFramework.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (AttributeReaders.TryGetValue(part, out var reader))
            {
                return reader;
            }
        }

        return null;
    }

    private static string? ResolveAssemblyName(string cliFramework)
    {
        foreach (var part in cliFramework.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (AssemblyNames.TryGetValue(part, out var name))
            {
                return name;
            }
        }

        return null;
    }

    private static string ResolveFrameworkName(string cliFramework)
    {
        foreach (var part in cliFramework.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (AssemblyNames.ContainsKey(part))
            {
                return part;
            }
        }

        return cliFramework;
    }

    private static void WriteCrawlArtifact(string outputDirectory, JsonObject result, JsonObject crawlArtifact)
    {
        RepositoryPathResolver.WriteJsonFile(Path.Combine(outputDirectory, "crawl.json"), crawlArtifact);
        result["artifacts"]!.AsObject()["crawlArtifact"] = "crawl.json";
    }
}
