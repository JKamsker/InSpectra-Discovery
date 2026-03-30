using System.Text.Json.Nodes;

internal static class JsonNodeFileLoader
{
    public static JsonNode? TryLoadJsonNode(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    public static JsonObject? TryLoadJsonObject(string path)
        => TryLoadJsonNode(path) as JsonObject;

    public static JsonArray? TryLoadJsonArray(string path)
        => TryLoadJsonNode(path) as JsonArray;

    public static async Task<JsonNode?> TryLoadJsonNodeAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken));
        }
        catch
        {
            return null;
        }
    }

    public static async Task<JsonObject?> TryLoadJsonObjectAsync(string path, CancellationToken cancellationToken)
        => await TryLoadJsonNodeAsync(path, cancellationToken) as JsonObject;

    public static async Task<JsonArray?> TryLoadJsonArrayAsync(string path, CancellationToken cancellationToken)
        => await TryLoadJsonNodeAsync(path, cancellationToken) as JsonArray;
}
