namespace InSpectra.Discovery.Tool.OpenCli.Artifacts;

using InSpectra.Discovery.Tool.Indexing;

using System.Text.Json.Nodes;

internal static class LatestArtifactRefreshSupport
{
    private static readonly string[] ArtifactNames = ["metadata.json", "opencli.json", "xmldoc.xml", "crawl.json"];

    public static bool SyncLatestDirectoryForVersion(string repositoryRoot, string metadataPath)
    {
        var versionDirectory = Path.GetDirectoryName(metadataPath);
        if (string.IsNullOrWhiteSpace(versionDirectory)
            || string.Equals(Path.GetFileName(versionDirectory), "latest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var packageDirectory = Path.GetDirectoryName(versionDirectory);
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return false;
        }

        var latestDirectory = Path.Combine(packageDirectory, "latest");
        var latestMetadataPath = Path.Combine(latestDirectory, "metadata.json");
        if (!File.Exists(latestMetadataPath))
        {
            return false;
        }

        var currentMetadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject();
        var latestMetadata = JsonNode.Parse(File.ReadAllText(latestMetadataPath))?.AsObject();
        var currentVersion = currentMetadata?["version"]?.GetValue<string>();
        var latestVersion = latestMetadata?["version"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(currentVersion)
            || !string.Equals(currentVersion, latestVersion, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var before = CaptureState(latestDirectory);
        RepositoryLatestArtifactSupport.SyncLatestDirectory(repositoryRoot, versionDirectory, latestDirectory);
        var after = CaptureState(latestDirectory);
        return !string.Equals(before, after, StringComparison.Ordinal);
    }

    private static string CaptureState(string latestDirectory)
        => string.Join(
            "|",
            ArtifactNames.Select(artifactName =>
            {
                var path = Path.Combine(latestDirectory, artifactName);
                if (!File.Exists(path))
                {
                    return $"{artifactName}:missing";
                }

                var info = new FileInfo(path);
                return $"{artifactName}:{info.Length}:{File.ReadAllText(path)}";
            }));
}
