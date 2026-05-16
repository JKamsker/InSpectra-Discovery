namespace InSpectra.Discovery.Tool.Analysis.Untrusted;

using InSpectra.Discovery.Tool.Analysis.Bridge;
using InSpectra.Discovery.Tool.Analysis.NonSpectre;
using InSpectra.Discovery.Tool.Analysis.Output;
using InSpectra.Discovery.Tool.Frameworks;
using InSpectra.Discovery.Tool.Infrastructure.Host;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Lib.Contracts;

using System.Diagnostics;
using System.Text.Json.Nodes;

internal sealed class UntrustedCommandService
{
    private readonly LibAnalysisBridge _bridge;

    public UntrustedCommandService(LibAnalysisBridge bridge)
    {
        _bridge = bridge;
    }

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
        var outputDirectory = Path.GetFullPath(outputRoot);
        var resultPath = Path.Combine(outputDirectory, "result.json");
        var stopwatch = Stopwatch.StartNew();
        Directory.CreateDirectory(outputDirectory);

        var result = ResultSupport.CreateInitialResult(packageId, version, batchId, attempt, source, generatedAt);

        try
        {
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
                var analysisRequest = new NonSpectreInstalledToolAnalysisRequest(
                    Result: result,
                    PackageId: packageId,
                    Version: version,
                    CommandName: string.Empty,
                    CliFramework: "Spectre.Console.Cli",
                    OutputDirectory: outputDirectory,
                    TempRoot: Path.Combine(Path.GetTempPath(), $"inspectra-untrusted-{Guid.NewGuid():N}"),
                    InstallTimeoutSeconds: installTimeoutSeconds,
                    CommandTimeoutSeconds: commandTimeoutSeconds);

                try
                {
                    Directory.CreateDirectory(analysisRequest.TempRoot);
                    await _bridge.AnalyzeAsync(analysisRequest, AnalysisMode.Native, cancellationToken);
                }
                finally
                {
                    if (Directory.Exists(analysisRequest.TempRoot))
                    {
                        try { Directory.Delete(analysisRequest.TempRoot, recursive: true); } catch { }
                    }
                }
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
