using System.Text.Json;

internal static class NuGetApiModelMapper
{
    public static NuGetServiceIndex ToModel(NuGetServiceIndexSpec spec)
        => new(RequiredList(spec.Resources, "resources", ToModel));

    public static CatalogIndex ToModel(CatalogIndexSpec spec)
        => new(RequiredList(spec.Items, "items", ToModel));

    public static CatalogPage ToModel(CatalogPageSpec spec)
        => new(RequiredList(spec.Items, "items", ToModel));

    public static SearchResponse ToModel(SearchResponseSpec spec)
        => new(
            TotalHits: RequiredInt32(spec.TotalHits, "totalHits"),
            Data: RequiredList(spec.Data, "data", ToModel));

    public static AutocompleteResponse ToModel(AutocompleteResponseSpec spec)
        => new(
            TotalHits: RequiredInt32(spec.TotalHits, "totalHits"),
            Data: RequiredList(spec.Data, "data", value => value));

    public static RegistrationIndex ToModel(RegistrationIndexSpec spec)
        => new(
            Id: RequiredString(spec.Id, "@id"),
            Items: RequiredList(spec.Items, "items", ToModel));

    public static RegistrationPage ToModel(RegistrationPageSpec spec)
        => new(RequiredList(spec.Items, "items", ToModel));

    public static RegistrationLeafDocument ToModel(RegistrationLeafDocumentSpec spec)
        => new(
            Id: spec.Id,
            CatalogEntryUrl: RequiredString(spec.CatalogEntryUrl, "catalogEntry"),
            Listed: spec.Listed,
            PackageContent: RequiredString(spec.PackageContent, "packageContent"),
            Published: spec.Published);

    public static CatalogLeaf ToModel(CatalogLeafSpec spec)
        => new(
            Id: spec.Id ?? string.Empty,
            ProjectUrl: spec.ProjectUrl,
            Repository: ToModel(spec.Repository),
            PackageEntries: OptionalList(spec.PackageEntries, ToModel),
            DependencyGroups: OptionalList(spec.DependencyGroups, ToModel),
            PackageTypes: spec.PackageTypes);

    private static NuGetServiceResource ToModel(NuGetServiceResourceSpec spec)
        => new(
            Id: RequiredString(spec.Id, "@id"),
            Type: RequiredString(spec.Type, "@type"));

    private static CatalogPageReference ToModel(CatalogPageReferenceSpec spec)
        => new(
            Id: RequiredString(spec.Id, "@id"),
            CommitTimeStamp: RequiredDateTimeOffset(spec.CommitTimeStamp, "commitTimeStamp"));

    private static CatalogPageItem ToModel(CatalogPageItemSpec spec)
        => new(
            Id: RequiredString(spec.Id, "@id"),
            Type: RequiredString(spec.Type, "@type"),
            CommitTimeStamp: RequiredDateTimeOffset(spec.CommitTimeStamp, "commitTimeStamp"),
            PackageId: RequiredString(spec.PackageId, "nuget:id"),
            PackageVersion: RequiredString(spec.PackageVersion, "nuget:version"));

    private static SearchPackage ToModel(SearchPackageSpec spec)
        => new(
            Id: RequiredString(spec.Id, "id"),
            TotalDownloads: RequiredInt64(spec.TotalDownloads, "totalDownloads"));

    private static RegistrationPageReference ToModel(RegistrationPageReferenceSpec spec)
        => new(
            Id: RequiredString(spec.Id, "@id"),
            Count: RequiredInt32(spec.Count, "count"),
            Items: OptionalList(spec.Items, ToModel));

    private static RegistrationPageLeaf ToModel(RegistrationPageLeafSpec spec)
    {
        var (catalogEntry, hasEmbeddedCatalogEntry) = ToRegistrationPageCatalogEntry(spec.CatalogEntry, spec.Id, spec.PackageContent);
        return new RegistrationPageLeaf(
            Id: spec.Id,
            CommitTimeStamp: RequiredDateTimeOffset(spec.CommitTimeStamp, "commitTimeStamp"),
            CatalogEntry: catalogEntry,
            PackageContent: RequiredString(spec.PackageContent, "packageContent"),
            HasEmbeddedCatalogEntry: hasEmbeddedCatalogEntry);
    }

    private static CatalogEntry ToModel(CatalogEntrySpec spec)
        => new(
            Id: RequiredString(spec.Id, "@id"),
            Authors: spec.Authors,
            Description: spec.Description,
            LicenseExpression: spec.LicenseExpression,
            LicenseUrl: spec.LicenseUrl,
            Listed: spec.Listed,
            ProjectUrl: spec.ProjectUrl,
            Published: spec.Published,
            Repository: ToModel(spec.Repository),
            ReadmeUrl: spec.ReadmeUrl,
            Version: RequiredString(spec.Version, "version"));

