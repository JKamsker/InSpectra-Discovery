namespace InSpectra.Discovery.Tool.Analysis.Untrusted;

using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class UntrustedCommandService
{
    private readonly InstalledToolAnalysisSupport _installedToolAnalyzer = new();

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

        var result = ResultSupport.CreateInitialResult(packageId, version, batchId, attempt, source, generatedAt);

        try
        {
            var environment = RuntimeSupport.CreateSandboxEnvironment(tempRoot);
            foreach (var directory in environment.Directories)
            {
                Directory.CreateDirectory(directory);
            }

            using var scope = Runtime.CreateNuGetApiClientScope();
            var (registrationLeaf, catalogLeaf) = await PackageVersionResolver.ResolveAsync(scope.Client, packageId, version, cancellationToken);

            result["registrationLeafUrl"] = registrationLeaf.Id;
            result["catalogEntryUrl"] = registrationLeaf.CatalogEntryUrl;
            result["packageContentUrl"] = registrationLeaf.PackageContent;
            result["publishedAt"] = registrationLeaf.Published?.ToUniversalTime().ToString("O");
            result["projectUrl"] = catalogLeaf.ProjectUrl;
            result["sourceRepositoryUrl"] = PackageVersionResolver.NormalizeRepositoryUrl(catalogLeaf.Repository?.Url);

            var detection = ResultSupport.BuildDetection(catalogLeaf);
            result["detection"] = detection.ToJsonObject();

            if (detection.HasSpectreConsoleCli)
            {
                var classified = CliFrameworkProviderRegistry.Detect(catalogLeaf);
                result["cliFramework"] = string.IsNullOrWhiteSpace(classified) || string.Equals(classified, "Spectre.Console.Cli", StringComparison.Ordinal)
                    ? "Spectre.Console.Cli"
                    : $"Spectre.Console.Cli + {classified}";
            }

            if (!detection.HasSpectreConsoleCli)
            {
                result["disposition"] = "terminal-negative";
                result["retryEligible"] = false;
                result["phase"] = "prefilter";
                result["classification"] = "spectre-cli-missing";
            }
            else
            {
                await _installedToolAnalyzer.AnalyzeAsync(
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
                result["failureSignature"] = ResultSupport.GetFailureSignature(
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

        return await CommandOutputSupport.WriteResultAsync(
            packageId,
            version,
            resultPath,
            result["disposition"]?.GetValue<string>(),
            json,
            cancellationToken);
    }
}


