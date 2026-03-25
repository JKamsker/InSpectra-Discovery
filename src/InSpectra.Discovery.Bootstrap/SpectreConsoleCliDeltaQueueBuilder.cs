using System.Collections.Concurrent;
using System.Text.Json;

internal sealed class SpectreConsoleCliDeltaQueueBuilder
{
    private readonly SpectreConsoleCatalogInspector _inspector;

    public SpectreConsoleCliDeltaQueueBuilder(NuGetApiClient apiClient)
    {
        _inspector = new SpectreConsoleCatalogInspector(apiClient);
    }

    public async Task<SpectreConsoleCliDeltaQueueComputation> RunAsync(
        IndexDeltaSpectreConsoleCliOptions options,
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

        reportProgress?.Invoke("Inspecting changed packages for Spectre.Console.Cli evidence...");

        var subsetChanges = new ConcurrentBag<SpectreConsoleCliDeltaEntry>();
        var queueItems = new ConcurrentBag<SpectreConsoleCliQueueItem>();
        var completed = 0;

        await Parallel.ForEachAsync(
            delta.Packages,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = options.Concurrency,
            },
            async (change, token) =>
            {
                var previous = await InspectStateAsync(change.Previous, token);
                var current = await InspectStateAsync(change.Current, token);
                var subsetChangeKind = GetSubsetChangeKind(previous, current);

                if (subsetChangeKind is not null)
                {
                    subsetChanges.Add(new SpectreConsoleCliDeltaEntry(
                        PackageId: change.PackageId,
                        BroadChangeKind: change.ChangeKind,
                        SubsetChangeKind: subsetChangeKind,
                        PreviousVersion: change.PreviousVersion,
                        CurrentVersion: change.CurrentVersion,
                        Previous: previous,
                        Current: current));
                }

                if (current?.Detection.HasSpectreConsoleCli == true && subsetChangeKind is not null)
                {
                    queueItems.Add(new SpectreConsoleCliQueueItem(
                        PackageId: current.PackageId,
                        Version: current.LatestVersion,
                        BroadChangeKind: change.ChangeKind,
                        SubsetChangeKind: subsetChangeKind,
                        TotalDownloads: current.TotalDownloads,
                        PackageUrl: current.PackageUrl,
                        PackageContentUrl: current.PackageContentUrl,
                        RegistrationUrl: current.RegistrationUrl,
                        CatalogEntryUrl: current.CatalogEntryUrl,
                        Detection: current.Detection));
                }

                var currentCount = Interlocked.Increment(ref completed);
                if (currentCount == delta.Packages.Count || currentCount % 50 == 0)
                {
                    reportProgress?.Invoke($"  Inspected {currentCount}/{delta.Packages.Count} changed packages.");
                }
            });

        var orderedChanges = subsetChanges
            .OrderByDescending(change => change.Current?.TotalDownloads ?? change.Previous?.TotalDownloads ?? 0L)
            .ThenBy(change => change.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var orderedQueue = queueItems
            .OrderByDescending(item => item.TotalDownloads)
            .ThenBy(item => item.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var generatedAtUtc = DateTimeOffset.UtcNow;
        return new SpectreConsoleCliDeltaQueueComputation(
            new SpectreConsoleCliDeltaSnapshot(
                GeneratedAtUtc: generatedAtUtc,
                Filter: "spectre-console-cli",
                InputDeltaPath: inputDeltaPath,
                SourceGeneratedAtUtc: delta.GeneratedAtUtc,
                CursorStartUtc: delta.CursorStartUtc,
                CursorEndUtc: delta.CursorEndUtc,
                ScannedChangeCount: delta.ChangedPackageCount,
                PackageCount: orderedChanges.Length,
                QueueCount: orderedQueue.Length,
                Packages: orderedChanges),
            new SpectreConsoleCliQueueSnapshot(
                GeneratedAtUtc: generatedAtUtc,
                Filter: "spectre-console-cli",
                InputDeltaPath: inputDeltaPath,
                SourceGeneratedAtUtc: delta.GeneratedAtUtc,
                CursorStartUtc: delta.CursorStartUtc,
                CursorEndUtc: delta.CursorEndUtc,
                SourceCurrentSnapshotPath: delta.CurrentSnapshotPath,
                ItemCount: orderedQueue.Length,
                Items: orderedQueue));
    }

    private async Task<SpectreConsoleToolEntry?> InspectStateAsync(
        DotnetToolIndexEntry? entry,
        CancellationToken cancellationToken)
        => entry is null
            ? null
            : await _inspector.InspectAsync(entry, SpectreConsoleFilterMode.SpectreConsoleCliOnly, cancellationToken);

    private static string? GetSubsetChangeKind(
        SpectreConsoleToolEntry? previous,
        SpectreConsoleToolEntry? current)
    {
        var previousMatches = previous?.Detection.HasSpectreConsoleCli == true;
        var currentMatches = current?.Detection.HasSpectreConsoleCli == true;

        return (previousMatches, currentMatches) switch
        {
            (false, false) => null,
            (false, true) => "entered-subset",
            (true, false) => "left-subset",
            (true, true) => "stayed-in-subset",
        };
    }
}
