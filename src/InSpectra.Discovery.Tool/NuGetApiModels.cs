using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record NuGetServiceIndex(
    IReadOnlyList<NuGetServiceResource> Resources)
{
    public string GetRequiredResource(params string[] preferredTypes)
    {
        foreach (var preferredType in preferredTypes)
        {
            var resource = Resources.FirstOrDefault(candidate =>
                string.Equals(candidate.Type, preferredType, StringComparison.OrdinalIgnoreCase));

            if (resource is not null)
            {
                return resource.Id;
            }
        }

        throw new InvalidOperationException($"Could not find any of the required service resources: {string.Join(", ", preferredTypes)}.");
    }
}

internal sealed record NuGetServiceResource(
    string Id,
    string Type);

internal sealed record CatalogIndex(
    IReadOnlyList<CatalogPageReference> Items);

internal sealed record CatalogPageReference(
    string Id,
    DateTimeOffset CommitTimeStamp);

internal sealed record CatalogPage(
    IReadOnlyList<CatalogPageItem> Items);

internal sealed record CatalogPageItem(
    string Id,
    string Type,
    DateTimeOffset CommitTimeStamp,
    string PackageId,
    string PackageVersion);

internal sealed record SearchResponse(
    int TotalHits,
    IReadOnlyList<SearchPackage> Data);

internal sealed record SearchPackage(
    string Id,
    long TotalDownloads);

internal sealed record AutocompleteResponse(
    int TotalHits,
    IReadOnlyList<string> Data);

internal sealed record RegistrationIndex(
    string Id,
    IReadOnlyList<RegistrationPageReference> Items);

internal sealed record RegistrationPageReference(
    string Id,
    int Count,
    IReadOnlyList<RegistrationPageLeaf>? Items);

internal sealed record RegistrationPage(
    IReadOnlyList<RegistrationPageLeaf> Items);

internal sealed record RegistrationPageLeaf(
    string? Id,
    DateTimeOffset CommitTimeStamp,
    CatalogEntry CatalogEntry,
    string PackageContent,
    bool HasEmbeddedCatalogEntry);

internal sealed record RegistrationLeafDocument(
    string? Id,
    string CatalogEntryUrl,
    bool? Listed,
    string PackageContent,
    DateTimeOffset? Published);

[JsonConverter(typeof(CatalogRepositoryJsonConverter))]
internal sealed record CatalogRepository(
    string? Type,
    string? Url,
    string? Commit);

internal sealed record CatalogEntry(
    string Id,
    string? Authors,
    string? Description,
    string? LicenseExpression,
    string? LicenseUrl,
    bool? Listed,
    string? ProjectUrl,
    DateTimeOffset? Published,
    CatalogRepository? Repository,
    string? ReadmeUrl,
    string Version);

internal sealed record CatalogLeaf(
    string Id,
    string? ProjectUrl,
    CatalogRepository? Repository,
    IReadOnlyList<CatalogPackageEntry>? PackageEntries,
    IReadOnlyList<CatalogDependencyGroup>? DependencyGroups,
    JsonElement? PackageTypes);

internal sealed record CatalogPackageEntry(
    string FullName,
    string Name);

internal sealed record CatalogPackageType(
    string Name,
    string? Version);

internal sealed record CatalogDependencyGroup(
    IReadOnlyList<CatalogDependency>? Dependencies);

internal sealed record CatalogDependency(
    string Id);
