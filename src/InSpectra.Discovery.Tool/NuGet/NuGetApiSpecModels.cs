namespace InSpectra.Discovery.Tool.NuGet;


using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record NuGetServiceIndexSpec(
    [property: JsonPropertyName("resources")] IReadOnlyList<NuGetServiceResourceSpec>? Resources);

internal sealed record NuGetServiceResourceSpec(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("@type")]
    [property: JsonConverter(typeof(NuGetTypeValueJsonConverter))]
    string? Type);

internal sealed record CatalogIndexSpec(
    [property: JsonPropertyName("items")] IReadOnlyList<CatalogPageReferenceSpec>? Items);

internal sealed record CatalogPageReferenceSpec(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("commitTimeStamp")] DateTimeOffset? CommitTimeStamp);

internal sealed record CatalogPageSpec(
    [property: JsonPropertyName("items")] IReadOnlyList<CatalogPageItemSpec>? Items);

internal sealed record CatalogPageItemSpec(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("@type")]
    [property: JsonConverter(typeof(NuGetTypeValueJsonConverter))]
    string? Type,
    [property: JsonPropertyName("commitTimeStamp")] DateTimeOffset? CommitTimeStamp,
    [property: JsonPropertyName("nuget:id")] string? PackageId,
    [property: JsonPropertyName("nuget:version")] string? PackageVersion);

internal sealed record SearchResponseSpec(
    [property: JsonPropertyName("totalHits")] int? TotalHits,
    [property: JsonPropertyName("data")] IReadOnlyList<SearchPackageSpec>? Data);

internal sealed record SearchPackageSpec(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("totalDownloads")] long? TotalDownloads);

internal sealed record AutocompleteResponseSpec(
    [property: JsonPropertyName("totalHits")] int? TotalHits,
    [property: JsonPropertyName("data")] IReadOnlyList<string>? Data);

internal sealed record RegistrationIndexSpec(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("items")] IReadOnlyList<RegistrationPageReferenceSpec>? Items);

internal sealed record RegistrationPageReferenceSpec(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("count")] int? Count,
    [property: JsonPropertyName("items")] IReadOnlyList<RegistrationPageLeafSpec>? Items);

internal sealed record RegistrationPageSpec(
    [property: JsonPropertyName("items")] IReadOnlyList<RegistrationPageLeafSpec>? Items);

internal sealed record RegistrationPageLeafSpec(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("commitTimeStamp")] DateTimeOffset? CommitTimeStamp,
    [property: JsonPropertyName("catalogEntry")] JsonElement? CatalogEntry,
    [property: JsonPropertyName("packageContent")] string? PackageContent);

internal sealed record RegistrationLeafDocumentSpec(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("catalogEntry")] string? CatalogEntryUrl,
    [property: JsonPropertyName("listed")] bool? Listed,
    [property: JsonPropertyName("packageContent")] string? PackageContent,
    [property: JsonPropertyName("published")] DateTimeOffset? Published);

internal sealed record CatalogEntrySpec(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("authors")] string? Authors,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("licenseExpression")] string? LicenseExpression,
    [property: JsonPropertyName("licenseUrl")] string? LicenseUrl,
    [property: JsonPropertyName("listed")] bool? Listed,
    [property: JsonPropertyName("projectUrl")] string? ProjectUrl,
    [property: JsonPropertyName("published")] DateTimeOffset? Published,
    [property: JsonPropertyName("repository")]
    [property: JsonConverter(typeof(NuGetRepositoryJsonConverter))]
    CatalogRepositorySpec? Repository,
    [property: JsonPropertyName("readmeUrl")] string? ReadmeUrl,
    [property: JsonPropertyName("version")] string? Version);

internal sealed record CatalogLeafSpec(
    [property: JsonPropertyName("@id")] string? Id,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("projectUrl")] string? ProjectUrl,
    [property: JsonPropertyName("repository")]
    [property: JsonConverter(typeof(NuGetRepositoryJsonConverter))]
    CatalogRepositorySpec? Repository,
    [property: JsonPropertyName("packageEntries")] IReadOnlyList<CatalogPackageEntrySpec>? PackageEntries,
    [property: JsonPropertyName("dependencyGroups")] IReadOnlyList<CatalogDependencyGroupSpec>? DependencyGroups,
    [property: JsonPropertyName("packageTypes")] JsonElement? PackageTypes);

internal sealed record CatalogPackageEntrySpec(
    [property: JsonPropertyName("fullName")] string? FullName,
    [property: JsonPropertyName("name")] string? Name);

internal sealed record CatalogDependencyGroupSpec(
    [property: JsonPropertyName("dependencies")] IReadOnlyList<CatalogDependencySpec>? Dependencies);

internal sealed record CatalogDependencySpec(
    [property: JsonPropertyName("id")] string? Id);

internal sealed record CatalogRepositorySpec(
    string? Type,
    string? Url,
    string? Commit);

internal sealed record CatalogRepositoryObjectSpec(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("commit")] string? Commit);

