namespace InSpectra.Discovery.Tool.Queue.Backfill;

using InSpectra.Discovery.Tool.Queue.Planning;

using InSpectra.Discovery.Tool.Catalog.Delta;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.Catalog.Indexing;

using InSpectra.Discovery.Tool.Infrastructure.Paths;

using InSpectra.Discovery.Tool.Queue.Models;

using System.Text.Json;
using System.Text.Json.Nodes;

internal sealed class LegacyTerminalNegativeQueueBuilder
{
    public async Task<LegacyTerminalNegativeQueueComputation> RunAsync(
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

        var rankedPackages = snapshot.Packages
            .OrderByDescending(package => package.TotalDownloads)
            .ThenBy(package => package.PackageId, StringComparer.OrdinalIgnoreCase);
        var queueItems = new List<DotnetToolQueueItem>();
        var eligibleCount = 0;

        foreach (var package in rankedPackages)
        {
            var statePath = Path.Combine(
                root,
                "state",
                "packages",
                package.PackageId.ToLowerInvariant(),
                $"{package.LatestVersion.ToLowerInvariant()}.json");
            if (!File.Exists(statePath))
            {
                continue;
            }

            JsonObject? state;
            try
            {
                state = JsonNode.Parse(await File.ReadAllTextAsync(statePath, cancellationToken))?.AsObject();
            }
            catch
            {
                continue;
            }

            if (!QueueCommandSupport.IsLegacyTerminalNegativeState(state))
            {
                continue;
            }

            eligibleCount++;
            queueItems.Add(new DotnetToolQueueItem(
                package.PackageId,
                package.LatestVersion,
                "legacy-terminal-negative-reanalysis",
                package.TotalDownloads,
                package.PackageUrl,
                package.PackageContentUrl,
                package.RegistrationUrl,
                package.CatalogEntryUrl));

            if (take is not null && queueItems.Count >= take.Value)
            {
                break;
            }
        }

        var relativeSnapshotPath = RepositoryPathResolver.GetRelativePath(root, snapshotPath);
        var queue = new DotnetToolQueueSnapshot(
            DateTimeOffset.UtcNow,
            "legacy-terminal-negative",
            relativeSnapshotPath,
            snapshot.GeneratedAtUtc,
            snapshot.GeneratedAtUtc,
            snapshot.GeneratedAtUtc,
            relativeSnapshotPath,
            queueItems.Count,
            queueItems);

        return new LegacyTerminalNegativeQueueComputation(snapshot.PackageCount, eligibleCount, queue);
    }
}

