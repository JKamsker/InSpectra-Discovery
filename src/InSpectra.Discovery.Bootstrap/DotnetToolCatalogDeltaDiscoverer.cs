using System.Collections.Concurrent;
using System.Text.Json;

internal sealed class DotnetToolCatalogDeltaDiscoverer
{
    private readonly NuGetApiClient _apiClient;
    private readonly DotnetToolIndexEntryResolver _entryResolver;

    public DotnetToolCatalogDeltaDiscoverer(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
        _entryResolver = new DotnetToolIndexEntryResolver(apiClient);
    }

    public async Task<DotnetToolDeltaComputation> RunAsync(
        IndexDeltaOptions options,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var currentSnapshotPath = Path.GetFullPath(options.CurrentSnapshotPath);
        var persistedCurrentSnapshotPath = NormalizePath(options.CurrentSnapshotPath);
        var currentSnapshot = await LoadJsonAsync<DotnetToolIndexSnapshot>(currentSnapshotPath, cancellationToken);
        var cursorStatePath = Path.GetFullPath(options.CursorStatePath);
        var currentCursorState = await LoadCursorStateAsync(cursorStatePath, persistedCurrentSnapshotPath, currentSnapshot, options, cancellationToken);
        var effectiveCatalogSinceUtc = currentCursorState.CursorCommitTimestampUtc.AddMinutes(-currentCursorState.OverlapMinutes);

        var baselineLookup = currentSnapshot.Packages.ToDictionary(entry => entry.PackageId, StringComparer.OrdinalIgnoreCase);
        var resources = await _apiClient.GetServiceResourcesAsync(options.ServiceIndexUrl, cancellationToken);
        var catalogIndexUrl = resources.GetRequiredResource("Catalog/3.0.0");
        var searchUrl = resources.GetRequiredResource("SearchQueryService/3.5.0");
        var autocompleteUrl = resources.GetRequiredResource("SearchAutocompleteService/3.5.0");
        var registrationBaseUrl = resources.GetRequiredResource("RegistrationsBaseUrl/Versioned", "RegistrationsBaseUrl/3.6.0");

        reportProgress?.Invoke($"Loading NuGet catalog pages newer than {effectiveCatalogSinceUtc:O}...");
        var catalogIndex = await _apiClient.GetCatalogIndexAsync(catalogIndexUrl, cancellationToken);
        var relevantPages = catalogIndex.Items
            .Where(page => page.CommitTimeStamp.ToUniversalTime() > effectiveCatalogSinceUtc)
            .OrderBy(page => page.CommitTimeStamp)
            .ToArray();

        var pageItems = await LoadCatalogPageItemsAsync(relevantPages, effectiveCatalogSinceUtc, reportProgress, cancellationToken);
        var affectedPackageIds = await GetAffectedPackageIdsAsync(pageItems, baselineLookup, options.Concurrency, reportProgress, cancellationToken);
        var changes = await ResolveChangesAsync(
            affectedPackageIds,
            baselineLookup,
            searchUrl,
            registrationBaseUrl,
            options.Concurrency,
            reportProgress,
            cancellationToken);

        var nextCursorUtc = pageItems.Count == 0
            ? currentCursorState.CursorCommitTimestampUtc
            : pageItems.Max(item => item.CommitTimeStamp).ToUniversalTime();

        var changedEntries = changes
            .OrderBy(change => change.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var updatedCurrentSnapshot = ApplyChanges(
            currentSnapshot,
            changedEntries,
            generatedAtUtc,
            options.ServiceIndexUrl,
            autocompleteUrl,
            searchUrl,
            registrationBaseUrl);
        var nextCursorState = currentCursorState with
        {
            ServiceIndexUrl = options.ServiceIndexUrl,
            CurrentSnapshotPath = persistedCurrentSnapshotPath,
            CursorCommitTimestampUtc = nextCursorUtc,
            OverlapMinutes = options.OverlapMinutes,
        };

        var delta = new DotnetToolDeltaSnapshot(
            GeneratedAtUtc: generatedAtUtc,
            CursorStartUtc: currentCursorState.CursorCommitTimestampUtc,
            EffectiveCatalogSinceUtc: effectiveCatalogSinceUtc,
            CursorEndUtc: nextCursorUtc,
            ServiceIndexUrl: options.ServiceIndexUrl,
            CatalogIndexUrl: catalogIndexUrl,
            CurrentSnapshotPath: persistedCurrentSnapshotPath,
            CatalogPageCount: relevantPages.Length,
            CatalogLeafCount: pageItems.Count,
            AffectedPackageCount: affectedPackageIds.Count,
            ChangedPackageCount: changedEntries.Length,
            Packages: changedEntries);

        return new DotnetToolDeltaComputation(delta, nextCursorState, updatedCurrentSnapshot);
    }

    private async Task<List<CatalogPageItem>> LoadCatalogPageItemsAsync(
        IReadOnlyList<CatalogPageReference> relevantPages,
        DateTimeOffset effectiveCatalogSinceUtc,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var results = new List<CatalogPageItem>();
        foreach (var page in relevantPages)
        {
            var catalogPage = await _apiClient.GetCatalogPageAsync(page.Id, cancellationToken);
            results.AddRange(catalogPage.Items.Where(item => item.CommitTimeStamp.ToUniversalTime() > effectiveCatalogSinceUtc));
        }

        reportProgress?.Invoke($"Loaded {results.Count} catalog leaves from {relevantPages.Count} pages.");
        return results;
    }

    private async Task<HashSet<string>> GetAffectedPackageIdsAsync(
        IReadOnlyList<CatalogPageItem> pageItems,
        IReadOnlyDictionary<string, DotnetToolIndexEntry> baselineLookup,
        int concurrency,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var affected = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

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
                if (baselineLookup.ContainsKey(item.PackageId))
                {
                    affected[item.PackageId] = 0;
                    return;
                }

                var leaf = await _apiClient.GetCatalogLeafAsync(item.Id, token);
                if (IsDotnetTool(leaf))
                {
                    affected[item.PackageId] = 0;
                }
            });

