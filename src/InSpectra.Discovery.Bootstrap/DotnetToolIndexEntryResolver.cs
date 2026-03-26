internal sealed class DotnetToolIndexEntryResolver
{
    private readonly NuGetApiClient _apiClient;

    public DotnetToolIndexEntryResolver(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<DotnetToolIndexEntry> ResolveRequiredAsync(
        string packageId,
        string searchUrl,
        string registrationBaseUrl,
        CancellationToken cancellationToken)
    {
        var entry = await TryResolveLatestListedAsync(packageId, searchUrl, registrationBaseUrl, cancellationToken);
        return entry ?? throw new InvalidOperationException(
            $"No listed version was found in the registration index for '{packageId}'.");
    }

    public async Task<DotnetToolIndexEntry?> TryResolveLatestListedAsync(
        string packageId,
        string searchUrl,
        string registrationBaseUrl,
        CancellationToken cancellationToken)
    {
        var registrationIndex = await _apiClient.GetRegistrationIndexAsync(registrationBaseUrl, packageId, cancellationToken);
        var versionCount = registrationIndex.Items.Sum(page => page.Count);
        var latestLeaf = await FindLatestListedLeafAsync(registrationIndex, cancellationToken);
        if (latestLeaf is null)
        {
            return null;
        }

        var latestCatalogLeaf = await _apiClient.GetCatalogLeafAsync(latestLeaf.CatalogEntry.Id, cancellationToken);
        if (!DotnetToolPackageType.IsDotnetTool(latestCatalogLeaf))
        {
            return null;
        }

        var totalDownloads = await _apiClient.GetPackageTotalDownloadsAsync(searchUrl, packageId, cancellationToken);
        return CreateEntry(packageId, registrationIndex, latestLeaf, totalDownloads, versionCount);
    }

    private async Task<RegistrationLeaf?> FindLatestListedLeafAsync(
        RegistrationIndex registrationIndex,
        CancellationToken cancellationToken)
    {
        foreach (var pageReference in registrationIndex.Items.Reverse())
        {
            var leaves = pageReference.Items
                ?? (await _apiClient.GetRegistrationPageAsync(pageReference.Id, cancellationToken)).Items;

            foreach (var leaf in leaves.Reverse())
            {
                if (leaf.CatalogEntry.Listed == true)
                {
                    return leaf;
                }
            }
        }

        return null;
    }

    private static DotnetToolIndexEntry CreateEntry(
        string packageId,
        RegistrationIndex registrationIndex,
        RegistrationLeaf leaf,
        long totalDownloads,
        int versionCount)
    {
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
