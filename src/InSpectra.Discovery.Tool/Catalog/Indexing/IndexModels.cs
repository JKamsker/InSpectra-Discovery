namespace InSpectra.Discovery.Tool.Catalog.Indexing;

internal sealed record DotnetToolIndexSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string PackageType,
    int PackageCount,
    DotnetToolIndexSource Source,
    IReadOnlyList<DotnetToolIndexEntry> Packages);

internal sealed record DotnetToolIndexSource(
    string ServiceIndexUrl,
    string AutocompleteUrl,
    string SearchUrl,
    string RegistrationBaseUrl,
    string PrefixAlphabet,
    int ExpectedPackageCount,
    string SortOrder);

internal sealed record DotnetToolIndexEntry(
    string PackageId,
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
    string? ReadmeUrl);

