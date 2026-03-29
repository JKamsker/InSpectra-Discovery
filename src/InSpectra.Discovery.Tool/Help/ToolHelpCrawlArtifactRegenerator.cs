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

        var candidates = EnumerateCandidates(root, packagesRoot).ToList();
        var rewritten = new List<string>();
        var failed = new List<string>();
        var unchangedCount = 0;

        foreach (var candidate in candidates)
        {
            try
            {
                var regenerated = RegenerateOpenCli(candidate);
                if (!OpenCliDocumentValidator.TryValidateDocument(regenerated, out var validationError))
                {
                    var rejectedMetadataChanged = OpenCliArtifactRejectionSupport.RejectInvalidArtifact(
                        root,
                        candidate.MetadataPath,
                        candidate.OpenCliPath,
                        validationError ?? "Generated OpenCLI artifact is not publishable.",
                        crawlPath: candidate.CrawlPath);
                    var rejectedStateChanged = IndexedStatePathsRepair.SyncFromMetadata(root, candidate.MetadataPath);
                    if (!rejectedMetadataChanged && !rejectedStateChanged)
                    {
                        unchangedCount++;
                        continue;
                    }

                    rewritten.Add(candidate.DisplayName);
                    continue;
                }

                var existing = TryLoadJsonNode(candidate.OpenCliPath);
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
                var stateChanged = IndexedStatePathsRepair.SyncFromMetadata(root, candidate.MetadataPath);
                if (!openCliChanged && !metadataChanged && !stateChanged)
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

        if (candidates.Count > 0)
        {
            RepositoryPackageIndexBuilder.Rebuild(root, writeBrowserIndex: true);
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
        var parsedCaptures = ParseCaptures(candidate.CommandName, crawl["commands"] as JsonArray);
        if (!parsedCaptures.TryGetValue(string.Empty, out _))
        {
            parsedCaptures[string.Empty] = CreateEmptyRootDocument();
        }

        var helpDocuments = BuildReachableDocuments(candidate.CommandName, parsedCaptures);
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
            var storedCommand = capture["command"]?.GetValue<string>() ?? string.Empty;
            var payload = SelectBestPayload(rootCommandName, storedCommand, capture);
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            var document = _parser.Parse(payload);
            var commandSegments = ToolHelpCommandPathSupport.ResolveStoredCaptureSegments(rootCommandName, storedCommand, document);
            if (!document.HasContent || !ToolHelpDocumentInspector.IsCompatible(commandSegments, document))
            {
                continue;
            }

            var commandKey = commandSegments.Length == 0 ? string.Empty : string.Join(' ', commandSegments);

            if (!documents.TryGetValue(commandKey, out var existing) || ToolHelpDocumentInspector.Score(document) > ToolHelpDocumentInspector.Score(existing))
            {
                documents[commandKey] = document;
            }
        }

        return documents;
    }

    private string? SelectBestPayload(string rootCommandName, string storedCommand, JsonObject capture)
    {
        var candidates = SelectPayloadCandidates(capture);
        ToolHelpDocument? bestDocument = null;
        string? bestPayload = null;
        var bestScore = -1;
        var helpInvocation = capture["helpInvocation"]?.GetValue<string>();

        foreach (var payload in candidates)
        {
            var document = _parser.Parse(payload);
            var commandSegments = ToolHelpCommandPathSupport.ResolveStoredCaptureSegments(rootCommandName, storedCommand, document);
            var compatibleDocument = document.HasContent && ToolHelpDocumentInspector.IsCompatible(commandSegments, document)
                ? document
                : null;
            var score = compatibleDocument is not null
                ? ScorePayloadCandidate(storedCommand, compatibleDocument, helpInvocation, payload)
                : 0;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestDocument = compatibleDocument;
            bestPayload = payload;
        }

        return bestDocument is not null ? bestPayload : null;
    }

    private static int ScorePayloadCandidate(string storedCommand, ToolHelpDocument document, string? helpInvocation, string payload)
    {
        var score = ToolHelpDocumentInspector.Score(document) - GetPayloadSelectionPenalty(payload, helpInvocation);
        if (string.IsNullOrWhiteSpace(storedCommand))
        {
            return score;
        }

        var storedCommandLeaf = ToolHelpCommandPathSupport.SplitSegments(storedCommand).LastOrDefault();
        var hasLeafSurface = document.Options.Count > 0 || document.Arguments.Count > 0;
        if (!hasLeafSurface
            && document.Commands.Count > 0
            && (string.Equals(storedCommandLeaf, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(storedCommandLeaf, "version", StringComparison.OrdinalIgnoreCase)))
        {
            return int.MinValue;
        }

        if (hasLeafSurface)
        {
            score += 20;
        }

        if (!hasLeafSurface && document.Commands.Count > 0)
        {
            score -= 10;
        }

        return score;
    }

    private static int GetPayloadSelectionPenalty(string payload, string? helpInvocation)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.IsNullOrWhiteSpace(helpInvocation))
        {
            return 0;
        }

        var lines = payload
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
        var edgeLines = lines
            .Take(4)
            .Concat(lines.TakeLast(4))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return edgeLines.Any(line => string.Equals(line, helpInvocation, StringComparison.OrdinalIgnoreCase))
            ? 50
            : 0;
    }

    private static IReadOnlyList<string> SelectPayloadCandidates(JsonObject capture)
    {
        var payloads = new List<string>();
        var storedPayload = ToolCommandRuntime.NormalizeConsoleText(capture["payload"]?.GetValue<string>());
        if (!string.IsNullOrWhiteSpace(storedPayload))
        {
            payloads.Add(storedPayload);
        }

        if (capture["result"] is JsonObject processResult)
        {
            var stdout = ToolCommandRuntime.NormalizeConsoleText(processResult["stdout"]?.GetValue<string>());
            var stderr = ToolCommandRuntime.NormalizeConsoleText(processResult["stderr"]?.GetValue<string>());

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                payloads.Add(stdout);
            }

            if (!string.IsNullOrWhiteSpace(stderr))
            {
                payloads.Add(stderr);
            }

            if (!string.IsNullOrWhiteSpace(stdout) && !string.IsNullOrWhiteSpace(stderr))
            {
                payloads.Add($"{stdout}\n{stderr}");
                payloads.Add($"{stderr}\n{stdout}");
            }
        }

        return payloads.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static Dictionary<string, ToolHelpDocument> BuildReachableDocuments(
        string rootCommandName,
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
            if (ToolHelpDocumentInspector.IsBuiltinAuxiliaryInventoryEcho(commandKey, current))
            {
                continue;
            }

            foreach (var child in current.Commands)
            {
                var childKey = ToolHelpCommandPathSupport.ResolveChildKey(rootCommandName, commandKey, child.Key);
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

    private static IEnumerable<HelpCrawlArtifactCandidate> EnumerateCandidates(string repositoryRoot, string packagesRoot)
        => Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => TryCreateCandidate(repositoryRoot, path))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!);

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

        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject();
        var openCliRelativePath = metadata?["artifacts"]?["opencliPath"]?.GetValue<string>();
        var openCliPath = string.IsNullOrWhiteSpace(openCliRelativePath)
            ? Path.Combine(versionDirectory, "opencli.json")
            : Path.Combine(repositoryRoot, openCliRelativePath);
        var openCli = TryLoadJsonNode(openCliPath) as JsonObject;
        var artifactSource = openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>()
            ?? metadata?["artifacts"]?["opencliSource"]?.GetValue<string>()
            ?? metadata?["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>();
        var openCliClassification = metadata?["steps"]?["opencli"]?["classification"]?.GetValue<string>();
        var analysisMode = metadata?["analysisMode"]?.GetValue<string>();
        var recoverRejectedHelpArtifact =
            string.Equals(openCliClassification, "invalid-opencli-artifact", StringComparison.OrdinalIgnoreCase)
            && string.Equals(analysisMode, "help", StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(artifactSource, "crawled-from-help", StringComparison.OrdinalIgnoreCase)
            && !recoverRejectedHelpArtifact)
        {
            return null;
        }

        var cliFramework = metadata?["cliFramework"]?.GetValue<string>()
            ?? openCli?["x-inspectra"]?["cliFramework"]?.GetValue<string>();
        if (CliFrameworkSupport.HasCliFx(cliFramework))
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

        return new HelpCrawlArtifactCandidate(
            packageId,
            version,
            commandName,
            cliFramework,
            metadataPath,
            crawlPath,
            openCliPath);
    }
    private static JsonNode? TryLoadJsonNode(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
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
