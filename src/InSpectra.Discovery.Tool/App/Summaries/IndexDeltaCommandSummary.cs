namespace InSpectra.Discovery.Tool.App.Summaries;


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
