namespace InSpectra.Discovery.Tool.Queue;

using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed class CurrentAnalysisBackfillQueueBuilder
{
    public async Task<CurrentAnalysisBackfillQueueComputation> RunAsync(
        string repositoryRoot,
        string currentSnapshotPath,
        int? take,
        CancellationToken cancellationToken)
    {
        var root = RepositoryPathResolver.ResolveRepositoryRoot(repositoryRoot);
        var snapshotPath = Path.GetFullPath(Path.Combine(root, currentSnapshotPath));
        await using var snapshotStream = File.OpenRead(snapshotPath);
        var snapshot = await JsonSerializer.DeserializeAsync<DotnetToolIndexSnapshot>(snapshotStream, JsonOptions.Default, cancellationToken)
            ?? throw new InvalidOperationException($"Current snapshot '{snapshotPath}' is empty.");

        var currentPackages = snapshot.Packages
            .OrderByDescending(package => package.TotalDownloads)
            .ThenBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase);
        var queueItems = new List<DotnetToolQueueItem>();
        var reasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var eligibleCount = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var package in currentPackages)
        {
            var statePath = Path.Combine(
                root,
                "state",
                "packages",
                package.PackageId.ToLowerInvariant(),
                $"{package.LatestVersion.ToLowerInvariant()}.json");
            var state = File.Exists(statePath)
                ? JsonNode.Parse(await File.ReadAllTextAsync(statePath, cancellationToken))?.AsObject()
                : null;
            var reason = QueueCommandSupport.GetCurrentBackfillReason(state, now);
            if (string.IsNullOrWhiteSpace(reason))
            {
                continue;
            }

            eligibleCount++;
            reasonCounts[reason] = reasonCounts.GetValueOrDefault(reason) + 1;
            if (take is not null && queueItems.Count >= take.Value)
            {
                continue;
            }

            queueItems.Add(new DotnetToolQueueItem(
                package.PackageId,
                package.LatestVersion,
                reason,
                package.TotalDownloads,
                package.PackageUrl,
                package.PackageContentUrl,
                package.RegistrationUrl,
                package.CatalogEntryUrl));
        }

        var relativeSnapshotPath = RepositoryPathResolver.GetRelativePath(root, snapshotPath);
        var queue = new DotnetToolQueueSnapshot(
            DateTimeOffset.UtcNow,
            "current-analysis-backfill",
            relativeSnapshotPath,
            snapshot.GeneratedAtUtc,
            snapshot.GeneratedAtUtc,
            snapshot.GeneratedAtUtc,
            relativeSnapshotPath,
            queueItems.Count,
            queueItems);

        return new CurrentAnalysisBackfillQueueComputation(
            snapshot.PackageCount,
            eligibleCount,
            reasonCounts.GetValueOrDefault("missing-current-analysis"),
            reasonCounts.GetValueOrDefault("legacy-terminal-negative-reanalysis"),
            reasonCounts.GetValueOrDefault("legacy-terminal-failure-reanalysis"),
            reasonCounts.GetValueOrDefault("retryable-current-reanalysis"),
            queue);
    }
}

