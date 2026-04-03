namespace InSpectra.Discovery.Tool.Infrastructure.Json;

using System.Text.Json.Nodes;

internal static class JsonDocumentStabilitySupport
{
    public static bool TryPreserveTopLevelProperties(
        JsonObject candidate,
        JsonObject? existing,
        params string[] propertyNames)
    {
        if (existing is null || !AreEquivalentIgnoringTopLevelProperties(candidate, existing, propertyNames))
        {
            return false;
        }

        foreach (var propertyName in propertyNames)
        {
            if (existing.ContainsKey(propertyName))
            {
                candidate[propertyName] = existing[propertyName]?.DeepClone();
            }
            else
            {
                candidate.Remove(propertyName);
            }
        }

        return true;
    }

    public static bool AreEquivalentIgnoringTopLevelProperties(
        JsonObject? left,
        JsonObject? right,
        params string[] propertyNames)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        var ignoredProperties = propertyNames.ToHashSet(StringComparer.Ordinal);
        return JsonNode.DeepEquals(
            CloneWithoutTopLevelProperties(left, ignoredProperties),
            CloneWithoutTopLevelProperties(right, ignoredProperties));
    }

    private static JsonObject CloneWithoutTopLevelProperties(
        JsonObject source,
        IReadOnlySet<string> ignoredProperties)
    {
        var clone = new JsonObject();

        foreach (var property in source)
        {
            if (ignoredProperties.Contains(property.Key))
            {
                continue;
            }

            var normalizedValue = Normalize(property.Value);
            if (normalizedValue is not null)
            {
                clone[property.Key] = normalizedValue;
            }
        }

        return clone;
    }

    private static JsonNode? Normalize(JsonNode? node)
        => node switch
        {
            null => null,
            JsonObject obj => NormalizeObject(obj),
            JsonArray array => NormalizeArray(array),
            _ => node.DeepClone(),
        };

    private static JsonObject NormalizeObject(JsonObject source)
    {
        var normalized = new JsonObject();
        foreach (var property in source)
        {
            var normalizedValue = Normalize(property.Value);
            if (normalizedValue is not null)
            {
                normalized[property.Key] = normalizedValue;
            }
        }

        return normalized;
    }

    private static JsonArray NormalizeArray(JsonArray source)
    {
        var normalized = new JsonArray();
        foreach (var item in source)
        {
            normalized.Add(Normalize(item));
        }

        return normalized;
    }
}
