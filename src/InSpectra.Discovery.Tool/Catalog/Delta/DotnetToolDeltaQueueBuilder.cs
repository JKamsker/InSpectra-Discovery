namespace InSpectra.Discovery.Tool.Catalog.Delta;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using System.Text.Json;

internal sealed class DotnetToolDeltaQueueBuilder
{
    public async Task<DotnetToolDeltaQueueComputation> RunAsync(
        IndexDeltaAllToolsOptions options,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var inputDeltaPath = Path.GetFullPath(options.InputDeltaPath);
        if (!File.Exists(inputDeltaPath))
        {
            throw new FileNotFoundException($"Input delta file was not found: {inputDeltaPath}", inputDeltaPath);
        }

        reportProgress?.Invoke($"Loading delta snapshot from {inputDeltaPath}...");
        await using var inputStream = File.OpenRead(inputDeltaPath);
        var delta = await JsonSerializer.DeserializeAsync<DotnetToolDeltaSnapshot>(inputStream, JsonOptions.Default, cancellationToken)
            ?? throw new InvalidOperationException($"Could not read a dotnet-tool delta snapshot from {inputDeltaPath}.");

        var subsetPackages = delta.Packages
            .Where(change => change.Current is not null)
            .Select(change => new DotnetToolDeltaQueueEntry(
                change.PackageId,
                change.ChangeKind,
                change.PreviousVersion,
                change.CurrentVersion ?? change.Current!.LatestVersion,
                change.Previous,
                change.Current!))
            .OrderByDescending(change => change.Current.TotalDownloads)
            .ThenBy(change => change.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var queueItems = subsetPackages
            .Select(change => new DotnetToolQueueItem(
                change.PackageId,
                change.CurrentVersion,
                change.ChangeKind,
                change.Current.TotalDownloads,
                change.Current.PackageUrl,
                change.Current.PackageContentUrl,
                change.Current.RegistrationUrl,
                change.Current.CatalogEntryUrl))
            .ToArray();
        var generatedAtUtc = DateTimeOffset.UtcNow;

        return new DotnetToolDeltaQueueComputation(
            new DotnetToolDeltaQueueSnapshot(
                generatedAtUtc,
                "all-tools",
                inputDeltaPath,
                delta.GeneratedAtUtc,
                delta.CursorStartUtc,
                delta.CursorEndUtc,
                delta.CurrentSnapshotPath,
                delta.ChangedPackageCount,
                subsetPackages.Length,
                queueItems.Length,
                subsetPackages),
            new DotnetToolQueueSnapshot(
                generatedAtUtc,
                "all-tools",
                inputDeltaPath,
                delta.GeneratedAtUtc,
                delta.CursorStartUtc,
                delta.CursorEndUtc,
                delta.CurrentSnapshotPath,
                queueItems.Length,
                queueItems));
    }
}

