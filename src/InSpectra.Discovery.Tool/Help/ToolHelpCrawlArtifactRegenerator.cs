using System.Text.Json.Nodes;

internal sealed class ToolHelpCrawlArtifactRegenerator
{
    private readonly ToolHelpOpenCliBuilder _openCliBuilder = new();
    private readonly ToolHelpTextParser _parser = new();

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

        var helpDocuments = ToolHelpReachableDocumentSupport.BuildReachableDocuments(candidate.CommandName, parsedCaptures);
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

    private static ToolHelpDocument CreateEmptyRootDocument()
        => new(
            Title: null,
            Version: null,
            ApplicationDescription: null,
            CommandDescription: null,
            UsageLines: [],
            Arguments: [],
            Options: [],
            Commands: []);

    private Dictionary<string, ToolHelpDocument> ParseCaptures(string rootCommandName, JsonArray? captures)
    {
        var documents = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var capture in captures?.OfType<JsonObject>() ?? [])
        {
            var selected = ToolHelpCapturePayloadSupport.SelectBestDocument(_parser, rootCommandName, capture);
            if (selected is null)
            {
                continue;
            }

            if (!documents.TryGetValue(selected.CommandKey, out var existing)
                || ToolHelpDocumentInspector.Score(selected.Document) > ToolHelpDocumentInspector.Score(existing))
            {
                documents[selected.CommandKey] = selected.Document;
            }
        }

        return documents;
    }

    private static HelpCrawlArtifactCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
        => ToolHelpCrawlArtifactCandidateFactory.TryCreate(repositoryRoot, metadataPath);

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

internal sealed record HelpCrawlArtifactRegenerationResult(
    int ScannedCount,
    int CandidateCount,
    int RewrittenCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<string> RewrittenItems,
    IReadOnlyList<string> FailedItems);

internal sealed record HelpCrawlArtifactCandidate(
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
