internal sealed record CliFxFilterSnapshot(
    DateTimeOffset GeneratedAtUtc,
    string Filter,
    string InputPath,
    DateTimeOffset SourceGeneratedAtUtc,
    int ScannedPackageCount,
    int PackageCount,
    IReadOnlyList<CliFxToolEntry> Packages);

internal sealed record CliFxToolEntry(
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
    CliFxDetection Detection);

internal sealed record CliFxDetection(
    bool HasCliFx,
    IReadOnlyList<string> MatchedPackageEntries,
    IReadOnlyList<string> MatchedDependencyIds,
    CliFxPackageInspection PackageInspection);

internal sealed record CliFxPackageInspection(
    IReadOnlyList<string> DepsFilePaths,
    IReadOnlyList<string> CliFxDependencyVersions,
    IReadOnlyList<CliFxAssemblyVersionInfo> CliFxAssemblies,
    IReadOnlyList<string> ToolSettingsPaths,
    IReadOnlyList<string> ToolCommandNames,
    IReadOnlyList<string> ToolEntryPointPaths,
    IReadOnlyList<string> ToolAssembliesReferencingCliFx)
{
    public bool HasToolAssemblyReferencingCliFx => ToolAssembliesReferencingCliFx.Count > 0;

    public static CliFxPackageInspection Empty { get; } = new(
        [],
        [],
        [],
        [],
        [],
        [],
        []);
}

internal sealed record CliFxAssemblyVersionInfo(
    string Path,
    string? AssemblyVersion,
    string? FileVersion,
    string? InformationalVersion);
