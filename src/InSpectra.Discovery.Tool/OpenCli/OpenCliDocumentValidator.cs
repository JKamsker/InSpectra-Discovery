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

        document = parsedDocument;
        return true;
    }
}