    private static (CatalogEntry CatalogEntry, bool HasEmbeddedCatalogEntry) ToRegistrationPageCatalogEntry(
        JsonElement? catalogEntryElement,
        string? leafId,
        string? packageContent)
    {
        if (catalogEntryElement is null)
        {
            throw new JsonException("Required property 'catalogEntry' was not present.");
        }

        return catalogEntryElement.Value.ValueKind switch
        {
            JsonValueKind.Object => (
                ToModel(JsonSerializer.Deserialize<CatalogEntrySpec>(catalogEntryElement.Value.GetRawText())
                    ?? throw new JsonException("Required property 'catalogEntry' was null.")),
                true),
            JsonValueKind.String => (
                new CatalogEntry(
                    Id: catalogEntryElement.Value.GetString()
                        ?? throw new JsonException("Required property 'catalogEntry' was not present."),
                    Authors: null,
                    Description: null,
                    LicenseExpression: null,
                    LicenseUrl: null,
                    Listed: null,
                    ProjectUrl: null,
                    Published: null,
                    Repository: null,
                    ReadmeUrl: null,
                    Version: ExtractRegistrationPageVersion(leafId, packageContent)),
                false),
            _ => throw new JsonException("Required property 'catalogEntry' was not an object or string."),
        };
    }

    private static string ExtractRegistrationPageVersion(string? leafId, string? packageContent)
        => ExtractVersionFromLeafUrl(leafId)
            ?? ExtractVersionFromPackageContent(packageContent)
            ?? throw new JsonException("Could not infer version from registration page leaf.");

    private static string? ExtractVersionFromLeafUrl(string? leafId)
    {
        if (string.IsNullOrWhiteSpace(leafId) || !Uri.TryCreate(leafId, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var segment = uri.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrWhiteSpace(segment) || !segment.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? null
            : segment[..^5];
    }

    private static string? ExtractVersionFromPackageContent(string? packageContent)
    {
        if (string.IsNullOrWhiteSpace(packageContent) || !Uri.TryCreate(packageContent, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var fileName = Path.GetFileName(uri.LocalPath);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var trimmed = fileName[..^6];
        var lastDot = trimmed.LastIndexOf('.');
        return lastDot > 0 && lastDot < trimmed.Length - 1
            ? trimmed[(lastDot + 1)..]
            : null;
    }

    private static CatalogRepository? ToModel(CatalogRepositorySpec? spec)
        => spec is null
            ? null
            : new CatalogRepository(spec.Type, spec.Url, spec.Commit);

    private static CatalogPackageEntry ToModel(CatalogPackageEntrySpec spec)
        => new(
            FullName: RequiredString(spec.FullName, "fullName"),
            Name: RequiredString(spec.Name, "name"));

    private static CatalogDependencyGroup ToModel(CatalogDependencyGroupSpec spec)
        => new(OptionalList(spec.Dependencies, ToModel));

    private static CatalogDependency ToModel(CatalogDependencySpec spec)
        => new(RequiredString(spec.Id, "id"));

    private static IReadOnlyList<TModel> RequiredList<TSpec, TModel>(
        IReadOnlyList<TSpec>? values,
        string propertyName,
        Func<TSpec, TModel> map)
    {
        if (values is null)
        {
            throw new JsonException($"Required property '{propertyName}' was not present.");
        }

        return values.Select(map).ToArray();
    }

    private static IReadOnlyList<TModel>? OptionalList<TSpec, TModel>(
        IReadOnlyList<TSpec>? values,
        Func<TSpec, TModel> map)
        => values?.Select(map).ToArray();

    private static T RequiredObject<T>(T? value, string propertyName)
        where T : class
        => value ?? throw new JsonException($"Required property '{propertyName}' was not present.");

    private static string RequiredString(string? value, string propertyName)
        => value ?? throw new JsonException($"Required property '{propertyName}' was not present.");

    private static int RequiredInt32(int? value, string propertyName)
        => value ?? throw new JsonException($"Required property '{propertyName}' was not present.");

    private static long RequiredInt64(long? value, string propertyName)
        => value ?? throw new JsonException($"Required property '{propertyName}' was not present.");

    private static DateTimeOffset RequiredDateTimeOffset(DateTimeOffset? value, string propertyName)
        => value ?? throw new JsonException($"Required property '{propertyName}' was not present.");
}
