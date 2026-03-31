namespace InSpectra.Discovery.Tool.Catalog.Delta;

using InSpectra.Discovery.Tool.Catalog.Delta.SpectreConsole;

using InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;

using InSpectra.Discovery.Tool.Catalog.Indexing;

internal static class DeltaStateProjection
{
    public static DotnetToolDeltaState Project(DotnetToolIndexEntry entry)
        => new(
            LatestVersion: entry.LatestVersion,
            TotalDownloads: entry.TotalDownloads,
            VersionCount: entry.VersionCount,
            Listed: entry.Listed,
            PublishedAtUtc: entry.PublishedAtUtc,
            CommitTimestampUtc: entry.CommitTimestampUtc,
            ProjectUrl: entry.ProjectUrl,
            PackageUrl: entry.PackageUrl,
            PackageContentUrl: entry.PackageContentUrl,
            RegistrationUrl: entry.RegistrationUrl,
            CatalogEntryUrl: entry.CatalogEntryUrl,
            Authors: entry.Authors,
            Description: entry.Description,
            LicenseExpression: entry.LicenseExpression,
            LicenseUrl: entry.LicenseUrl,
            ReadmeUrl: entry.ReadmeUrl);

    public static DotnetToolIndexEntry Rehydrate(string packageId, DotnetToolDeltaState state)
        => new(
            PackageId: packageId,
            LatestVersion: state.LatestVersion,
            TotalDownloads: state.TotalDownloads,
            VersionCount: state.VersionCount,
            Listed: state.Listed,
            PublishedAtUtc: state.PublishedAtUtc,
            CommitTimestampUtc: state.CommitTimestampUtc,
            ProjectUrl: state.ProjectUrl,
            PackageUrl: state.PackageUrl,
            PackageContentUrl: state.PackageContentUrl,
            RegistrationUrl: state.RegistrationUrl,
            CatalogEntryUrl: state.CatalogEntryUrl,
            Authors: state.Authors,
            Description: state.Description,
            LicenseExpression: state.LicenseExpression,
            LicenseUrl: state.LicenseUrl,
            ReadmeUrl: state.ReadmeUrl);

    public static SpectreConsoleCliDeltaState Project(SpectreConsoleToolEntry entry)
        => new(
            LatestVersion: entry.LatestVersion,
            TotalDownloads: entry.TotalDownloads,
            VersionCount: entry.VersionCount,
            Listed: entry.Listed,
            PublishedAtUtc: entry.PublishedAtUtc,
            CommitTimestampUtc: entry.CommitTimestampUtc,
            ProjectUrl: entry.ProjectUrl,
            PackageUrl: entry.PackageUrl,
            PackageContentUrl: entry.PackageContentUrl,
            RegistrationUrl: entry.RegistrationUrl,
            CatalogEntryUrl: entry.CatalogEntryUrl,
            Authors: entry.Authors,
            Description: entry.Description,
            LicenseExpression: entry.LicenseExpression,
            LicenseUrl: entry.LicenseUrl,
            ReadmeUrl: entry.ReadmeUrl,
            Detection: entry.Detection);
}

