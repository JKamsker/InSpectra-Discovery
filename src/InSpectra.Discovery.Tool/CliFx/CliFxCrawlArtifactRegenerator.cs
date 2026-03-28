using System.Text.Json.Nodes;

internal sealed class CliFxCrawlArtifactRegenerator
{
    private readonly CliFxOpenCliBuilder _openCliBuilder = new();
    private readonly CliFxHelpTextParser _parser = new();

    public CliFxCrawlArtifactRegenerationResult RegenerateRepository(string repositoryRoot)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var packagesRoot = Path.Combine(root, "index", "packages");
        if (!Directory.Exists(packagesRoot))
        {
            return new CliFxCrawlArtifactRegenerationResult(0, 0, 0, 0, 0, [], []);
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
                    "crawled-from-clifx-help",
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

        return new CliFxCrawlArtifactRegenerationResult(
            ScannedCount: Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
                .Count(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase)),
            CandidateCount: candidates.Count,
            RewrittenCount: rewritten.Count,
            UnchangedCount: unchangedCount,
            FailedCount: failed.Count,
            RewrittenItems: rewritten,
            FailedItems: failed);
    }

    private JsonObject RegenerateOpenCli(CliFxCrawlArtifactCandidate candidate)
    {
        var crawl = JsonNode.Parse(File.ReadAllText(candidate.CrawlPath))?.AsObject()
            ?? throw new InvalidOperationException($"Crawl artifact '{candidate.CrawlPath}' is empty.");
        var parsedHelpDocuments = ParseCaptures(crawl["commands"] as JsonArray);
        if (!parsedHelpDocuments.ContainsKey(string.Empty))
        {
            parsedHelpDocuments[string.Empty] = CreateEmptyRootDocument();
        }

        var staticCommands = CliFxCrawlArtifactSupport.DeserializeStaticCommands(crawl["staticCommands"]);
        var helpDocuments = BuildReachableDocuments(parsedHelpDocuments, staticCommands);
        if (helpDocuments.Count == 0)
        {
            helpDocuments[string.Empty] = CreateEmptyRootDocument();
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
            var payload = ExtractPayload(capture);
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            var commandKey = capture["command"]?.GetValue<string>() ?? string.Empty;
            var document = _parser.Parse(payload);
            if (!HasContent(document))
            {
                continue;
            }

            documents[commandKey] = document;
        }

        return documents;
    }

    private static Dictionary<string, CliFxHelpDocument> BuildReachableDocuments(
        IReadOnlyDictionary<string, CliFxHelpDocument> parsedHelpDocuments,
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands)
    {
        var documents = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };

        if (parsedHelpDocuments.TryGetValue(string.Empty, out var rootDocument))
        {
            documents[string.Empty] = rootDocument;
        }

        queue.Enqueue(string.Empty);
        while (queue.Count > 0)
        {
            var commandKey = queue.Dequeue();
            foreach (var childKey in EnumerateReachableChildKeys(commandKey, parsedHelpDocuments, staticCommands))
            {
                if (!seen.Add(childKey))
                {
                    continue;
                }

                if (parsedHelpDocuments.TryGetValue(childKey, out var childDocument))
                {
                    documents[childKey] = childDocument;
                }

                queue.Enqueue(childKey);
            }
        }

        return documents;
    }

    private static IEnumerable<string> EnumerateReachableChildKeys(
        string commandKey,
        IReadOnlyDictionary<string, CliFxHelpDocument> parsedHelpDocuments,
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (parsedHelpDocuments.TryGetValue(commandKey, out var document))
        {
            foreach (var child in document.Commands)
            {
                var childKey = string.IsNullOrWhiteSpace(commandKey) ? child.Key : $"{commandKey} {child.Key}";
                if (yielded.Add(childKey))
                {
                    yield return childKey;
                }
            }
        }

        foreach (var childKey in EnumerateStaticChildKeys(commandKey, staticCommands.Keys))
        {
            if (yielded.Add(childKey))
            {
                yield return childKey;
            }
        }
    }

    private static IEnumerable<string> EnumerateStaticChildKeys(string commandKey, IEnumerable<string> staticCommandKeys)
    {
        var parentSegments = string.IsNullOrWhiteSpace(commandKey)
            ? []
            : commandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var prefix = parentSegments.Length == 0 ? string.Empty : $"{commandKey} ";

        foreach (var staticCommandKey in staticCommandKeys.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            string[] segments;
            if (parentSegments.Length == 0)
            {
                segments = staticCommandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (segments.Length == 0)
                {
                    continue;
                }

                yield return segments[0];
                continue;
            }

            if (!staticCommandKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            segments = staticCommandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length <= parentSegments.Length)
            {
                continue;
            }

            yield return string.Join(' ', segments.Take(parentSegments.Length + 1));
        }
    }

    private static string? ExtractPayload(JsonObject capture)
    {
        var payload = ToolCommandRuntime.NormalizeConsoleText(capture["payload"]?.GetValue<string>());
        return !string.IsNullOrWhiteSpace(payload)
            ? payload
            : SelectBestPayload(capture["result"] as JsonObject);
    }

    private static string? SelectBestPayload(JsonObject? processResult)
    {
        if (processResult is null)
        {
            return null;
        }

        var stdout = ToolCommandRuntime.NormalizeConsoleText(processResult["stdout"]?.GetValue<string>());
        var stderr = ToolCommandRuntime.NormalizeConsoleText(processResult["stderr"]?.GetValue<string>());

        if (LooksLikeHelp(stdout))
        {
            return stdout;
        }

        if (LooksLikeHelp(stderr))
        {
            return stderr;
        }

        return stdout ?? stderr;
    }

    private static bool LooksLikeHelp(string? text)
        => !string.IsNullOrWhiteSpace(text)
            && (text.Contains("\nUSAGE\n", StringComparison.Ordinal)
                || text.Contains("\nOPTIONS\n", StringComparison.Ordinal)
                || text.Contains("\nCOMMANDS\n", StringComparison.Ordinal));

    private static bool HasContent(CliFxHelpDocument document)
        => !string.IsNullOrWhiteSpace(document.Title)
            || !string.IsNullOrWhiteSpace(document.Version)
            || !string.IsNullOrWhiteSpace(document.ApplicationDescription)
            || !string.IsNullOrWhiteSpace(document.CommandDescription)
            || document.UsageLines.Count > 0
            || document.Parameters.Count > 0
            || document.Options.Count > 0
            || document.Commands.Count > 0;

    private static CliFxHelpDocument CreateEmptyRootDocument()
        => new(
            Title: null,
            Version: null,
            ApplicationDescription: null,
            CommandDescription: null,
            UsageLines: [],
            Parameters: [],
            Options: [],
            Commands: []);

    private static IEnumerable<CliFxCrawlArtifactCandidate> EnumerateCandidates(string repositoryRoot, string packagesRoot)
        => Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => TryCreateCandidate(repositoryRoot, path))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!);

    private static CliFxCrawlArtifactCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
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
        var openCli = TryLoadJsonNode(openCliPath);
        var artifactSource = openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>()
            ?? metadata?["artifacts"]?["opencliSource"]?.GetValue<string>()
            ?? metadata?["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>();
        var cliFramework = metadata?["cliFramework"]?.GetValue<string>()
            ?? openCli?["x-inspectra"]?["cliFramework"]?.GetValue<string>();
        if (!CliFrameworkSupport.HasCliFx(cliFramework)
            || !IsCliFxCrawlArtifactSource(artifactSource))
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

        return new CliFxCrawlArtifactCandidate(
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

    private static bool IsCliFxCrawlArtifactSource(string? artifactSource)
        => string.Equals(artifactSource, "crawled-from-clifx-help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(artifactSource, "crawled-from-help", StringComparison.OrdinalIgnoreCase);
}

internal sealed record CliFxCrawlArtifactRegenerationResult(
    int ScannedCount,
    int CandidateCount,
    int RewrittenCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<string> RewrittenItems,
    IReadOnlyList<string> FailedItems);

internal sealed record CliFxCrawlArtifactCandidate(
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
