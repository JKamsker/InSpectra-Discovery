namespace InSpectra.Discovery.Tool.Analysis.Help;

internal sealed record HelpBatchSnapshotItem(
    string PackageId,
    long? TotalDownloads,
    string? PackageUrl,
    string? PackageContentUrl,
    string? CatalogEntryUrl);
