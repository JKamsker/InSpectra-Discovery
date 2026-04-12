namespace InSpectra.Discovery.Tool.Docs.Services;

using InSpectra.Discovery.Tool.Frameworks;
using InSpectra.Discovery.Tool.Infrastructure.Json;
using InSpectra.Discovery.Tool.Infrastructure.Paths;
using InSpectra.Discovery.Tool.Indexing;
using InSpectra.Discovery.Tool.OpenCli.Artifacts;
using InSpectra.Discovery.Tool.OpenCli.Documents;

using InSpectra.Lib.Contracts.Providers;

using System.Text.Json.Nodes;

internal sealed class HelpCrawlArtifactRegenerationService
{
    private readonly ICrawlArtifactRebuilder _rebuilder;

    public HelpCrawlArtifactRegenerationService(ICrawlArtifactRebuilder rebuilder)
    {
        _rebuilder = rebuilder;
    }

    public ArtifactRegenerationRunResult RegenerateRepository(
        string repositoryRoot,
        ArtifactRegenerationScope? scope = null,
        bool rebuildIndexes = true)
        => ArtifactRegenerationRunner.Run(
            repositoryRoot,
            scope,
            rebuildIndexes,
            TryCreateCandidate,
            ProcessCandidate,
            static candidate => candidate.DisplayName);

    private static HelpCrawlArtifactCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
    {
        var versionDirectory = Path.GetDirectoryName(metadataPath);
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            return null;
        }

        var crawlPath = Path.Combine(versionDirectory, "crawl.json");
        if (!File.Exists(crawlPath))
        {
            return null;
        }

        var metadata = JsonNodeFileLoader.TryLoadJsonObject(metadataPath);
        var openCliPath = ResolveOpenCliPath(repositoryRoot, versionDirectory, metadata);
        var openCli = JsonNodeFileLoader.TryLoadJsonObject(openCliPath);
        var artifactSource = ResolveArtifactSource(metadata, openCli);

        if (!ShouldRegenerate(metadata, artifactSource))
        {
            return null;
        }

        var cliFramework = metadata?["cliFramework"]?.GetValue<string>()
            ?? openCli?["x-inspectra"]?["cliFramework"]?.GetValue<string>();
        if (CliFrameworkProviderRegistry.HasCliFxAnalysisSupport(cliFramework))
        {
            return null;
        }

        var packageId = metadata?["packageId"]?.GetValue<string>();
        var version = metadata?["version"]?.GetValue<string>();
        var commandName = metadata?["command"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        return new HelpCrawlArtifactCandidate(packageId, version, commandName, cliFramework, metadataPath, crawlPath, openCliPath);
    }

    private bool ProcessCandidate(string repositoryRoot, HelpCrawlArtifactCandidate candidate)
    {
        var crawl = JsonNodeFileLoader.TryLoadJsonObject(candidate.CrawlPath)
            ?? throw new InvalidOperationException($"Crawl artifact '{candidate.CrawlPath}' is empty.");

        var regenerated = _rebuilder.RebuildOpenCli(crawl, candidate.CommandName, candidate.Version, candidate.CliFramework);
        string? validationError = null;
        if (regenerated is null || !OpenCliDocumentValidator.TryValidateDocument(regenerated, out validationError))
        {
            var rejectedMetadataChanged = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
                repositoryRoot,
                candidate.MetadataPath,
                candidate.OpenCliPath,
                validationError ?? "Generated OpenCLI artifact is not publishable.",
                crawlPath: candidate.CrawlPath);
            var rejectedStateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
            return rejectedMetadataChanged || rejectedStateChanged;
        }

        var existing = JsonNodeFileLoader.TryLoadJsonNode(candidate.OpenCliPath);
        var openCliChanged = !JsonNode.DeepEquals(existing, regenerated);
        if (openCliChanged)
        {
            RepositoryPathResolver.WriteJsonFile(candidate.OpenCliPath, regenerated);
        }

        var metadataChanged = OpenCliArtifactMetadataRepair.SyncMetadata(
            repositoryRoot,
            candidate.MetadataPath,
            candidate.OpenCliPath,
            "crawled-from-help",
            crawlPath: candidate.CrawlPath);
        var stateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
        return openCliChanged || metadataChanged || stateChanged;
    }

    private static string ResolveOpenCliPath(string repositoryRoot, string versionDirectory, JsonObject? metadata)
    {
        var openCliRelativePath = metadata?["artifacts"]?["opencliPath"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(openCliRelativePath)
            ? Path.Combine(versionDirectory, "opencli.json")
            : Path.Combine(repositoryRoot, openCliRelativePath);
    }

    private static string? ResolveArtifactSource(JsonObject? metadata, JsonObject? openCli)
        => openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>()
            ?? metadata?["artifacts"]?["opencliSource"]?.GetValue<string>()
            ?? metadata?["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>();

    private static bool ShouldRegenerate(JsonObject? metadata, string? artifactSource)
    {
        var openCliClassification = metadata?["steps"]?["opencli"]?["classification"]?.GetValue<string>();
        var analysisMode = metadata?["analysisMode"]?.GetValue<string>();
        var recoverRejectedHelpArtifact =
            string.Equals(openCliClassification, "invalid-opencli-artifact", StringComparison.OrdinalIgnoreCase)
            && string.Equals(analysisMode, "help", StringComparison.OrdinalIgnoreCase);
        return string.Equals(artifactSource, "crawled-from-help", StringComparison.OrdinalIgnoreCase)
            || recoverRejectedHelpArtifact;
    }

    private sealed record HelpCrawlArtifactCandidate(
        string PackageId,
        string Version,
        string CommandName,
        string? CliFramework,
        string MetadataPath,
        string CrawlPath,
        string OpenCliPath)
    {
        public string DisplayName => $"{PackageId} {Version}";
    }
}
