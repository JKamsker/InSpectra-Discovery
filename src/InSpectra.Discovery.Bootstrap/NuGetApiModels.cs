using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record NuGetServiceIndex(
    [property: JsonPropertyName("resources")] IReadOnlyList<NuGetServiceResource> Resources)
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
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("@type")] string Type);

internal sealed record CatalogIndex(
    [property: JsonPropertyName("items")] IReadOnlyList<CatalogPageReference> Items);

internal sealed record CatalogPageReference(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("commitTimeStamp")] DateTimeOffset CommitTimeStamp);

internal sealed record CatalogPage(
    [property: JsonPropertyName("items")] IReadOnlyList<CatalogPageItem> Items);

internal sealed record CatalogPageItem(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("@type")] string Type,
    [property: JsonPropertyName("commitTimeStamp")] DateTimeOffset CommitTimeStamp,
    [property: JsonPropertyName("nuget:id")] string PackageId,
    [property: JsonPropertyName("nuget:version")] string PackageVersion);

internal sealed record SearchResponse(
    [property: JsonPropertyName("totalHits")] int TotalHits,
    [property: JsonPropertyName("data")] IReadOnlyList<SearchPackage> Data);

internal sealed record SearchPackage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("totalDownloads")] long TotalDownloads);

internal sealed record AutocompleteResponse(
    [property: JsonPropertyName("totalHits")] int TotalHits,
    [property: JsonPropertyName("data")] IReadOnlyList<string> Data);

internal sealed record RegistrationIndex(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("items")] IReadOnlyList<RegistrationPageReference> Items);

internal sealed record RegistrationPageReference(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("items")] IReadOnlyList<RegistrationLeaf>? Items);

internal sealed record RegistrationPage(
    [property: JsonPropertyName("items")] IReadOnlyList<RegistrationLeaf> Items);

internal sealed record RegistrationLeaf(
    [property: JsonPropertyName("commitTimeStamp")] DateTimeOffset CommitTimeStamp,
    [property: JsonPropertyName("catalogEntry")] CatalogEntry CatalogEntry,
    [property: JsonPropertyName("packageContent")] string PackageContent);

internal sealed record CatalogEntry(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("authors")] string? Authors,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("licenseExpression")] string? LicenseExpression,
    [property: JsonPropertyName("licenseUrl")] string? LicenseUrl,
    [property: JsonPropertyName("listed")] bool? Listed,
    [property: JsonPropertyName("projectUrl")] string? ProjectUrl,
    [property: JsonPropertyName("published")] DateTimeOffset? Published,
    [property: JsonPropertyName("readmeUrl")] string? ReadmeUrl,
    [property: JsonPropertyName("version")] string Version);

internal sealed record CatalogLeaf(
    [property: JsonPropertyName("@id")] string Id,
    [property: JsonPropertyName("packageEntries")] IReadOnlyList<CatalogPackageEntry>? PackageEntries,
    [property: JsonPropertyName("dependencyGroups")] IReadOnlyList<CatalogDependencyGroup>? DependencyGroups,
    [property: JsonPropertyName("packageTypes")] JsonElement? PackageTypes);

internal sealed record CatalogPackageEntry(
    [property: JsonPropertyName("fullName")] string FullName,
    [property: JsonPropertyName("name")] string Name);

internal sealed record CatalogPackageType(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string? Version);

internal sealed record CatalogDependencyGroup(
    [property: JsonPropertyName("dependencies")] IReadOnlyList<CatalogDependency>? Dependencies);

internal sealed record CatalogDependency(
    [property: JsonPropertyName("id")] string Id);
