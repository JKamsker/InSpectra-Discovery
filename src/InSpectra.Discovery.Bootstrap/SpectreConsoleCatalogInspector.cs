internal sealed class SpectreConsoleCatalogInspector
{
    private readonly NuGetApiClient _apiClient;
    private readonly PackageArchiveInspector _packageArchiveInspector;

    public SpectreConsoleCatalogInspector(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
        _packageArchiveInspector = new PackageArchiveInspector(apiClient);
    }

    public async Task<SpectreConsoleToolEntry> InspectAsync(
        DotnetToolIndexEntry package,
        SpectreConsoleFilterMode packageInspectionMode,
        CancellationToken cancellationToken)
    {
        var catalogLeaf = await _apiClient.GetCatalogLeafAsync(package.CatalogEntryUrl, cancellationToken);
        var detection = Detect(catalogLeaf);

        if (ShouldInspectPackage(packageInspectionMode, detection))
        {
            var packageInspection = await _packageArchiveInspector.InspectAsync(package.PackageContentUrl, cancellationToken);
            detection = detection with { PackageInspection = packageInspection };
        }

        return CreateToolEntry(package, detection);
    }

    public async Task<SpectreConsoleToolEntry?> TryInspectAsync(
        DotnetToolIndexEntry package,
        SpectreConsoleFilterMode mode,
        CancellationToken cancellationToken)
    {
        var inspection = await InspectAsync(package, mode, cancellationToken);
        return ShouldInclude(mode, inspection.Detection)
            ? inspection
            : null;
    }

    public static bool ShouldInclude(SpectreConsoleFilterMode mode, SpectreConsoleDetection detection)
        => mode switch
        {
            SpectreConsoleFilterMode.AnySpectreConsole => detection.HasSpectreConsole || detection.HasSpectreConsoleCli,
            SpectreConsoleFilterMode.SpectreConsoleCliOnly => detection.HasSpectreConsoleCli,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

    public static SpectreConsoleToolEntry CreateToolEntry(
        DotnetToolIndexEntry package,
        SpectreConsoleDetection detection)
        => new(
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

    private static bool ShouldInspectPackage(SpectreConsoleFilterMode mode, SpectreConsoleDetection detection)
        => mode switch
        {
            SpectreConsoleFilterMode.AnySpectreConsole => detection.HasSpectreConsole || detection.HasSpectreConsoleCli,
            SpectreConsoleFilterMode.SpectreConsoleCliOnly => detection.HasSpectreConsoleCli,
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };

    private static SpectreConsoleDetection Detect(CatalogLeaf catalogLeaf)
    {
        var matchedEntries = (catalogLeaf.PackageEntries ?? [])
            .Where(entry =>
                string.Equals(entry.Name, "Spectre.Console.dll", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.Name, "Spectre.Console.Cli.dll", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matchedDependencies = (catalogLeaf.DependencyGroups ?? [])
            .SelectMany(group => group.Dependencies ?? [])
            .Select(dependency => dependency.Id)
            .Where(id =>
                string.Equals(id, "Spectre.Console", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "Spectre.Console.Cli", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SpectreConsoleDetection(
            HasSpectreConsole: matchedEntries.Any(entry => entry.EndsWith("Spectre.Console.dll", StringComparison.OrdinalIgnoreCase))
                || matchedDependencies.Any(id => string.Equals(id, "Spectre.Console", StringComparison.OrdinalIgnoreCase)),
            HasSpectreConsoleCli: matchedEntries.Any(entry => entry.EndsWith("Spectre.Console.Cli.dll", StringComparison.OrdinalIgnoreCase))
                || matchedDependencies.Any(id => string.Equals(id, "Spectre.Console.Cli", StringComparison.OrdinalIgnoreCase)),
            MatchedPackageEntries: matchedEntries,
            MatchedDependencyIds: matchedDependencies,
            PackageInspection: SpectrePackageInspection.Empty);
    }
}
