using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class AnalysisCommandService
{
    public Task<int> RunQuietAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
        => RunCoreAsync(
            packageId,
            version,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            commandTimeoutSeconds,
            json: false,
            suppressOutput: true,
            cancellationToken);

    public Task<int> RunUntrustedAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        CancellationToken cancellationToken)
        => RunCoreAsync(
            packageId,
            version,
            outputRoot,
            batchId,
            attempt,
            source,
            installTimeoutSeconds,
            commandTimeoutSeconds,
            json,
            suppressOutput: false,
            cancellationToken);

    private async Task<int> RunCoreAsync(
        string packageId,
        string version,
        string outputRoot,
        string batchId,
        int attempt,
        string source,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        bool json,
        bool suppressOutput,
        CancellationToken cancellationToken)
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var tempRoot = Path.Combine(Path.GetTempPath(), $"inspectra-untrusted-{packageId.ToLowerInvariant()}-{version.ToLowerInvariant()}-{Guid.NewGuid():N}");
        var outputDirectory = Path.GetFullPath(outputRoot);
        var resultPath = Path.Combine(outputDirectory, "result.json");
        var stopwatch = Stopwatch.StartNew();
        Directory.CreateDirectory(outputDirectory);
        Directory.CreateDirectory(tempRoot);

        var result = AnalysisResultSupport.CreateInitialResult(packageId, version, batchId, attempt, source, generatedAt);

        try
        {
            var environment = AnalysisRuntimeSupport.CreateSandboxEnvironment(tempRoot);
            foreach (var directory in environment.Directories)
            {
                Directory.CreateDirectory(directory);
            }

            using var scope = ToolRuntime.CreateNuGetApiClientScope();
            var (registrationLeaf, catalogLeaf) = await PackageVersionResolver.ResolveAsync(scope.Client, packageId, version, cancellationToken);

            result["registrationLeafUrl"] = registrationLeaf.Id;
            result["catalogEntryUrl"] = registrationLeaf.CatalogEntryUrl;
            result["packageContentUrl"] = registrationLeaf.PackageContent;
            result["publishedAt"] = registrationLeaf.Published?.ToUniversalTime().ToString("O");
            result["projectUrl"] = catalogLeaf.ProjectUrl;
            result["sourceRepositoryUrl"] = PackageVersionResolver.NormalizeRepositoryUrl(catalogLeaf.Repository?.Url);

            var detection = AnalysisResultSupport.BuildDetection(catalogLeaf);
            result["detection"] = detection.ToJsonObject();

            if (!detection.HasSpectreConsoleCli)
            {
                result["disposition"] = "terminal-negative";
                result["retryEligible"] = false;
                result["phase"] = "prefilter";
                result["classification"] = "spectre-cli-missing";
            }
            else
            {
                await AnalyzeInstalledToolAsync(
                    result,
                    scope.Client,
                    packageId,
                    version,
                    outputDirectory,
                    tempRoot,
                    environment.Values,
                    registrationLeaf.PackageContent,
                    installTimeoutSeconds,
                    commandTimeoutSeconds,
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            result["disposition"] = "retryable-failure";
            result["retryEligible"] = true;

            if (string.Equals(result["phase"]?.GetValue<string>(), "bootstrap", StringComparison.Ordinal))
            {
                result["classification"] = "unexpected-exception";
            }

            result["failureMessage"] = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            result["timings"]!.AsObject()["totalMs"] = (int)Math.Round(stopwatch.Elapsed.TotalMilliseconds);

            var disposition = result["disposition"]?.GetValue<string>();
            if (disposition is "retryable-failure" or "terminal-failure")
            {
                result["failureSignature"] = AnalysisResultSupport.GetFailureSignature(
                    result["phase"]?.GetValue<string>() ?? "unknown",
                    result["classification"]?.GetValue<string>() ?? "unknown",
                    result["failureMessage"]?.GetValue<string>());
            }

            RepositoryPathResolver.WriteJsonFile(resultPath, result);
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
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

    private static async Task AnalyzeInstalledToolAsync(
        JsonObject result,
        NuGetApiClient apiClient,
        string packageId,
        string version,
        string outputDirectory,
        string tempRoot,
        IReadOnlyDictionary<string, string> environment,
        string packageContentUrl,
        int installTimeoutSeconds,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var packageInspection = await new PackageArchiveInspector(apiClient).InspectAsync(packageContentUrl, cancellationToken);
        AnalysisResultSupport.MergePackageInspection(result["detection"]!.AsObject(), packageInspection);

        var commandName = packageInspection.ToolCommandNames.FirstOrDefault();
        result["command"] = commandName;
        result["entryPoint"] = packageInspection.ToolEntryPointPaths.FirstOrDefault();
        result["runner"] = null;
        result["toolSettingsPath"] = packageInspection.ToolSettingsPaths.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(commandName))
        {
            result["phase"] = "bootstrap";
            result["classification"] = "tool-command-missing";
            result["failureMessage"] = $"No tool command could be resolved for package '{packageId}' version '{version}'.";
            return;
        }

        var installDirectory = Path.Combine(tempRoot, "tool");
        var installResult = await AnalysisRuntimeSupport.InvokeProcessCaptureAsync(
            "dotnet",
            ["tool", "install", packageId, "--version", version, "--tool-path", installDirectory],
            tempRoot,
            environment,
            installTimeoutSeconds,
            cancellationToken);
        result["steps"]!.AsObject()["install"] = installResult.ToStepMetadata(includeStdout: true);
        result["timings"]!.AsObject()["installMs"] = installResult.DurationMs;

        if (installResult.TimedOut || installResult.ExitCode != 0)
        {
            result["phase"] = "install";
            result["classification"] = installResult.TimedOut ? "install-timeout" : "install-failed";
            result["failureMessage"] = AnalysisRuntimeSupport.GetPreferredMessage(installResult.Stdout, installResult.Stderr);
            return;
        }

        var commandPath = AnalysisRuntimeSupport.ResolveInstalledCommandPath(installDirectory, commandName);
        if (commandPath is null)
        {
            result["phase"] = "install";
            result["classification"] = "installed-command-missing";
            result["failureMessage"] = $"Installed tool command '{commandName}' was not found.";
            return;
        }

        var openCliOutcome = await AnalysisIntrospectionSupport.InvokeIntrospectionCommandAsync(
            commandPath,
            ["cli", "opencli"],
            "json",
            tempRoot,
            environment,
            commandTimeoutSeconds,
            cancellationToken);
        var xmlDocOutcome = await AnalysisIntrospectionSupport.InvokeIntrospectionCommandAsync(
            commandPath,
            ["cli", "xmldoc"],
            "xml",
            tempRoot,
            environment,
            commandTimeoutSeconds,
            cancellationToken);

        AnalysisIntrospectionSupport.ApplyOutputs(result, outputDirectory, openCliOutcome, xmlDocOutcome);
        AnalysisIntrospectionSupport.ApplyClassification(result, openCliOutcome, xmlDocOutcome);
    }
}
