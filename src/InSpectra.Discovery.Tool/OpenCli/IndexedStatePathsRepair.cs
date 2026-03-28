using System.Text.Json.Nodes;

internal static class IndexedStatePathsRepair
{
    public static bool SyncFromMetadata(string repositoryRoot, string metadataPath)
    {
        if (!File.Exists(metadataPath))
        {
            return false;
        }

        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject();
        if (metadata is null)
        {
            return false;
        }

        var packageId = metadata["packageId"]?.GetValue<string>();
        var version = metadata["version"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        var statePath = Path.Combine(
            repositoryRoot,
            "state",
            "packages",
            packageId.ToLowerInvariant(),
            $"{version.ToLowerInvariant()}.json");
        if (!File.Exists(statePath))
        {
            return false;
        }

        var state = JsonNode.Parse(File.ReadAllText(statePath))?.AsObject();
        if (state is null)
        {
            return false;
        }

        var original = state.DeepClone();
        state["indexedPaths"] = metadata["artifacts"]?.DeepClone();
        if (JsonNode.DeepEquals(original, state))
        {
            return false;
        }

        RepositoryPathResolver.WriteJsonFile(statePath, state);
        return true;
    }
}
