internal sealed class CliFxCatalogInspector
{
    private readonly NuGetApiClient _apiClient;
    private readonly CliFxPackageArchiveInspector _packageArchiveInspector;

    public CliFxCatalogInspector(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
        _packageArchiveInspector = new CliFxPackageArchiveInspector(apiClient);
    }

    public async Task<CliFxToolEntry> InspectAsync(DotnetToolIndexEntry package, CancellationToken cancellationToken)
    {
        var catalogLeaf = await _apiClient.GetCatalogLeafAsync(package.CatalogEntryUrl, cancellationToken);
        var detection = Detect(catalogLeaf);

        if (detection.HasCliFx)
        {
            var packageInspection = await _packageArchiveInspector.InspectAsync(package.PackageContentUrl, cancellationToken);
            detection = detection with { PackageInspection = packageInspection };
        }

        return new CliFxToolEntry(
            PackageId: package.PackageId,
            LatestVersion: package.LatestVersion,
            TotalDownloads: package.TotalDownloads,
            VersionCount: package.VersionCount,
            Listed: package.Listed,
            PublishedAtUtc: package.PublishedAtUtc,
            CommitTimestampUtc: package.CommitTimestampUtc,
            ProjectUrl: package.ProjectUrl,
            PackageUrl: package.PackageUrl,
            PackageContentUrl: package.PackageContentUrl,
            RegistrationUrl: package.RegistrationUrl,
            CatalogEntryUrl: package.CatalogEntryUrl,
            Authors: package.Authors,
            Description: package.Description,
            LicenseExpression: package.LicenseExpression,
            LicenseUrl: package.LicenseUrl,
            ReadmeUrl: package.ReadmeUrl,
            Detection: detection);
    }

    public async Task<CliFxToolEntry?> TryInspectAsync(DotnetToolIndexEntry package, CancellationToken cancellationToken)
    {
        var inspection = await InspectAsync(package, cancellationToken);
        return ShouldInclude(inspection.Detection) ? inspection : null;
    }

    public static bool ShouldInclude(CliFxDetection detection)
    {
        if (!detection.HasCliFx)
        {
            return false;
        }

        return detection.MatchedDependencyIds.Any(id => string.Equals(id, "CliFx", StringComparison.OrdinalIgnoreCase))
            || detection.PackageInspection.CliFxAssemblies.Count > 0
            || detection.PackageInspection.HasToolAssemblyReferencingCliFx;
    }

    private static CliFxDetection Detect(CatalogLeaf catalogLeaf)
    {
        var matchedEntries = (catalogLeaf.PackageEntries ?? [])
            .Where(entry => string.Equals(entry.Name, "CliFx.dll", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matchedDependencies = (catalogLeaf.DependencyGroups ?? [])
            .SelectMany(group => group.Dependencies ?? [])
            .Select(dependency => dependency.Id)
            .Where(id => string.Equals(id, "CliFx", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new CliFxDetection(
            HasCliFx: matchedEntries.Length > 0 || matchedDependencies.Length > 0,
            MatchedPackageEntries: matchedEntries,
            MatchedDependencyIds: matchedDependencies,
            PackageInspection: CliFxPackageInspection.Empty);
    }
}
