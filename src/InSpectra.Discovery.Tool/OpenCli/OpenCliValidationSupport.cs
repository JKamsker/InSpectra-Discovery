using System.Text.Json.Nodes;

internal static class OpenCliValidationSupport
{
    public static bool TryValidateArrayProperty(JsonObject node, string propertyName, string path, out string? reason)
    {
        reason = null;

        if (!node.TryGetPropertyValue(propertyName, out var value))
        {
            return true;
        }

        if (value is null)
        {
            reason = $"OpenCLI artifact has a null '{propertyName}' property at '{path}'.";
            return false;
        }

        if (value is not JsonArray)
        {
            reason = $"OpenCLI artifact has a non-array '{propertyName}' property at '{path}'.";
            return false;
        }

        return true;
    }

    public static bool TryValidateStringEntries(JsonArray array, string path, out string? reason)
    {
        reason = null;

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonValue value || !value.TryGetValue<string>(out _))
            {
                reason = $"OpenCLI artifact has a non-string entry at '{path}[{index}]'.";
                return false;
            }
        }

        return true;
    }

    public static string? GetString(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
}
