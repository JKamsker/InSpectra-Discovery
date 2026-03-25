internal sealed record IndexBuildCommandSummary(
    string Command,
    string OutputPath,
    int PackageCount,
    string SortOrder);

internal sealed record IndexDeltaCommandSummary(
    string Command,
    string CurrentSnapshotPath,
    string DeltaOutputPath,
    string CursorStatePath,
    int CatalogLeafCount,
    int AffectedPackageCount,
    int ChangedPackageCount,
    DateTimeOffset CursorStartUtc,
    DateTimeOffset CursorEndUtc);

internal sealed record SpectreConsoleFilterCommandSummary(
    string Command,
    string InputPath,
    string OutputPath,
    int ScannedPackageCount,
    int MatchedPackageCount);
