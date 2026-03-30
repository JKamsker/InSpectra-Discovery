namespace InSpectra.Discovery.Tool.Catalog.Filtering.CliFx;

using System.Collections.Concurrent;
using System.Text.Json;

internal sealed class CliFxCatalogFilter
{
    private readonly CliFxCatalogInspector _inspector;

    public CliFxCatalogFilter(NuGetApiClient apiClient)
    {
        _inspector = new CliFxCatalogInspector(apiClient);
    }

    public async Task<CliFxFilterSnapshot> RunAsync(
        CliFxFilterOptions options,
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
        var snapshot = await JsonSerializer.DeserializeAsync<DotnetToolIndexSnapshot>(inputStream, JsonOptions.Default, cancellationToken);
        if (snapshot is null)
        {
            throw new InvalidOperationException($"Could not read a dotnet-tool snapshot from {inputPath}.");
        }

        reportProgress?.Invoke($"Scanning catalog entries for {CliFxFilterOptions.EvidenceLabel} evidence...");

        var matches = new ConcurrentBag<CliFxToolEntry>();
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
                var inspection = await _inspector.TryInspectAsync(package, token);
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

        return new CliFxFilterSnapshot(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            Filter: CliFxFilterOptions.FilterName,
            InputPath: inputPath,
            SourceGeneratedAtUtc: snapshot.GeneratedAtUtc,
            ScannedPackageCount: snapshot.Packages.Count,
            PackageCount: filteredPackages.Length,
            Packages: filteredPackages);
    }
}

