namespace InSpectra.Discovery.Tool.Catalog.Delta.SpectreConsole;

using InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;


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
    SpectreConsoleCliDeltaState? Previous,
    SpectreConsoleCliDeltaState? Current);

internal sealed record SpectreConsoleCliDeltaState(
    string LatestVersion,
    long TotalDownloads,
    int VersionCount,
    bool Listed,
    DateTimeOffset? PublishedAtUtc,
    DateTimeOffset CommitTimestampUtc,
    string? ProjectUrl,
    string PackageUrl,
    string PackageContentUrl,
    string RegistrationUrl,
    string CatalogEntryUrl,
    string? Authors,
    string? Description,
    string? LicenseExpression,
    string? LicenseUrl,
    string? ReadmeUrl,
    SpectreConsoleDetection Detection);

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

