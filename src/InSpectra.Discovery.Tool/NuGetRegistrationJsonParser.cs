using System.Text.Json;

internal static class NuGetRegistrationJsonParser
{
    public static RegistrationIndex ParseRegistrationIndex(JsonElement root)
        => new(
            Id: NuGetJson.GetRequiredString(root, "@id"),
            Items: NuGetJson.GetRequiredArray(root, "items", ParseRegistrationPageReference));

    public static RegistrationPage ParseRegistrationPage(JsonElement root)
        => new(NuGetJson.GetRequiredArray(root, "items", ParseRegistrationPageLeaf));

    public static RegistrationLeafDocument ParseRegistrationLeaf(JsonElement root)
        => new(
            Id: NuGetJson.GetOptionalString(root, "@id"),
            CatalogEntryUrl: NuGetJson.GetRequiredString(root, "catalogEntry"),
            Listed: NuGetJson.GetOptionalBoolean(root, "listed"),
            PackageContent: NuGetJson.GetRequiredString(root, "packageContent"),
            Published: NuGetJson.GetOptionalDateTimeOffset(root, "published"));

    private static RegistrationPageReference ParseRegistrationPageReference(JsonElement element)
        => new(
            Id: NuGetJson.GetRequiredString(element, "@id"),
            Count: ParseRequiredInt32(element, "count"),
            Items: NuGetJson.GetOptionalArray(element, "items", ParseRegistrationPageLeaf));

    private static RegistrationPageLeaf ParseRegistrationPageLeaf(JsonElement element)
        => new(
            Id: NuGetJson.GetOptionalString(element, "@id"),
            CommitTimeStamp: NuGetJson.GetRequiredDateTimeOffset(element, "commitTimeStamp"),
            CatalogEntry: ParseCatalogEntry(GetRequiredObject(element, "catalogEntry")),
            PackageContent: NuGetJson.GetRequiredString(element, "packageContent"));

    private static CatalogEntry ParseCatalogEntry(JsonElement element)
        => new(
            Id: NuGetJson.GetRequiredString(element, "@id"),
            Authors: NuGetJson.GetOptionalString(element, "authors"),
            Description: NuGetJson.GetOptionalString(element, "description"),
            LicenseExpression: NuGetJson.GetOptionalString(element, "licenseExpression"),
            LicenseUrl: NuGetJson.GetOptionalString(element, "licenseUrl"),
            Listed: NuGetJson.GetOptionalBoolean(element, "listed"),
            ProjectUrl: NuGetJson.GetOptionalString(element, "projectUrl"),
            Published: NuGetJson.GetOptionalDateTimeOffset(element, "published"),
            Repository: ParseRepository(element),
            ReadmeUrl: NuGetJson.GetOptionalString(element, "readmeUrl"),
            Version: NuGetJson.GetRequiredString(element, "version"));

    private static CatalogRepository? ParseRepository(JsonElement element)
    {
        if (!element.TryGetProperty("repository", out var property))
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
            _ => throw new JsonException($"Expected property 'repository' to be null, a string, or an object but found {property.ValueKind}."),
        };
    }

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Required property '{propertyName}' was not present.");
        }

        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Expected property '{propertyName}' to be an object but found {property.ValueKind}.");
        }

        return property;
    }

    private static int ParseRequiredInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Required property '{propertyName}' was not present.");
        }

        if (!property.TryGetInt32(out var value))
        {
            throw new JsonException($"Expected property '{propertyName}' to be an integer.");
        }

        return value;
    }
}
