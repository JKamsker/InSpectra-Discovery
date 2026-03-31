namespace InSpectra.Discovery.Tool.Analysis.Help.Models;


internal sealed record HelpBatchSnapshotItem(
    string PackageId,
    long? TotalDownloads,
    string? PackageUrl,
    string? PackageContentUrl,
    string? CatalogEntryUrl);
