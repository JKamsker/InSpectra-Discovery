using System.Collections.Concurrent;

internal sealed class CurrentDotnetToolIndexBootstrapper
{
    private const string PackageType = "dotnettool";
    private readonly NuGetApiClient _apiClient;

    public CurrentDotnetToolIndexBootstrapper(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<DotnetToolIndexSnapshot> RunAsync(
        BootstrapOptions options,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var resources = await _apiClient.GetServiceResourcesAsync(options.ServiceIndexUrl, cancellationToken);
        var autocompleteUrl = resources.GetRequiredResource("SearchAutocompleteService/3.5.0");
        var searchUrl = resources.GetRequiredResource("SearchQueryService/3.5.0");
        var registrationBaseUrl = resources.GetRequiredResource("RegistrationsBaseUrl/Versioned", "RegistrationsBaseUrl/3.6.0");

        reportProgress?.Invoke("Fetching expected dotnet-tool package count from search...");
        var expectedCount = await _apiClient.GetSearchTotalHitsAsync(searchUrl, cancellationToken);

        reportProgress?.Invoke("Enumerating package IDs from autocomplete...");
        var packageIds = await EnumeratePackageIdsAsync(autocompleteUrl, options, reportProgress, cancellationToken);
        reportProgress?.Invoke($"Enumerated {packageIds.Count} unique package IDs.");

        if (packageIds.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"Autocomplete enumeration returned {packageIds.Count} packages, but search reports {expectedCount}. " +
                "Update the prefix alphabet or investigate changed NuGet search behavior before trusting this snapshot.");
        }

        reportProgress?.Invoke("Fetching registration metadata and download counts...");
        var packages = await BuildPackageIndexAsync(packageIds, searchUrl, registrationBaseUrl, options, reportProgress, cancellationToken);

        return new DotnetToolIndexSnapshot(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            PackageType: "DotnetTool",
            PackageCount: packages.Count,
            Source: new DotnetToolIndexSource(
                ServiceIndexUrl: options.ServiceIndexUrl,
                AutocompleteUrl: autocompleteUrl,
                SearchUrl: searchUrl,
                RegistrationBaseUrl: registrationBaseUrl,
                PrefixAlphabet: options.PrefixAlphabet,
                ExpectedPackageCount: expectedCount,
                SortOrder: "totalDownloads-desc"),
            Packages: packages);
    }

    private async Task<HashSet<string>> EnumeratePackageIdsAsync(
        string autocompleteUrl,
        BootstrapOptions options,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prefixes = options.PrefixAlphabet.Distinct().Select(character => character.ToString());

        foreach (var prefix in prefixes)
        {
            var skip = 0;
            var totalHits = 0;

            while (true)
            {
                var response = await _apiClient.AutocompleteAsync(
                    autocompleteUrl,
                    prefix,
                    skip,
                    options.PageSize,
                    PackageType,
                    cancellationToken);

                totalHits = response.TotalHits;
                if (response.Data.Count == 0)
                {
                    break;
                }

                foreach (var id in response.Data)
                {
                    ids.Add(id);
                }

                skip += response.Data.Count;
            }

            reportProgress?.Invoke($"  Prefix '{prefix}' yielded {totalHits} hits.");
        }

        return ids;
    }

    private async Task<IReadOnlyList<DotnetToolIndexEntry>> BuildPackageIndexAsync(
        IEnumerable<string> packageIds,
        string searchUrl,
        string registrationBaseUrl,
        BootstrapOptions options,
        Action<string>? reportProgress,
        CancellationToken cancellationToken)
    {
        var results = new ConcurrentBag<DotnetToolIndexEntry>();
        var packageList = packageIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
        var completed = 0;

        await Parallel.ForEachAsync(
            packageList,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = options.MetadataConcurrency,
            },
            async (packageId, token) =>
            {
                var entry = await BuildPackageEntryAsync(packageId, searchUrl, registrationBaseUrl, token);
                results.Add(entry);

                var current = Interlocked.Increment(ref completed);
                if (current == packageList.Length || current % 250 == 0)
                {
                    reportProgress?.Invoke($"  Loaded {current}/{packageList.Length} package records.");
                }
            });

        return results
            .OrderByDescending(entry => entry.TotalDownloads)
            .ThenBy(entry => entry.PackageId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<DotnetToolIndexEntry> BuildPackageEntryAsync(
        string packageId,
        string searchUrl,
        string registrationBaseUrl,
        CancellationToken cancellationToken)
    {
        var registrationTask = _apiClient.GetRegistrationIndexAsync(registrationBaseUrl, packageId, cancellationToken);
        var totalDownloadsTask = _apiClient.GetPackageTotalDownloadsAsync(searchUrl, packageId, cancellationToken);

        await Task.WhenAll(registrationTask, totalDownloadsTask);

        var registrationIndex = await registrationTask;
        var totalDownloads = await totalDownloadsTask;
        var versionCount = registrationIndex.Items.Sum(page => page.Count);

        foreach (var pageReference in registrationIndex.Items.Reverse())
        {
            var leaves = pageReference.Items
                ?? (await _apiClient.GetRegistrationPageAsync(pageReference.Id, cancellationToken)).Items;

            foreach (var leaf in leaves.Reverse())
            {
                if (leaf.CatalogEntry.Listed != true)
                {
                    continue;
                }

                return new DotnetToolIndexEntry(
                    PackageId: packageId,
                    LatestVersion: leaf.CatalogEntry.Version,
                    TotalDownloads: totalDownloads,
                    VersionCount: versionCount,
                    Listed: true,
                    PublishedAtUtc: leaf.CatalogEntry.Published?.ToUniversalTime(),
                    CommitTimestampUtc: leaf.CommitTimeStamp.ToUniversalTime(),
                    ProjectUrl: leaf.CatalogEntry.ProjectUrl,
                    PackageUrl: $"https://www.nuget.org/packages/{Uri.EscapeDataString(packageId)}/{Uri.EscapeDataString(leaf.CatalogEntry.Version)}",
                    PackageContentUrl: leaf.PackageContent,
                    RegistrationUrl: registrationIndex.Id,
                    CatalogEntryUrl: leaf.CatalogEntry.Id,
                    Authors: leaf.CatalogEntry.Authors,
                    Description: leaf.CatalogEntry.Description,
                    LicenseExpression: leaf.CatalogEntry.LicenseExpression,
                    LicenseUrl: leaf.CatalogEntry.LicenseUrl,
                    ReadmeUrl: leaf.CatalogEntry.ReadmeUrl);
            }
        }

        throw new InvalidOperationException($"No listed version was found in the registration index for '{packageId}'.");
    }
}
