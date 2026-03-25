internal sealed record SpectreConsoleFilterSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string Filter,
    string InputPath,
    DateTimeOffset SourceGeneratedAtUtc,
    int ScannedPackageCount,
    int PackageCount,
    IReadOnlyList<SpectreConsoleToolEntry> Packages);

internal sealed record SpectreConsoleToolEntry(
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
    string? ReadmeUrl,
    SpectreConsoleDetection Detection);

internal sealed record SpectreConsoleDetection(
    bool HasSpectreConsole,
    bool HasSpectreConsoleCli,
    IReadOnlyList<string> MatchedPackageEntries,
    IReadOnlyList<string> MatchedDependencyIds);
