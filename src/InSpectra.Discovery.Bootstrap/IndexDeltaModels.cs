internal sealed record DotnetToolCatalogCursorState(
    int SchemaVersion,
    string ServiceIndexUrl,
    string CurrentSnapshotPath,
    DateTimeOffset CursorCommitTimestampUtc,
    int OverlapMinutes,
    DateTimeOffset SeededAtUtc,
    string SeedSource);

internal sealed record DotnetToolDeltaSnapshot(
    DateTimeOffset GeneratedAtUtc,
    DateTimeOffset CursorStartUtc,
    DateTimeOffset EffectiveCatalogSinceUtc,
    DateTimeOffset CursorEndUtc,
    string ServiceIndexUrl,
    string CatalogIndexUrl,
    string CurrentSnapshotPath,
    int CatalogPageCount,
    int CatalogLeafCount,
    int AffectedPackageCount,
    int ChangedPackageCount,
    IReadOnlyList<DotnetToolDeltaEntry> Packages);

internal sealed record DotnetToolDeltaEntry(
    string PackageId,
    string ChangeKind,
    string? PreviousVersion,
    string? CurrentVersion,
    DotnetToolIndexEntry? Previous,
    DotnetToolIndexEntry? Current);

internal sealed record DotnetToolDeltaComputation(
    DotnetToolDeltaSnapshot Delta,
    DotnetToolCatalogCursorState CursorState,
    DotnetToolIndexSnapshot UpdatedCurrentSnapshot);
