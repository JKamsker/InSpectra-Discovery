namespace InSpectra.Discovery.Tool.Infrastructure;

using System.Text.Json.Nodes;

internal static class CrawlArtifactBuilder
{
    public static JsonObject Build(
        int documentCount,
        IReadOnlyDictionary<string, JsonObject> captures,
        JsonObject? metadata = null)
    {
        var artifact = new JsonObject
        {
            ["documentCount"] = documentCount,
            ["commandCount"] = documentCount,
            ["captureCount"] = captures.Count,
            ["commands"] = new JsonArray(captures
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => pair.Value.DeepClone())
                .ToArray()),
        };

        if (metadata is null)
        {
            return artifact;
        }

        foreach (var property in metadata)
        {
            artifact[property.Key] = property.Value?.DeepClone();
        }

        return artifact;
    }
}
