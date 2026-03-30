using System.Text.Json.Nodes;

internal sealed class StaticAnalysisCrawlArtifactRegenerator
{
    private readonly StaticAnalysisOpenCliBuilder _openCliBuilder = new();
    private readonly ToolHelpTextParser _parser = new();

    public StaticAnalysisCrawlArtifactRegenerationResult RegenerateRepository(
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

        return new StaticAnalysisCrawlArtifactRegenerationResult(
            result.ScannedCount,
            result.CandidateCount,
            result.RewrittenCount,
            result.UnchangedCount,
            result.FailedCount,
            result.RewrittenItems,
            result.FailedItems);
    }

    private JsonObject RegenerateOpenCli(StaticAnalysisCrawlArtifactCandidate candidate)
    {
        var crawl = JsonNode.Parse(File.ReadAllText(candidate.CrawlPath))?.AsObject()
            ?? throw new InvalidOperationException($"Crawl artifact '{candidate.CrawlPath}' is empty.");
        var helpDocuments = ParseCaptures(crawl["commands"] as JsonArray);
        var staticCommands = StaticAnalysisCrawlArtifactSupport.DeserializeStaticCommands(crawl["staticCommands"]);
        var existingOpenCli = JsonNodeFileLoader.TryLoadJsonNode(candidate.OpenCliPath) as JsonObject;

        var framework = ResolveFramework(candidate.CliFramework);
        var openCli = _openCliBuilder.Build(candidate.CommandName, candidate.Version, framework, staticCommands, helpDocuments);
        if (!string.IsNullOrWhiteSpace(candidate.CliFramework))
        {
            openCli["x-inspectra"]!.AsObject()["cliFramework"] = candidate.CliFramework;
        }

        RestoreExistingCliMetadataEnrichment(openCli, existingOpenCli);
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

    private bool ProcessCandidate(string repositoryRoot, StaticAnalysisCrawlArtifactCandidate candidate)
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
            "static-analysis",
            crawlPath: candidate.CrawlPath);
        var stateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
        return openCliChanged || metadataChanged || stateChanged;
    }

    private static void RestoreExistingCliMetadataEnrichment(JsonObject regenerated, JsonObject? existing)
    {
        if (existing is null
            || regenerated["info"] is not JsonObject regeneratedInfo
            || existing["info"] is not JsonObject existingInfo
            || existing["x-inspectra"] is not JsonObject existingInspectra)
        {
            return;
        }

        var regeneratedInspectra = regenerated["x-inspectra"] as JsonObject ?? new JsonObject();
        regenerated["x-inspectra"] = regeneratedInspectra;

        RestoreExistingCliMetadataEnrichment(
            regeneratedInfo,
            regeneratedInspectra,
            existingInfo,
            existingInspectra,
            infoPropertyName: "title",
            cliParsedPropertyName: "cliParsedTitle");
        RestoreExistingCliMetadataEnrichment(
            regeneratedInfo,
            regeneratedInspectra,
            existingInfo,
            existingInspectra,
            infoPropertyName: "description",
            cliParsedPropertyName: "cliParsedDescription");
    }

    private static void RestoreExistingCliMetadataEnrichment(
        JsonObject regeneratedInfo,
        JsonObject regeneratedInspectra,
        JsonObject existingInfo,
        JsonObject existingInspectra,
        string infoPropertyName,
        string cliParsedPropertyName)
    {
        var existingInfoValue = existingInfo[infoPropertyName]?.GetValue<string>();
        var existingCliParsedValue = existingInspectra[cliParsedPropertyName]?.GetValue<string>();
        var regeneratedInfoValue = regeneratedInfo[infoPropertyName]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(regeneratedInfoValue) && !string.IsNullOrWhiteSpace(existingInfoValue))
        {
            regeneratedInfo[infoPropertyName] = existingInfoValue;
            if (!string.IsNullOrWhiteSpace(existingCliParsedValue))
            {
                regeneratedInspectra[cliParsedPropertyName] = existingCliParsedValue;
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(existingInfoValue)
            || string.IsNullOrWhiteSpace(existingCliParsedValue)
            || string.IsNullOrWhiteSpace(regeneratedInfoValue)
            || !string.Equals(regeneratedInfoValue, existingCliParsedValue, StringComparison.Ordinal))
        {
            return;
        }

        regeneratedInfo[infoPropertyName] = existingInfoValue;
        regeneratedInspectra[cliParsedPropertyName] = existingCliParsedValue;
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
