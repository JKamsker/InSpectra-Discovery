namespace InSpectra.Discovery.Tool.Analysis.CliFx.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.Analysis.CliFx.Crawling;

using InSpectra.Discovery.Tool.Analysis.CliFx.OpenCli;

using InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;

using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;

internal sealed class CliFxCrawlArtifactRegenerator
{
    private readonly CliFxOpenCliBuilder _openCliBuilder = new();
    private readonly CliFxHelpTextParser _parser = new();

    public CliFxCrawlArtifactRegenerationResult RegenerateRepository(
        string repositoryRoot,
        ArtifactRegenerationScope? scope = null,
        bool rebuildIndexes = true)
    {
        var result = ArtifactRegenerationRunner.Run(
            repositoryRoot,
            scope,
            rebuildIndexes,
            TryCreateCandidate,
            ProcessCandidate,
            static candidate => candidate.DisplayName);

        return new CliFxCrawlArtifactRegenerationResult(
            result.ScannedCount,
            result.CandidateCount,
            result.RewrittenCount,
            result.UnchangedCount,
            result.FailedCount,
            result.RewrittenItems,
            result.FailedItems);
    }

    private JsonObject RegenerateOpenCli(CliFxCrawlArtifactCandidate candidate)
    {
        var crawl = JsonNodeFileLoader.TryLoadJsonObject(candidate.CrawlPath)
            ?? throw new InvalidOperationException($"Crawl artifact '{candidate.CrawlPath}' is empty.");
        var parsedHelpDocuments = ParseCaptures(crawl["commands"] as JsonArray);
        if (!parsedHelpDocuments.ContainsKey(string.Empty))
        {
            parsedHelpDocuments[string.Empty] = CliFxCrawlReplaySupport.CreateEmptyRootDocument();
        }

        var staticCommands = CliFxCrawlArtifactSupport.DeserializeStaticCommands(crawl["staticCommands"]);
        var helpDocuments = CliFxReachableDocumentSupport.BuildReachableDocuments(parsedHelpDocuments, staticCommands);
        if (helpDocuments.Count == 0)
        {
            helpDocuments[string.Empty] = CliFxCrawlReplaySupport.CreateEmptyRootDocument();
        }

        var openCli = _openCliBuilder.Build(candidate.CommandName, candidate.Version, staticCommands, helpDocuments);
        if (!string.IsNullOrWhiteSpace(candidate.CliFramework))
        {
            openCli["x-inspectra"]!.AsObject()["cliFramework"] = candidate.CliFramework;
        }

        return openCli;
    }

    private Dictionary<string, CliFxHelpDocument> ParseCaptures(JsonArray? captures)
    {
        var documents = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var capture in captures?.OfType<JsonObject>() ?? [])
        {
            var replay = CliFxCrawlReplaySupport.TryReplayCapture(_parser, capture);
            if (replay is null)
            {
                continue;
            }

            documents[replay.CommandKey] = replay.Document;
        }

        return documents;
    }

    private static CliFxCrawlArtifactCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
        => CliFxCrawlArtifactCandidateFactory.TryCreate(repositoryRoot, metadataPath);

    private bool ProcessCandidate(string repositoryRoot, CliFxCrawlArtifactCandidate candidate)
    {
        var regenerated = RegenerateOpenCli(candidate);
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
            "crawled-from-clifx-help",
            crawlPath: candidate.CrawlPath);
        var stateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
        return openCliChanged || metadataChanged || stateChanged;
    }
}

