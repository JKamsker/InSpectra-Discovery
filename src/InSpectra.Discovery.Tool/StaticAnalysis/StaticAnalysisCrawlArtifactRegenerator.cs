using System.Text.Json.Nodes;

internal sealed class StaticAnalysisCrawlArtifactRegenerator
{
    private readonly StaticAnalysisOpenCliBuilder _openCliBuilder = new();
    private readonly ToolHelpTextParser _parser = new();

    public StaticAnalysisCrawlArtifactRegenerationResult RegenerateRepository(string repositoryRoot)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var packagesRoot = Path.Combine(root, "index", "packages");
        if (!Directory.Exists(packagesRoot))
        {
            return new StaticAnalysisCrawlArtifactRegenerationResult(0, 0, 0, 0, 0, [], []);
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
                    "static-analysis",
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

        return new StaticAnalysisCrawlArtifactRegenerationResult(
            ScannedCount: Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
                .Count(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase)),
            CandidateCount: candidates.Count,
            RewrittenCount: rewritten.Count,
            UnchangedCount: unchangedCount,
            FailedCount: failed.Count,
            RewrittenItems: rewritten,
            FailedItems: failed);
    }

    private JsonObject RegenerateOpenCli(StaticAnalysisCrawlArtifactCandidate candidate)
    {
        var crawl = JsonNode.Parse(File.ReadAllText(candidate.CrawlPath))?.AsObject()
            ?? throw new InvalidOperationException($"Crawl artifact '{candidate.CrawlPath}' is empty.");
        var helpDocuments = ParseCaptures(crawl["commands"] as JsonArray);
        var staticCommands = StaticAnalysisCrawlArtifactSupport.DeserializeStaticCommands(crawl["staticCommands"]);

        var framework = ResolveFramework(candidate.CliFramework);
        var openCli = _openCliBuilder.Build(candidate.CommandName, candidate.Version, framework, staticCommands, helpDocuments);
        if (!string.IsNullOrWhiteSpace(candidate.CliFramework))
        {
            openCli["x-inspectra"]!.AsObject()["cliFramework"] = candidate.CliFramework;
        }

        return openCli;
    }

    private Dictionary<string, ToolHelpDocument> ParseCaptures(JsonArray? captures)
    {
        var documents = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var capture in captures?.OfType<JsonObject>() ?? [])
        {
            var payload = ExtractPayload(capture);
            if (string.IsNullOrWhiteSpace(payload))
            {
                continue;
            }

            var commandKey = capture["command"]?.GetValue<string>() ?? string.Empty;
            var document = _parser.Parse(payload);
            if (!document.HasContent)
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
        if (!string.IsNullOrWhiteSpace(payload))
        {
            return payload;
        }

        var processResult = capture["result"] as JsonObject;
        if (processResult is null)
        {
            return null;
        }

        return ToolCommandRuntime.NormalizeConsoleText(processResult["stdout"]?.GetValue<string>())
            ?? ToolCommandRuntime.NormalizeConsoleText(processResult["stderr"]?.GetValue<string>());
    }

    private static string ResolveFramework(string? cliFramework)
    {
        if (cliFramework is not null && cliFramework.Contains("CommandLineParser", StringComparison.OrdinalIgnoreCase))
        {
            return "CommandLineParser";
        }

        return cliFramework ?? "unknown";
    }

    private static IEnumerable<StaticAnalysisCrawlArtifactCandidate> EnumerateCandidates(string repositoryRoot, string packagesRoot)
        => Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => TryCreateCandidate(repositoryRoot, path))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!);

    private static StaticAnalysisCrawlArtifactCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
    {
        var versionDirectory = Path.GetDirectoryName(metadataPath);
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            return null;
        }

        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject();
        var artifactSource = metadata?["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>();
        if (!string.Equals(artifactSource, "static-analysis", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var crawlRelativePath = metadata?["artifacts"]?["crawlPath"]?.GetValue<string>();
        var crawlPath = string.IsNullOrWhiteSpace(crawlRelativePath)
            ? Path.Combine(versionDirectory, "crawl.json")
            : Path.Combine(repositoryRoot, crawlRelativePath);
        if (!File.Exists(crawlPath))
        {
            return null;
        }

        var openCliRelativePath = metadata?["artifacts"]?["opencliPath"]?.GetValue<string>();
        var openCliPath = string.IsNullOrWhiteSpace(openCliRelativePath)
            ? Path.Combine(versionDirectory, "opencli.json")
            : Path.Combine(repositoryRoot, openCliRelativePath);

        var packageId = metadata?["packageId"]?.GetValue<string>();
        var version = metadata?["version"]?.GetValue<string>();
        var commandName = metadata?["command"]?.GetValue<string>();
        var cliFramework = metadata?["cliFramework"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        return new StaticAnalysisCrawlArtifactCandidate(
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
