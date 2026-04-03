namespace InSpectra.Discovery.Tool.Indexing;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using InSpectra.Discovery.Tool.Docs.Indexing;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.Infrastructure.Paths;


using System.Text.Json.Nodes;

internal static class RepositoryPackageIndexBuilder
{
    public static RepositoryPackageIndexBuildResult Rebuild(string repositoryRoot, bool writeBrowserIndex)
    {
        var indexRoot = Path.Combine(repositoryRoot, "index");
        var packagesRoot = Path.Combine(indexRoot, "packages");
        Directory.CreateDirectory(indexRoot);
        Directory.CreateDirectory(packagesRoot);

        var versionRecordLookup = LoadVersionRecordLookup(packagesRoot);
        var versionRecordCount = versionRecordLookup.Sum(entry => entry.Value.Count);
        var currentSnapshotLookup = RepositoryPackageSummaryBuilder.LoadCurrentPackageSnapshotLookup(repositoryRoot);
        var packageSummaries = BuildPackageSummaries(repositoryRoot, packagesRoot, versionRecordLookup, currentSnapshotLookup);

        var now = DateTimeOffset.UtcNow;
        var allIndexPath = Path.Combine(indexRoot, "all.json");
        var allIndex = BuildAllIndex(allIndexPath, packageSummaries, now);
        RepositoryPathResolver.WriteJsonFile(allIndexPath, allIndex);

        string? browserIndexPath = null;
        string? browserMinIndexPath = null;
        if (writeBrowserIndex)
        {
            browserIndexPath = Path.Combine(indexRoot, "index.json");
            browserMinIndexPath = Path.Combine(indexRoot, "index.min.json");
            var browserIndex = DocsBrowserIndexSupport.BuildBrowserIndex(
                allIndex,
                browserIndexPath,
                CancellationToken.None,
                now);
            var minBrowserIndex = DocsBrowserIndexSupport.BuildMinBrowserIndex(browserIndex);

            RepositoryPathResolver.WriteJsonFile(
                browserIndexPath,
                DocsBrowserIndexSupport.StabilizeVolatileTimestamps(browserIndexPath, browserIndex));
            RepositoryPathResolver.WriteJsonFile(
                browserMinIndexPath,
                DocsBrowserIndexSupport.StabilizeVolatileTimestamps(browserMinIndexPath, minBrowserIndex));
        }

        return new RepositoryPackageIndexBuildResult(packageSummaries.Count, versionRecordCount, allIndexPath, browserIndexPath, browserMinIndexPath);
    }

    public static string? ToIsoTimestamp(JsonNode? value)
        => RepositoryPackageIndexTimestampSupport.ToIsoTimestamp(value);

    public static PackageEntryTimestamps ResolvePackageTimestamps(JsonObject package)
        => RepositoryPackageIndexTimestampSupport.ResolvePackageTimestamps(package);

    private static IReadOnlyDictionary<string, List<PackageRecord>> LoadVersionRecordLookup(string packagesRoot)
    {
        var lookup = new Dictionary<string, List<PackageRecord>>(StringComparer.OrdinalIgnoreCase);

        foreach (var metadataPath in Directory.EnumerateFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories))
        {
            if (IsLatestMetadataPath(metadataPath))
            {
                continue;
            }

            var metadata = JsonNode.Parse(File.ReadAllText(metadataPath))?.AsObject();
            var packageId = metadata?["packageId"]?.GetValue<string>();
            if (metadata is null || string.IsNullOrWhiteSpace(packageId))
            {
                continue;
            }

            if (!lookup.TryGetValue(packageId, out var records))
            {
                records = [];
                lookup[packageId] = records;
            }

            records.Add(new PackageRecord(packageId, metadata, Path.GetDirectoryName(metadataPath)!));
        }

        return lookup;
    }

    private static IReadOnlyList<JsonObject> BuildPackageSummaries(
        string repositoryRoot,
        string packagesRoot,
        IReadOnlyDictionary<string, List<PackageRecord>> versionRecordLookup,
        IReadOnlyDictionary<string, CurrentPackageSnapshot> currentSnapshotLookup)
    {
        var unsortedPackageSummaries = new List<JsonObject>();

        foreach (var packageGroup in versionRecordLookup)
        {
            var orderedRecords = packageGroup.Value
                .OrderByDescending(record => RepositoryPackageIndexTimestampSupport.ParseDateTimeOrMin(record.Metadata["publishedAt"]?.GetValue<string>()))
                .ThenByDescending(record => RepositoryPackageIndexTimestampSupport.ParseDateTimeOrMin(record.Metadata["evaluatedAt"]?.GetValue<string>()))
                .ToList();
            var latestRecord = orderedRecords[0];
            var summaryPath = Path.Combine(packagesRoot, latestRecord.LowerId, "index.json");
            var existingSummary = File.Exists(summaryPath)
                ? JsonNode.Parse(File.ReadAllText(summaryPath))?.AsObject()
                : null;
            var summary = RepositoryPackageSummaryBuilder.BuildPackageSummary(
                packageGroup.Key,
                orderedRecords.Select(record => record.Metadata).ToList(),
                currentSnapshotLookup,
                existingSummary);
            var latestPaths = RepositoryLatestArtifactSupport.SyncLatestDirectory(
                repositoryRoot,
                latestRecord.VersionDirectory,
                Path.Combine(packagesRoot, latestRecord.LowerId, "latest"));
            RepositoryLatestArtifactSupport.ApplyLatestPaths(summary, latestPaths);
            RepositoryPathResolver.WriteJsonFile(summaryPath, summary);
            unsortedPackageSummaries.Add(summary);
        }

        return OpenCliMetrics.SortPackageSummariesForAllIndex(unsortedPackageSummaries, repositoryRoot);
    }

    private static JsonObject BuildAllIndex(
        string allIndexPath,
        IReadOnlyList<JsonObject> packageSummaries,
        DateTimeOffset now)
    {
        var allIndexTimestamps = RepositoryPackageIndexTimestampSupport.ResolveDocumentTimestamps(allIndexPath, now);
        var allIndex = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["createdAt"] = allIndexTimestamps.CreatedAt,
            ["updatedAt"] = allIndexTimestamps.UpdatedAt,
            ["generatedAt"] = now.ToString("O"),
            ["packageCount"] = packageSummaries.Count,
            ["packages"] = new JsonArray(packageSummaries.Select(summary => (JsonNode)summary).ToArray()),
        };

        JsonDocumentStabilitySupport.TryPreserveTopLevelProperties(
            allIndex,
            JsonNodeFileLoader.TryLoadJsonObject(allIndexPath),
            "updatedAt",
            "generatedAt");

        return allIndex;
    }

    private static bool IsLatestMetadataPath(string metadataPath)
        => string.Equals(
            Path.GetFileName(Path.GetDirectoryName(metadataPath)),
            "latest",
            StringComparison.OrdinalIgnoreCase);

    private sealed record PackageRecord(string PackageId, JsonObject Metadata, string VersionDirectory)
    {
        public string LowerId { get; } = PackageId.ToLowerInvariant();
    }
}

internal sealed record RepositoryPackageIndexBuildResult(
    int PackageCount,
    int VersionRecordCount,
    string AllIndexPath,
    string? BrowserIndexPath,
    string? BrowserMinIndexPath);
