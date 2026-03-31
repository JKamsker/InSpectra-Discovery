namespace InSpectra.Discovery.Tool.Help.Artifacts;

using InSpectra.Discovery.Tool.Frameworks;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using System.Text.Json.Nodes;

internal static class CrawlArtifactCandidateFactory
{
    public static HelpCrawlArtifactCandidate? TryCreate(string repositoryRoot, string metadataPath)
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

        var metadata = JsonNodeFileLoader.TryLoadJsonObject(metadataPath);
        var openCliPath = ResolveOpenCliPath(repositoryRoot, versionDirectory, metadata);
        var openCli = JsonNodeFileLoader.TryLoadJsonObject(openCliPath);
        var artifactSource = ResolveArtifactSource(metadata, openCli);

        if (!ShouldRegenerate(metadata, artifactSource))
        {
            return null;
        }

        var cliFramework = metadata?["cliFramework"]?.GetValue<string>()
            ?? openCli?["x-inspectra"]?["cliFramework"]?.GetValue<string>();
        if (CliFrameworkProviderRegistry.HasCliFxAnalysisSupport(cliFramework))
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

    private static bool ShouldRegenerate(JsonObject? metadata, string? artifactSource)
    {
        var openCliClassification = metadata?["steps"]?["opencli"]?["classification"]?.GetValue<string>();
        var analysisMode = metadata?["analysisMode"]?.GetValue<string>();
        var recoverRejectedHelpArtifact =
            string.Equals(openCliClassification, "invalid-opencli-artifact", StringComparison.OrdinalIgnoreCase)
            && string.Equals(analysisMode, "help", StringComparison.OrdinalIgnoreCase);
        return string.Equals(artifactSource, "crawled-from-help", StringComparison.OrdinalIgnoreCase)
            || recoverRejectedHelpArtifact;
    }
}

