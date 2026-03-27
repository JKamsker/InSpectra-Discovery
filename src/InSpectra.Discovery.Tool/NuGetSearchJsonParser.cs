using System.Text.Json;

internal static class NuGetSearchJsonParser
{
    public static SearchResponse ParseSearchResponse(JsonElement root)
        => new(
            TotalHits: ParseRequiredInt32(root, "totalHits"),
            Data: NuGetJson.GetRequiredArray(root, "data", ParseSearchPackage));

    public static AutocompleteResponse ParseAutocompleteResponse(JsonElement root)
        => new(
            TotalHits: ParseRequiredInt32(root, "totalHits"),
            Data: NuGetJson.GetRequiredArray(root, "data", item =>
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw new JsonException($"Expected autocomplete entries to be strings but found {item.ValueKind}.");
                }

                return item.GetString() ?? string.Empty;
            }));

    private static SearchPackage ParseSearchPackage(JsonElement element)
        => new(
            Id: NuGetJson.GetRequiredString(element, "id"),
            TotalDownloads: ParseRequiredInt64(element, "totalDownloads"));

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

    private static long ParseRequiredInt64(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Required property '{propertyName}' was not present.");
        }

        if (!property.TryGetInt64(out var value))
        {
            throw new JsonException($"Expected property '{propertyName}' to be an integer.");
        }

        return value;
    }
}
