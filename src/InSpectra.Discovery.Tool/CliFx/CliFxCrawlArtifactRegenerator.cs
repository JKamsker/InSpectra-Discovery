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

        var candidates = EnumerateCandidates(packagesRoot).ToList();
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
        var helpDocuments = ParseCaptures(crawl["commands"] as JsonArray);
        if (!helpDocuments.ContainsKey(string.Empty))
        {
            helpDocuments[string.Empty] = CreateEmptyRootDocument();
        }

        var staticCommands = CliFxCrawlArtifactSupport.DeserializeStaticCommands(crawl["staticCommands"]);
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

    private static IEnumerable<CliFxCrawlArtifactCandidate> EnumerateCandidates(string packagesRoot)
        => Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(TryCreateCandidate)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!);

    private static CliFxCrawlArtifactCandidate? TryCreateCandidate(string metadataPath)
    {
        var versionDirectory = Path.GetDirectoryName(metadataPath);
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            return null;
        }

        var crawlPath = Path.Combine(versionDirectory, "crawl.json");
        var openCliPath = Path.Combine(versionDirectory, "opencli.json");
        if (!File.Exists(crawlPath))
        {
            return null;
        }

        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject();
        var openCli = TryLoadJsonNode(openCliPath);
        var artifactSource = openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>()
            ?? metadata?["artifacts"]?["opencliSource"]?.GetValue<string>()
            ?? metadata?["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>();
        var cliFramework = metadata?["cliFramework"]?.GetValue<string>()
            ?? openCli?["x-inspectra"]?["cliFramework"]?.GetValue<string>();
        if (!string.Equals(cliFramework, "CliFx", StringComparison.OrdinalIgnoreCase)
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
            metadata?["cliFramework"]?.GetValue<string>(),
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
