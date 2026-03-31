namespace InSpectra.Discovery.Tool.Analysis.CliFx.Artifacts;

using InSpectra.Discovery.Tool.Frameworks;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using System.Text.Json.Nodes;

internal static class CliFxCrawlArtifactCandidateFactory
{
    public static CliFxCrawlArtifactCandidate? TryCreate(string repositoryRoot, string metadataPath)
    {
        var versionDirectory = Path.GetDirectoryName(metadataPath);
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            return null;
        }

        var metadata = JsonNodeFileLoader.TryLoadJsonObject(metadataPath);
        var crawlPath = ResolveCrawlPath(repositoryRoot, versionDirectory, metadata);
        if (!File.Exists(crawlPath))
        {
            return null;
        }

        var openCliPath = ResolveOpenCliPath(repositoryRoot, versionDirectory, metadata);
        var openCli = JsonNodeFileLoader.TryLoadJsonObject(openCliPath);
        var artifactSource = ResolveArtifactSource(metadata, openCli);
        var cliFramework = metadata?["cliFramework"]?.GetValue<string>()
            ?? openCli?["x-inspectra"]?["cliFramework"]?.GetValue<string>();
        if (!CliFrameworkProviderRegistry.HasCliFxAnalysisSupport(cliFramework)
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

    private static string ResolveCrawlPath(string repositoryRoot, string versionDirectory, JsonObject? metadata)
    {
        var crawlRelativePath = metadata?["artifacts"]?["crawlPath"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(crawlRelativePath)
            ? Path.Combine(versionDirectory, "crawl.json")
            : Path.Combine(repositoryRoot, crawlRelativePath);
    }

    private static string ResolveOpenCliPath(string repositoryRoot, string versionDirectory, JsonObject? metadata)
    {
        var openCliRelativePath = metadata?["artifacts"]?["opencliPath"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(openCliRelativePath)
            ? Path.Combine(versionDirectory, "opencli.json")
            : Path.Combine(repositoryRoot, openCliRelativePath);
    }

    private static string? ResolveArtifactSource(JsonObject? metadata, JsonObject? openCli)
        => openCli?["x-inspectra"]?["artifactSource"]?.GetValue<string>()
            ?? metadata?["artifacts"]?["opencliSource"]?.GetValue<string>()
            ?? metadata?["steps"]?["opencli"]?["artifactSource"]?.GetValue<string>();

    private static bool IsCliFxCrawlArtifactSource(string? artifactSource)
        => string.Equals(artifactSource, "crawled-from-clifx-help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(artifactSource, "crawled-from-help", StringComparison.OrdinalIgnoreCase);
}

