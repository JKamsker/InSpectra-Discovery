namespace InSpectra.Discovery.Tool.Infrastructure;

internal static class PackageVersionResolver
{
    public static async Task<(RegistrationLeafDocument Leaf, CatalogLeaf CatalogLeaf)> ResolveAsync(
        NuGetApiClient apiClient,
        string packageId,
        string version,
        CancellationToken cancellationToken)
    {
        var resources = await apiClient.GetServiceResourcesAsync(BootstrapOptions.DefaultServiceIndexUrl, cancellationToken);
        var registrationBaseUrl = resources.GetRequiredResource(
            "RegistrationsBaseUrl/3.6.0",
            "RegistrationsBaseUrl/3.4.0",
            "RegistrationsBaseUrl/3.0.0-rc",
            "RegistrationsBaseUrl/3.0.0-beta");
        var registrationIndex = await apiClient.GetRegistrationIndexAsync(registrationBaseUrl, packageId, cancellationToken);

        foreach (var pageReference in registrationIndex.Items)
        {
            var leaves = pageReference.Items ?? (await apiClient.GetRegistrationPageAsync(pageReference.Id, cancellationToken)).Items;
            var match = leaves.FirstOrDefault(candidate => string.Equals(candidate.CatalogEntry.Version, version, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                continue;
            }

            var leaf = await apiClient.GetRegistrationLeafAsync(match.Id ?? $"{pageReference.Id.TrimEnd('/')}/{version}.json", cancellationToken);
            var catalogLeaf = await apiClient.GetCatalogLeafAsync(leaf.CatalogEntryUrl, cancellationToken);
            return (leaf, catalogLeaf);
        }

        throw new InvalidOperationException($"Could not resolve package '{packageId}' version '{version}' from the NuGet registration index.");
    }

    public static string? NormalizeRepositoryUrl(string? repositoryUrl)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            return null;
        }

        var normalized = repositoryUrl.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out _))
        {
            return normalized;
        }

        return normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^4]
            : normalized;
    }
}
