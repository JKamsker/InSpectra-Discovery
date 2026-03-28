using System.Text.Json.Nodes;

internal static class OpenCliDocumentValidator
{
    public static bool TryLoadValidDocument(string path, out JsonObject? document, out string? reason)
    {
        document = null;
        reason = null;

        if (!PromotionArtifactSupport.TryLoadJsonObject(path, out var parsedDocument) || parsedDocument is null)
        {
            reason = "OpenCLI artifact is not a JSON object.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsedDocument["opencli"]?.GetValue<string>()))
        {
            reason = "OpenCLI artifact is missing the root 'opencli' marker.";
            return false;
        }

        if (parsedDocument["info"] is not null && parsedDocument["info"] is not JsonObject)
        {
            reason = "OpenCLI artifact has a non-object 'info' property.";
            return false;
        }

        foreach (var arrayProperty in new[] { "arguments", "commands", "options" })
        {
            if (parsedDocument[arrayProperty] is not null && parsedDocument[arrayProperty] is not JsonArray)
            {
                reason = $"OpenCLI artifact has a non-array '{arrayProperty}' property.";
                return false;
            }
        }

        if (!TryValidateCommandLikeNode(parsedDocument, "$", out reason))
        {
            return false;
        }

        document = parsedDocument;
        return true;
    }

    private static bool TryValidateCommandLikeNode(JsonObject node, string path, out string? reason)
    {
        reason = null;

        foreach (var arrayProperty in new[] { "arguments", "commands", "options" })
        {
            if (node[arrayProperty] is not JsonArray array)
            {
                continue;
            }

            for (var index = 0; index < array.Count; index++)
            {
                if (array[index] is not JsonObject child)
                {
                    reason = $"OpenCLI artifact has a non-object entry at '{path}.{arrayProperty}[{index}]'.";
                    return false;
                }

                if (string.Equals(arrayProperty, "commands", StringComparison.Ordinal)
                    && !TryValidateCommandLikeNode(child, $"{path}.{arrayProperty}[{index}]", out reason))
                {
                    return false;
                }
            }
        }

        if (node["examples"] is JsonArray examples)
        {
            for (var index = 0; index < examples.Count; index++)
            {
                if (examples[index] is not JsonValue value || !value.TryGetValue<string>(out _))
                {
                    reason = $"OpenCLI artifact has a non-string entry at '{path}.examples[{index}]'.";
                    return false;
                }
            }
        }
        else if (node["examples"] is not null)
        {
            reason = $"OpenCLI artifact has a non-array 'examples' property at '{path}'.";
            return false;
        }

        return true;
    }
}
