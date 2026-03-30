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
}