        reportProgress?.Invoke($"Identified {affected.Count} affected package IDs.");
        return affected.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<DotnetToolDeltaEntry>> ResolveChangesAsync(
        IReadOnlyCollection<string> affectedPackageIds,
        IReadOnlyDictionary<string, DotnetToolIndexEntry> baselineLookup,
        string searchUrl,
        string registrationBaseUrl,
        int concurrency,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var changes = new ConcurrentBag<DotnetToolDeltaEntry>();
        await Parallel.ForEachAsync(
            affectedPackageIds,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = concurrency,
            },
            async (packageId, token) =>
            {
                baselineLookup.TryGetValue(packageId, out var previous);
                var current = await _entryResolver.TryResolveLatestListedAsync(packageId, searchUrl, registrationBaseUrl, token);
                var change = TryCreateChange(previous, current);
                if (change is not null)
                {
                    changes.Add(change);
                }
            });

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

    private static DotnetToolIndexSnapshot ApplyChanges(
        DotnetToolIndexSnapshot currentSnapshot,
        IReadOnlyList<DotnetToolDeltaEntry> changes,
        DateTimeOffset generatedAtUtc,
        string serviceIndexUrl,
        string autocompleteUrl,
        string searchUrl,
        string registrationBaseUrl)
    {
        var packages = currentSnapshot.Packages.ToDictionary(entry => entry.PackageId, StringComparer.OrdinalIgnoreCase);
        foreach (var change in changes)
        {
            if (change.Current is null)
            {
                packages.Remove(change.PackageId);
            }
            else
            {
                packages[change.PackageId] = DeltaStateProjection.Rehydrate(change.PackageId, change.Current);
            }
        }

        var orderedPackages = packages.Values
            .OrderByDescending(entry => entry.TotalDownloads)
            .ThenBy(entry => entry.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return currentSnapshot with
        {
            GeneratedAtUtc = generatedAtUtc,
            PackageCount = orderedPackages.Length,
            Source = currentSnapshot.Source with
            {
                ServiceIndexUrl = serviceIndexUrl,
                AutocompleteUrl = autocompleteUrl,
                SearchUrl = searchUrl,
                RegistrationBaseUrl = registrationBaseUrl,
                ExpectedPackageCount = orderedPackages.Length,
            },
            Packages = orderedPackages,
        };
    }

    private static bool IsPackageDelete(string type)
        => type.Contains("PackageDelete", StringComparison.OrdinalIgnoreCase);

    private static bool IsDotnetTool(CatalogLeaf leaf)
        => (leaf.PackageTypes ?? [])
            .Any(packageType => string.Equals(packageType.Name, "DotnetTool", StringComparison.OrdinalIgnoreCase));

    private static async Task<T> LoadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions.Default, cancellationToken);
        return value ?? throw new InvalidOperationException($"Could not deserialize '{path}'.");
    }

    private static async Task<DotnetToolCatalogCursorState> LoadCursorStateAsync(
        string cursorStatePath,
        string persistedCurrentSnapshotPath,
        DotnetToolIndexSnapshot currentSnapshot,
        IndexDeltaOptions options,
        CancellationToken cancellationToken)
    {
        if (File.Exists(cursorStatePath))
        {
            var existing = await LoadJsonAsync<DotnetToolCatalogCursorState>(cursorStatePath, cancellationToken);
            return existing with
            {
                ServiceIndexUrl = options.ServiceIndexUrl,
                CurrentSnapshotPath = persistedCurrentSnapshotPath,
                OverlapMinutes = options.OverlapMinutes,
            };
        }

        var seedCursorUtc = options.SeedCursorUtc ?? currentSnapshot.GeneratedAtUtc;
        return new DotnetToolCatalogCursorState(
            SchemaVersion: 1,
            ServiceIndexUrl: options.ServiceIndexUrl,
            CurrentSnapshotPath: persistedCurrentSnapshotPath,
            CursorCommitTimestampUtc: seedCursorUtc.ToUniversalTime(),
            OverlapMinutes: options.OverlapMinutes,
            SeededAtUtc: DateTimeOffset.UtcNow,
            SeedSource: options.SeedCursorUtc is null
                ? persistedCurrentSnapshotPath
                : "command-line-seed");
    }

    private static string NormalizePath(string path)
        => path.Replace('\\', '/');
}
