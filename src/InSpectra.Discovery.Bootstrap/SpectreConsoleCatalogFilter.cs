using System.Collections.Concurrent;
using System.Text.Json;

internal sealed class SpectreConsoleCatalogFilter
{
    private readonly NuGetApiClient _apiClient;

    public SpectreConsoleCatalogFilter(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<SpectreConsoleFilterSnapshot> RunAsync(
        SpectreConsoleFilterOptions options,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var inputPath = Path.GetFullPath(options.InputPath);
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input index file was not found: {inputPath}", inputPath);
        }

        reportProgress?.Invoke($"Loading input snapshot from {inputPath}...");
        await using var inputStream = File.OpenRead(inputPath);
        var snapshot = await JsonSerializer.DeserializeAsync<DotnetToolIndexSnapshot>(
            inputStream,
            JsonOptions.Default,
            cancellationToken);

        if (snapshot is null)
        {
            throw new InvalidOperationException($"Could not read a dotnet-tool snapshot from {inputPath}.");
        }

        reportProgress?.Invoke("Scanning catalog entries for Spectre.Console evidence...");

        var matches = new ConcurrentBag<SpectreConsoleToolEntry>();
        var completed = 0;

        await Parallel.ForEachAsync(
            snapshot.Packages,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = options.Concurrency,
            },
            async (package, token) =>
            {
                var catalogLeaf = await _apiClient.GetCatalogLeafAsync(package.CatalogEntryUrl, token);
                var detection = Detect(catalogLeaf);

                if (detection.HasSpectreConsole || detection.HasSpectreConsoleCli)
                {
                    matches.Add(new SpectreConsoleToolEntry(
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
                        Detection: detection));
                }

                var current = Interlocked.Increment(ref completed);
                if (current == snapshot.Packages.Count || current % 250 == 0)
                {
                    reportProgress?.Invoke($"  Scanned {current}/{snapshot.Packages.Count} catalog entries.");
                }
            });

        var filteredPackages = matches
            .OrderByDescending(entry => entry.TotalDownloads)
            .ThenBy(entry => entry.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SpectreConsoleFilterSnapshot(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Filter: "spectre-console",
            InputPath: inputPath,
            SourceGeneratedAtUtc: snapshot.GeneratedAtUtc,
            ScannedPackageCount: snapshot.Packages.Count,
            PackageCount: filteredPackages.Length,
            Packages: filteredPackages);
    }

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
            MatchedDependencyIds: matchedDependencies);
    }
}
