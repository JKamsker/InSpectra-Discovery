using System.Text.Json;

internal static class NuGetJson
{
    public static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Required property '{propertyName}' was not present.");
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw new JsonException($"Expected property '{propertyName}' to be a string but found {property.ValueKind}.");
        }

        return property.GetString() ?? string.Empty;
    }

    public static string? GetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => property.GetString(),
            _ => throw new JsonException($"Expected property '{propertyName}' to be a string but found {property.ValueKind}."),
        };
    }

    public static bool? GetOptionalBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new JsonException($"Expected property '{propertyName}' to be a boolean but found {property.ValueKind}."),
        };
    }

    public static DateTimeOffset GetRequiredDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Required property '{propertyName}' was not present.");
        }

        if (property.ValueKind != JsonValueKind.String || !property.TryGetDateTimeOffset(out var value))
        {
            throw new JsonException($"Expected property '{propertyName}' to be an ISO-8601 date-time string.");
        }

        return value;
    }

    public static DateTimeOffset? GetOptionalDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String || !property.TryGetDateTimeOffset(out var value))
        {
            throw new JsonException($"Expected property '{propertyName}' to be an ISO-8601 date-time string.");
        }

        return value;
    }

    public static IReadOnlyList<T> GetRequiredArray<T>(JsonElement element, string propertyName, Func<JsonElement, T> converter)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new JsonException($"Required property '{propertyName}' was not present.");
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Expected property '{propertyName}' to be an array but found {property.ValueKind}.");
        }

        var values = new List<T>();
        foreach (var item in property.EnumerateArray())
        {
            values.Add(converter(item));
        }

        return values;
    }

    public static IReadOnlyList<T>? GetOptionalArray<T>(JsonElement element, string propertyName, Func<JsonElement, T> converter)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException($"Expected property '{propertyName}' to be an array but found {property.ValueKind}.");
        }

        var values = new List<T>();
        foreach (var item in property.EnumerateArray())
        {
            values.Add(converter(item));
        }

        return values;
    }

    public static JsonElement? GetOptionalClonedElement(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind == JsonValueKind.Null
            ? null
            : property.Clone();
    }
}
