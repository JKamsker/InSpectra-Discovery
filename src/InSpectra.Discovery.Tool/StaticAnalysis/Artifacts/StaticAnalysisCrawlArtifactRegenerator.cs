namespace InSpectra.Discovery.Tool.StaticAnalysis.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.StaticAnalysis.OpenCli;

using InSpectra.Discovery.Tool.OpenCli.Artifacts;
using InSpectra.Discovery.Tool.OpenCli.Documents;


using System.Text.Json.Nodes;

internal sealed class StaticAnalysisCrawlArtifactRegenerator
{
    private readonly StaticAnalysisCrawlOpenCliSupport _openCliSupport = new();

    public StaticAnalysisCrawlArtifactRegenerationResult RegenerateRepository(
        string repositoryRoot,
        ArtifactRegenerationScope? scope = null,
        bool rebuildIndexes = true)
    {
        var result = ArtifactRegenerationRunner.Run(
            repositoryRoot,
            scope,
            rebuildIndexes,
            StaticAnalysisCrawlArtifactCandidateFactory.TryCreateCandidate,
            ProcessCandidate,
            static candidate => candidate.DisplayName);

        return new StaticAnalysisCrawlArtifactRegenerationResult(
            result.ScannedCount,
            result.CandidateCount,
            result.RewrittenCount,
            result.UnchangedCount,
            result.FailedCount,
            result.RewrittenItems,
            result.FailedItems);
    }

    private bool ProcessCandidate(string repositoryRoot, StaticAnalysisCrawlArtifactCandidate candidate)
    {
        var regenerated = _openCliSupport.RegenerateOpenCli(candidate);
        if (!OpenCliDocumentValidator.TryValidateDocument(regenerated, out var validationError))
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
            "static-analysis",
            crawlPath: candidate.CrawlPath);
        var stateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
        return openCliChanged || metadataChanged || stateChanged;
    }
}

internal sealed record StaticAnalysisCrawlArtifactRegenerationResult(
    int ScannedCount,
    int CandidateCount,
    int RewrittenCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<string> RewrittenItems,
    IReadOnlyList<string> FailedItems);

internal sealed record StaticAnalysisCrawlArtifactCandidate(
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

