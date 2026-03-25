internal sealed record SpectreConsoleCliDeltaSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string Filter,
    string InputDeltaPath,
    DateTimeOffset SourceGeneratedAtUtc,
    DateTimeOffset CursorStartUtc,
    DateTimeOffset CursorEndUtc,
    int ScannedChangeCount,
    int PackageCount,
    int QueueCount,
    IReadOnlyList<SpectreConsoleCliDeltaEntry> Packages);

internal sealed record SpectreConsoleCliDeltaEntry(
    string PackageId,
    string BroadChangeKind,
    string SubsetChangeKind,
    string? PreviousVersion,
    string? CurrentVersion,
    SpectreConsoleToolEntry? Previous,
    SpectreConsoleToolEntry? Current);

internal sealed record SpectreConsoleCliQueueSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string Filter,
    string InputDeltaPath,
    DateTimeOffset SourceGeneratedAtUtc,
    DateTimeOffset CursorStartUtc,
    DateTimeOffset CursorEndUtc,
    string SourceCurrentSnapshotPath,
    int ItemCount,
    IReadOnlyList<SpectreConsoleCliQueueItem> Items);

internal sealed record SpectreConsoleCliQueueItem(
    string PackageId,
    string Version,
    string BroadChangeKind,
    string SubsetChangeKind,
    long TotalDownloads,
    string PackageUrl,
    string PackageContentUrl,
    string RegistrationUrl,
    string CatalogEntryUrl,
    SpectreConsoleDetection Detection);

internal sealed record SpectreConsoleCliDeltaQueueComputation(
    SpectreConsoleCliDeltaSnapshot Delta,
    SpectreConsoleCliQueueSnapshot Queue);
