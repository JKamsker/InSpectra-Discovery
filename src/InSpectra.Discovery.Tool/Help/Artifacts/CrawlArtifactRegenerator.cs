namespace InSpectra.Discovery.Tool.Help.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using InSpectra.Discovery.Tool.Help.Crawling;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.Help.Parsing;

using InSpectra.Discovery.Tool.Help.OpenCli;

using InSpectra.Discovery.Tool.Help.Documents;

using InSpectra.Discovery.Tool.OpenCli.Artifacts;

using System.Text.Json.Nodes;

internal sealed class CrawlArtifactRegenerator
{
    private readonly OpenCliBuilder _openCliBuilder = new();
    private readonly TextParser _parser = new();

    public HelpCrawlArtifactRegenerationResult RegenerateRepository(
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

        return new HelpCrawlArtifactRegenerationResult(
            result.ScannedCount,
            result.CandidateCount,
            result.RewrittenCount,
            result.UnchangedCount,
            result.FailedCount,
            result.RewrittenItems,
            result.FailedItems);
    }

    private JsonObject RegenerateOpenCli(HelpCrawlArtifactCandidate candidate)
    {
        var crawl = JsonNodeFileLoader.TryLoadJsonObject(candidate.CrawlPath)
            ?? throw new InvalidOperationException($"Crawl artifact '{candidate.CrawlPath}' is empty.");
        var parsedCaptures = ParseCaptures(candidate.CommandName, crawl["commands"] as JsonArray);
        if (!parsedCaptures.TryGetValue(string.Empty, out _))
        {
            parsedCaptures[string.Empty] = CreateEmptyRootDocument();
        }

        var helpDocuments = ReachableDocumentSupport.BuildReachableDocuments(candidate.CommandName, parsedCaptures);
        if (helpDocuments.Count == 0)
        {
            helpDocuments[string.Empty] = CreateEmptyRootDocument();
        }

        var openCli = _openCliBuilder.Build(candidate.CommandName, candidate.Version, helpDocuments);
        if (!string.IsNullOrWhiteSpace(candidate.CliFramework))
        {
            openCli["x-inspectra"]!.AsObject()["cliFramework"] = candidate.CliFramework;
        }

        return openCli;
    }

    private static Document CreateEmptyRootDocument()
        => new(
            Title: null,
            Version: null,
            ApplicationDescription: null,
            CommandDescription: null,
            UsageLines: [],
            Arguments: [],
            Options: [],
            Commands: []);

    private Dictionary<string, Document> ParseCaptures(string rootCommandName, JsonArray? captures)
    {
        var documents = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);
        foreach (var capture in captures?.OfType<JsonObject>() ?? [])
        {
            var selected = CapturePayloadSupport.SelectBestDocument(_parser, rootCommandName, capture);
            if (selected is null)
            {
                continue;
            }

            if (!documents.TryGetValue(selected.CommandKey, out var existing)
                || DocumentInspector.Score(selected.Document) > DocumentInspector.Score(existing))
            {
                documents[selected.CommandKey] = selected.Document;
            }
        }

        return documents;
    }

    private static HelpCrawlArtifactCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
        => CrawlArtifactCandidateFactory.TryCreate(repositoryRoot, metadataPath);

    private bool ProcessCandidate(string repositoryRoot, HelpCrawlArtifactCandidate candidate)
    {
        var regenerated = RegenerateOpenCli(candidate);
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
            "crawled-from-help",
            crawlPath: candidate.CrawlPath);
        var stateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
        return openCliChanged || metadataChanged || stateChanged;
    }
}

