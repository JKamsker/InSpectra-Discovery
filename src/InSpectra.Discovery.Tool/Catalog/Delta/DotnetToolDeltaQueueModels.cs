namespace InSpectra.Discovery.Tool.Catalog.Delta;


internal sealed record DotnetToolDeltaQueueSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string Filter,
    string InputDeltaPath,
    DateTimeOffset SourceGeneratedAtUtc,
    DateTimeOffset CursorStartUtc,
    DateTimeOffset CursorEndUtc,
    string SourceCurrentSnapshotPath,
    int ScannedChangeCount,
    int PackageCount,
    int QueueCount,
    IReadOnlyList<DotnetToolDeltaQueueEntry> Packages);

internal sealed record DotnetToolDeltaQueueEntry(
    string PackageId,
    string ChangeKind,
    string? PreviousVersion,
    string CurrentVersion,
    DotnetToolDeltaState? Previous,
    DotnetToolDeltaState Current);

internal sealed record DotnetToolQueueSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string Filter,
    string InputDeltaPath,
    DateTimeOffset SourceGeneratedAtUtc,
    DateTimeOffset CursorStartUtc,
    DateTimeOffset CursorEndUtc,
    string SourceCurrentSnapshotPath,
    int ItemCount,
    IReadOnlyList<DotnetToolQueueItem> Items);

internal sealed record DotnetToolQueueItem(
    string PackageId,
    string Version,
    string ChangeKind,
    long TotalDownloads,
    string PackageUrl,
    string PackageContentUrl,
    string RegistrationUrl,
    string CatalogEntryUrl);

internal sealed record DotnetToolDeltaQueueComputation(
    DotnetToolDeltaQueueSnapshot Delta,
    DotnetToolQueueSnapshot Queue);

