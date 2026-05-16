namespace InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using InSpectra.Discovery.Tool.Catalog.Indexing;

using InSpectra.Discovery.Tool.NuGet;

using System.Collections.Concurrent;
using System.Text.Json;

internal sealed class SpectreConsoleCatalogFilter
{
    private readonly SpectreConsoleCatalogInspector _inspector;

    public SpectreConsoleCatalogFilter(NuGetApiClient apiClient)
    {
        _inspector = new SpectreConsoleCatalogInspector(apiClient);
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

        reportProgress?.Invoke($"Scanning catalog entries for {options.EvidenceLabel} evidence...");

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
                var inspection = await _inspector.TryInspectAsync(package, options.Mode, token);
                if (inspection is not null)
                {
                    matches.Add(inspection);
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
            Filter: options.FilterName,
            InputPath: inputPath,
            SourceGeneratedAtUtc: snapshot.GeneratedAtUtc,
            ScannedPackageCount: snapshot.Packages.Count,
            PackageCount: filteredPackages.Length,
            Packages: filteredPackages);
    }
}

