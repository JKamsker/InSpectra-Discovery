using System.Text.Json;

internal static class DotnetToolPackageType
{
    public static bool IsDotnetTool(CatalogLeaf leaf)
        => EnumeratePackageTypes(leaf.PackageTypes)
            .Any(packageType => string.Equals(packageType.Name, "DotnetTool", StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<CatalogPackageType> EnumeratePackageTypes(JsonElement? packageTypes)
    {
        if (!packageTypes.HasValue)
        {
            yield break;
        }

        foreach (var packageType in EnumeratePackageTypeElements(packageTypes.Value))
        {
            if (TryParsePackageType(packageType, out var value))
            {
                yield return value;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumeratePackageTypeElements(JsonElement packageTypes)
    {
        if (packageTypes.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in packageTypes.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        if (packageTypes.ValueKind == JsonValueKind.Object)
        {
            yield return packageTypes;
        }
    }

    private static bool TryParsePackageType(JsonElement element, out CatalogPackageType packageType)
    {
        if (!element.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
        {
            packageType = default!;
            return false;
        }

        packageType = new CatalogPackageType(
            Name: nameElement.GetString() ?? string.Empty,
            Version: element.TryGetProperty("version", out var versionElement) && versionElement.ValueKind == JsonValueKind.String
                ? versionElement.GetString()
                : null);
        return true;
    }
}
