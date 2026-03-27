using System.Text.Json;

internal static class NuGetCatalogJsonParser
{
    public static NuGetServiceIndex ParseServiceIndex(JsonElement root)
        => new(NuGetJson.GetRequiredArray(root, "resources", ParseServiceResource));

    public static CatalogIndex ParseCatalogIndex(JsonElement root)
        => new(NuGetJson.GetRequiredArray(root, "items", ParseCatalogPageReference));

    public static CatalogPage ParseCatalogPage(JsonElement root)
        => new(NuGetJson.GetRequiredArray(root, "items", ParseCatalogPageItem));

    public static CatalogLeaf ParseCatalogLeaf(JsonElement root)
        => new(
            Id: NuGetJson.GetOptionalString(root, "@id") ?? string.Empty,
            ProjectUrl: NuGetJson.GetOptionalString(root, "projectUrl"),
            Repository: ParseRepository(root, "repository"),
            PackageEntries: NuGetJson.GetOptionalArray(root, "packageEntries", ParseCatalogPackageEntry),
            DependencyGroups: NuGetJson.GetOptionalArray(root, "dependencyGroups", ParseCatalogDependencyGroup),
            PackageTypes: NuGetJson.GetOptionalClonedElement(root, "packageTypes"));

    private static NuGetServiceResource ParseServiceResource(JsonElement element)
        => new(
            Id: NuGetJson.GetRequiredString(element, "@id"),
            Type: ParseTypeValue(element, "@type"));

    private static CatalogPageReference ParseCatalogPageReference(JsonElement element)
        => new(
            Id: NuGetJson.GetRequiredString(element, "@id"),
            CommitTimeStamp: NuGetJson.GetRequiredDateTimeOffset(element, "commitTimeStamp"));

    private static CatalogPageItem ParseCatalogPageItem(JsonElement element)
        => new(
            Id: NuGetJson.GetRequiredString(element, "@id"),
            Type: ParseTypeValue(element, "@type"),
            CommitTimeStamp: NuGetJson.GetRequiredDateTimeOffset(element, "commitTimeStamp"),
            PackageId: NuGetJson.GetRequiredString(element, "nuget:id"),
            PackageVersion: NuGetJson.GetRequiredString(element, "nuget:version"));

    private static CatalogPackageEntry ParseCatalogPackageEntry(JsonElement element)
        => new(
            FullName: NuGetJson.GetRequiredString(element, "fullName"),
            Name: NuGetJson.GetRequiredString(element, "name"));

    private static CatalogDependencyGroup ParseCatalogDependencyGroup(JsonElement element)
        => new(NuGetJson.GetOptionalArray(element, "dependencies", ParseCatalogDependency));

    private static CatalogDependency ParseCatalogDependency(JsonElement element)
        => new(NuGetJson.GetRequiredString(element, "id"));

    private static CatalogRepository? ParseRepository(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => string.IsNullOrWhiteSpace(property.GetString())
                ? null
                : new CatalogRepository(
                    Type: null,
                    Url: property.GetString(),
                    Commit: null),
            JsonValueKind.Object => new CatalogRepository(
                Type: NuGetJson.GetOptionalString(property, "type"),
                Url: NuGetJson.GetOptionalString(property, "url"),
                Commit: NuGetJson.GetOptionalString(property, "commit")),
            _ => throw new JsonException($"Expected property '{propertyName}' to be null, a string, or an object but found {property.ValueKind}."),
        };
    }

    private static string ParseTypeValue(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Required property '{propertyName}' was not present.");
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Array => property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                ?? throw new JsonException($"Expected property '{propertyName}' to contain at least one string entry."),
            _ => throw new JsonException($"Expected property '{propertyName}' to be a string or array but found {property.ValueKind}."),
        };
    }
}
