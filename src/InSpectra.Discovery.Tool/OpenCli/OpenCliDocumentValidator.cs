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

        if (!TryValidateDocument(parsedDocument, out reason))
        {
            return false;
        }

        document = parsedDocument;
        return true;
    }

    public static bool TryValidateDocument(JsonObject document, out string? reason)
    {
        reason = null;

        if (string.IsNullOrWhiteSpace(OpenCliValidationSupport.GetString(document["opencli"])))
        {
            reason = "OpenCLI artifact is missing the root 'opencli' marker.";
            return false;
        }

        if (!TryValidateInfo(document, out reason))
        {
            return false;
        }

        if (!OpenCliNodeValidationSupport.TryValidateCommandLikeNode(document, "$", isRoot: true, out reason))
        {
            return false;
        }

        if (!OpenCliDocumentPublishabilityInspector.HasPublishableSurface(document))
        {
            reason = "OpenCLI artifact does not expose any commands, options, or arguments.";
            return false;
        }

        if (OpenCliDocumentPublishabilityInspector.LooksLikeInventoryOnlyCommandShellDocument(document))
        {
            reason = "OpenCLI artifact only exposes root command inventory shells without any detailed command surface.";
            return false;
        }

        if (OpenCliDocumentPublishabilityInspector.ContainsErrorText(document))
        {
            reason = "OpenCLI artifact contains error or exception text in its surface descriptions.";
            return false;
        }

        var totalCommandCount = OpenCliDocumentPublishabilityInspector.CountTotalCommands(document);
        if (totalCommandCount > 500)
        {
            reason = $"OpenCLI artifact has an implausible command count ({totalCommandCount}).";
            return false;
        }

        if (OpenCliDocumentPublishabilityInspector.ContainsBoxDrawingCommandNames(document))
        {
            reason = "OpenCLI artifact contains box-drawing characters in command names (table art parsed as commands).";
            return false;
        }

        return true;
    }

    private static bool TryValidateInfo(JsonObject document, out string? reason)
    {
        reason = null;

        if (!document.TryGetPropertyValue("info", out var infoNode) || infoNode is null)
        {
            return true;
        }

        if (infoNode is not JsonObject info)
        {
            reason = "OpenCLI artifact has a non-object 'info' property.";
            return false;
        }

        var title = OpenCliValidationSupport.GetString(info["title"]);
        if (OpenCliDocumentPublishabilityInspector.LooksLikeNonPublishableTitle(title))
        {
            reason = "OpenCLI artifact has a non-publishable 'info.title' value.";
            return false;
        }

        var description = OpenCliValidationSupport.GetString(info["description"]);
        if (OpenCliDocumentPublishabilityInspector.LooksLikeNonPublishableDescription(description))
        {
            reason = "OpenCLI artifact has a non-publishable 'info.description' value.";
            return false;
        }

        return true;
    }
}
