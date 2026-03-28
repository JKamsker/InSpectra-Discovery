using System.Text.Json.Nodes;

internal sealed class ToolHelpCrawlArtifactRegenerator
{
    private readonly ToolHelpOpenCliBuilder _openCliBuilder = new();
    private readonly ToolHelpTextParser _parser = new();

    public HelpCrawlArtifactRegenerationResult RegenerateRepository(string repositoryRoot)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var packagesRoot = Path.Combine(root, "index", "packages");
        if (!Directory.Exists(packagesRoot))
        {
            return new HelpCrawlArtifactRegenerationResult(0, 0, 0, 0, 0, [], []);
        }

        var candidates = EnumerateCandidates(packagesRoot).ToList();
        var rewritten = new List<string>();
        var failed = new List<string>();
        var unchangedCount = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                var regenerated = RegenerateOpenCli(candidate);
                var existing = JsonNode.Parse(File.ReadAllText(candidate.OpenCliPath));
                var openCliChanged = !JsonNode.DeepEquals(existing, regenerated);
                if (openCliChanged)
                {
                    RepositoryPathResolver.WriteJsonFile(candidate.OpenCliPath, regenerated);
                }

                var metadataChanged = OpenCliArtifactMetadataRepair.SyncMetadata(
                    root,
                    candidate.MetadataPath,
                    candidate.OpenCliPath,
                    "crawled-from-help",
                    crawlPath: candidate.CrawlPath);
                if (!openCliChanged && !metadataChanged)
                {
                    unchangedCount++;
                    continue;
                }

                rewritten.Add(candidate.DisplayName);
            }
            catch (Exception ex)
            {
                failed.Add($"{candidate.DisplayName}: {ex.Message}");
            }
        }

        return new HelpCrawlArtifactRegenerationResult(
            ScannedCount: Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
                .Count(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase)),
            CandidateCount: candidates.Count,
            RewrittenCount: rewritten.Count,
            UnchangedCount: unchangedCount,
            FailedCount: failed.Count,
            RewrittenItems: rewritten,
            FailedItems: failed);
    }

    private JsonObject RegenerateOpenCli(HelpCrawlArtifactCandidate candidate)
    {
        var crawl = JsonNode.Parse(File.ReadAllText(candidate.CrawlPath))?.AsObject()
            ?? throw new InvalidOperationException($"Crawl artifact '{candidate.CrawlPath}' is empty.");
        var parsedCaptures = ParseCaptures(crawl["commands"] as JsonArray);
        if (!parsedCaptures.TryGetValue(string.Empty, out _))
        {
            parsedCaptures[string.Empty] = CreateEmptyRootDocument();
        }

        var helpDocuments = BuildReachableDocuments(parsedCaptures);
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

    private Dictionary<string, ToolHelpDocument> ParseCaptures(JsonArray? captures)
    {
        var documents = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var capture in captures?.OfType<JsonObject>() ?? [])
        {
            var payload = capture["payload"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            var commandKey = capture["command"]?.GetValue<string>() ?? string.Empty;
            var commandSegments = SplitCommandSegments(commandKey);
            var document = _parser.Parse(payload);
            if (!document.HasContent || !ToolHelpDocumentInspector.IsCompatible(commandSegments, document))
            {
                continue;
            }

            if (!documents.TryGetValue(commandKey, out var existing) || ToolHelpDocumentInspector.Score(document) > ToolHelpDocumentInspector.Score(existing))
            {
                documents[commandKey] = document;
            }
        }

        return documents;
    }

    private static Dictionary<string, ToolHelpDocument> BuildReachableDocuments(
        IReadOnlyDictionary<string, ToolHelpDocument> parsedCaptures)
    {
        var documents = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase);
        if (!parsedCaptures.TryGetValue(string.Empty, out var rootDocument))
        {
            return documents;
        }

        var queue = new Queue<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
        documents[string.Empty] = rootDocument;
        queue.Enqueue(string.Empty);

        while (queue.Count > 0)
        {
            var commandKey = queue.Dequeue();
            var current = documents[commandKey];
            foreach (var child in current.Commands)
            {
                var childKey = string.IsNullOrWhiteSpace(commandKey) ? child.Key : $"{commandKey} {child.Key}";
                if (!seen.Add(childKey) || !parsedCaptures.TryGetValue(childKey, out var childDocument))
                {
                    continue;
                }

                documents[childKey] = childDocument;
                queue.Enqueue(childKey);
            }
        }

        return documents;
    }

    private static IEnumerable<HelpCrawlArtifactCandidate> EnumerateCandidates(string packagesRoot)
        => Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(TryCreateCandidate)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!);

    private static HelpCrawlArtifactCandidate? TryCreateCandidate(string metadataPath)
    {
        var versionDirectory = Path.GetDirectoryName(metadataPath);
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            return null;
        }

        var crawlPath = Path.Combine(versionDirectory, "crawl.json");
        var openCliPath = Path.Combine(versionDirectory, "opencli.json");
        if (!File.Exists(crawlPath) || !File.Exists(openCliPath))
        {
            return null;
        }

        var openCli = JsonNode.Parse(File.ReadAllText(openCliPath));
        var artifactSource = openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>();
        if (!string.Equals(artifactSource, "crawled-from-help", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject();
        var packageId = metadata?["packageId"]?.GetValue<string>();
        var version = metadata?["version"]?.GetValue<string>();
        var commandName = metadata?["command"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        return new HelpCrawlArtifactCandidate(
            packageId,
            version,
            commandName,
            metadata?["cliFramework"]?.GetValue<string>(),
            metadataPath,
            crawlPath,
            openCliPath);
    }

    private static string[] SplitCommandSegments(string commandKey)
        => string.IsNullOrWhiteSpace(commandKey)
            ? []
            : commandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
