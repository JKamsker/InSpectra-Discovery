namespace InSpectra.Discovery.Tool.OpenCli.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using System.Text.Json.Nodes;

internal sealed class LegacyPartialMetadataReconciler
{
    public LegacyPartialMetadataReconciliationResult RegenerateRepository(
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

        return new LegacyPartialMetadataReconciliationResult(
            result.ScannedCount,
            result.CandidateCount,
            result.RewrittenCount,
            result.UnchangedCount,
            result.FailedCount,
            result.RewrittenItems,
            result.FailedItems);
    }

    private static LegacyPartialMetadataCandidate? TryCreateCandidate(string repositoryRoot, string metadataPath)
    {
        if (JsonNode.Parse(File.ReadAllText(metadataPath)) is not JsonObject metadata)
        {
            return null;
        }

        if (!string.Equals(metadata["status"]?.GetValue<string>(), "partial", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var packageId = metadata["packageId"]?.GetValue<string>();
        var version = metadata["version"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(packageId) || string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var artifacts = metadata["artifacts"] as JsonObject;
        if (HasExistingArtifactPath(repositoryRoot, artifacts, "opencliPath")
            || HasExistingArtifactPath(repositoryRoot, artifacts, "crawlPath")
            || HasExistingArtifactPath(repositoryRoot, artifacts, "xmldocPath"))
        {
            return null;
        }

        if (metadata["steps"]?["opencli"] is not null
            || metadata["introspection"]?["opencli"] is not null)
        {
            return null;
        }

        return new LegacyPartialMetadataCandidate(packageId, version, metadataPath);
    }

    private static bool ProcessCandidate(string repositoryRoot, LegacyPartialMetadataCandidate candidate)
    {
        var metadata = JsonNode.Parse(File.ReadAllText(candidate.MetadataPath))?.AsObject()
            ?? throw new InvalidOperationException($"Metadata artifact '{candidate.MetadataPath}' is empty.");
        var original = metadata.DeepClone();

        var steps = metadata["steps"] as JsonObject ?? new JsonObject();
        steps["opencli"] = CreateFailureStep();
        metadata["steps"] = steps;

        var introspection = metadata["introspection"] as JsonObject ?? new JsonObject();
        introspection["opencli"] = CreateFailureStep();
        metadata["introspection"] = introspection;

        if (JsonNode.DeepEquals(original, metadata))
        {
            return false;
        }

        RepositoryPathResolver.WriteJsonFile(candidate.MetadataPath, metadata);
        var stateChanged = IndexedStatePathsRepair.SyncFromMetadata(repositoryRoot, candidate.MetadataPath);
        return stateChanged || !JsonNode.DeepEquals(original, metadata);
    }

    private static JsonObject CreateFailureStep()
        => new()
        {
            ["status"] = "failed",
            ["classification"] = "introspection-unresolved",
            ["message"] = "The tool did not yield a usable introspection result.",
        };

    private static bool HasExistingArtifactPath(string repositoryRoot, JsonObject? artifacts, string propertyName)
    {
        var relativePath = artifacts?[propertyName]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        return File.Exists(Path.Combine(repositoryRoot, relativePath));
    }
}

internal sealed record LegacyPartialMetadataCandidate(
    string PackageId,
    string Version,
    string MetadataPath)
{
    public string DisplayName => $"{PackageId} {Version}";
}

internal sealed record LegacyPartialMetadataReconciliationResult(
    int ScannedCount,
    int CandidateCount,
    int RewrittenCount,
    int UnchangedCount,
    int FailedCount,
    IReadOnlyList<string> RewrittenItems,
    IReadOnlyList<string> FailedItems);
