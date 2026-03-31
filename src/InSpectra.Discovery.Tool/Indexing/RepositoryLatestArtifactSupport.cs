namespace InSpectra.Discovery.Tool.Indexing;

using InSpectra.Discovery.Tool.Infrastructure.Paths;


using System.Text.Json.Nodes;

internal static class RepositoryLatestArtifactSupport
{
    private static readonly string[] ArtifactNames = ["metadata.json", "opencli.json", "xmldoc.xml", "crawl.json"];

    public static RepositoryLatestArtifactPaths SyncLatestDirectory(
        string repositoryRoot,
        string versionDirectory,
        string latestDirectory)
    {
        Directory.CreateDirectory(latestDirectory);

        foreach (var artifactName in ArtifactNames)
        {
            var sourcePath = Path.Combine(versionDirectory, artifactName);
            var targetPath = Path.Combine(latestDirectory, artifactName);
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
            }
            else if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }
        }

        var latestPaths = BuildLatestArtifactPaths(repositoryRoot, latestDirectory);
        RebaseLatestMetadataPaths(latestDirectory, latestPaths);
        return latestPaths;
    }

    public static void ApplyLatestPaths(JsonObject summary, RepositoryLatestArtifactPaths latestPaths)
        => summary["latestPaths"] = latestPaths.ToJsonObject();

    private static void RebaseLatestMetadataPaths(
        string latestDirectory,
        RepositoryLatestArtifactPaths latestPaths)
    {
        var metadataFile = Path.Combine(latestDirectory, "metadata.json");
        if (!File.Exists(metadataFile))
        {
            return;
        }

        var metadata = JsonNode.Parse(File.ReadAllText(metadataFile))?.AsObject();
        if (metadata is null)
        {
            return;
        }

        var original = metadata.DeepClone();
        var artifacts = metadata["artifacts"] as JsonObject ?? new JsonObject();
        artifacts["metadataPath"] = latestPaths.MetadataPath;
        artifacts["opencliPath"] = latestPaths.OpenCliPath;
        artifacts["xmldocPath"] = latestPaths.XmldocPath;
        artifacts["crawlPath"] = latestPaths.CrawlPath;
        metadata["artifacts"] = artifacts;

        if (metadata["steps"] is JsonObject steps)
        {
            RebaseStepPath(steps, "opencli", latestPaths.OpenCliPath);
            RebaseStepPath(steps, "xmldoc", latestPaths.XmldocPath);
        }

        if (!JsonNode.DeepEquals(original, metadata))
        {
            RepositoryPathResolver.WriteJsonFile(metadataFile, metadata);
        }
    }

    private static RepositoryLatestArtifactPaths BuildLatestArtifactPaths(string repositoryRoot, string latestDirectory)
    {
        var metadataPath = RepositoryPathResolver.GetRelativePath(repositoryRoot, Path.Combine(latestDirectory, "metadata.json"));
        return new RepositoryLatestArtifactPaths(
            MetadataPath: metadataPath,
            OpenCliPath: ResolveLatestArtifactPath(repositoryRoot, latestDirectory, "opencli.json"),
            CrawlPath: ResolveLatestArtifactPath(repositoryRoot, latestDirectory, "crawl.json"),
            XmldocPath: ResolveLatestArtifactPath(repositoryRoot, latestDirectory, "xmldoc.xml"));
    }

    private static void RebaseStepPath(JsonObject steps, string stepName, string? artifactPath)
    {
        if (steps[stepName] is not JsonObject step)
        {
            return;
        }

        if (artifactPath is null)
        {
            step.Remove("path");
            return;
        }

        step["path"] = artifactPath;
    }

    private static string? ResolveLatestArtifactPath(string repositoryRoot, string latestDirectory, string artifactName)
    {
        var artifactPath = Path.Combine(latestDirectory, artifactName);
        return File.Exists(artifactPath)
            ? RepositoryPathResolver.GetRelativePath(repositoryRoot, artifactPath)
            : null;
    }
}

internal sealed record RepositoryLatestArtifactPaths(
    string MetadataPath,
    string? OpenCliPath,
    string? CrawlPath,
    string? XmldocPath)
{
    public JsonObject ToJsonObject()
        => new()
        {
            ["metadataPath"] = MetadataPath,
            ["opencliPath"] = OpenCliPath,
            ["crawlPath"] = CrawlPath,
            ["xmldocPath"] = XmldocPath,
        };
}

