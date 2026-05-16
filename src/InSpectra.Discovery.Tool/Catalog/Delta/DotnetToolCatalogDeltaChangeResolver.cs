namespace InSpectra.Discovery.Tool.Catalog.Delta;

using InSpectra.Discovery.Tool.Catalog.Indexing;

using InSpectra.Discovery.Tool.NuGet;

using System.Collections.Concurrent;

internal sealed class DotnetToolCatalogDeltaChangeResolver
{
    private readonly NuGetApiClient _apiClient;
    private readonly DotnetToolIndexEntryResolver _entryResolver;

    public DotnetToolCatalogDeltaChangeResolver(
        NuGetApiClient apiClient,
        DotnetToolIndexEntryResolver entryResolver)
    {
        _apiClient = apiClient;
        _entryResolver = entryResolver;
    }

    public async Task<HashSet<string>> GetAffectedPackageIdsAsync(
        IReadOnlyList<CatalogPageItem> pageItems,
        IReadOnlyDictionary<string, DotnetToolIndexEntry> baselineLookup,
        int concurrency,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var affected = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        var skippedPackages = new ConcurrentQueue<string>();

        foreach (var deleted in pageItems.Where(item => IsPackageDelete(item.Type)))
        {
            if (baselineLookup.ContainsKey(deleted.PackageId))
            {
                affected[deleted.PackageId] = 0;
            }
        }

        var detailItems = pageItems.Where(item => !IsPackageDelete(item.Type)).ToArray();
        await Parallel.ForEachAsync(
            detailItems,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = concurrency,
            },
            async (item, token) =>
            {
                try
                {
                    if (baselineLookup.ContainsKey(item.PackageId))
                    {
                        affected[item.PackageId] = 0;
                        return;
                    }

                    var leaf = await _apiClient.GetCatalogLeafAsync(item.Id, token);
                    if (DotnetToolPackageType.IsDotnetTool(leaf))
                    {
                        affected[item.PackageId] = 0;
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    skippedPackages.Enqueue(
                        $"Skipped affected-package detection for '{item.PackageId}': {ex.Message}");
                }
            });

        ReportSkippedPackages("affected-package detection", skippedPackages, reportProgress);
        reportProgress?.Invoke($"Identified {affected.Count} affected package IDs.");
        return affected.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<DotnetToolDeltaEntry>> ResolveChangesAsync(
        IReadOnlyCollection<string> affectedPackageIds,
        IReadOnlyDictionary<string, DotnetToolIndexEntry> baselineLookup,
        string searchUrl,
        string registrationBaseUrl,
        int concurrency,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var changes = new ConcurrentBag<DotnetToolDeltaEntry>();
        var skippedPackages = new ConcurrentQueue<string>();
        await Parallel.ForEachAsync(
            affectedPackageIds,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = concurrency,
            },
            async (packageId, token) =>
            {
                try
                {
                    baselineLookup.TryGetValue(packageId, out var previous);
                    var resolution = await _entryResolver.TryResolveLatestListedNonFatalAsync(
                        packageId,
                        searchUrl,
                        registrationBaseUrl,
                        token);
                    if (resolution.IsSkipped)
                    {
                        skippedPackages.Enqueue(
                            $"Skipped change resolution for '{packageId}': {resolution.SkipReason ?? "Unknown reason."}");
                        return;
                    }

                    var change = TryCreateChange(previous, resolution.Entry);
                    if (change is not null)
                    {
                        changes.Add(change);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    skippedPackages.Enqueue(
                        $"Skipped change resolution for '{packageId}': {ex.Message}");
                }
            });

        ReportSkippedPackages("change resolution", skippedPackages, reportProgress);
        reportProgress?.Invoke($"Resolved {changes.Count} effective latest-version changes.");
        return changes.ToArray();
    }

    private static DotnetToolDeltaEntry? TryCreateChange(DotnetToolIndexEntry? previous, DotnetToolIndexEntry? current)
    {
        if (previous is null && current is not null)
        {
            return new DotnetToolDeltaEntry(
                current.PackageId,
                "added",
                null,
                current.LatestVersion,
                null,
                DeltaStateProjection.Project(current));
        }

        if (previous is not null && current is null)
        {
            return new DotnetToolDeltaEntry(
                previous.PackageId,
                "removed",
                previous.LatestVersion,
                null,
                DeltaStateProjection.Project(previous),
                null);
        }

        if (previous is not null && current is not null && !string.Equals(previous.LatestVersion, current.LatestVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new DotnetToolDeltaEntry(
                previous.PackageId,
                "latest-version-changed",
                previous.LatestVersion,
                current.LatestVersion,
                DeltaStateProjection.Project(previous),
                DeltaStateProjection.Project(current));
        }

        return null;
    }

    private static bool IsPackageDelete(string type)
        => type.Contains("PackageDelete", StringComparison.OrdinalIgnoreCase);

    private static void ReportSkippedPackages(
        string stage,
        ConcurrentQueue<string> skippedPackages,
        Action<string>? reportProgress)
    {
        if (reportProgress is null || skippedPackages.IsEmpty)
        {
            return;
        }

        var messages = skippedPackages
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(message => message, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        reportProgress($"Skipped {messages.Length} packages during {stage} due to non-fatal errors.");
        foreach (var message in messages.Take(10))
        {
            reportProgress($"  {message}");
        }

        if (messages.Length > 10)
        {
            reportProgress($"  ... {messages.Length - 10} more skipped packages.");
        }
    }
}

