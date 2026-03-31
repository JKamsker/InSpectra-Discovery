namespace InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

using System.Text.Json.Nodes;

internal static class StaticAnalysisCliMetadataRestoreSupport
{
    public static void RestoreExistingCliMetadataEnrichment(JsonObject regenerated, JsonObject? existing)
    {
        if (existing is null
            || regenerated["info"] is not JsonObject regeneratedInfo
            || existing["info"] is not JsonObject existingInfo
            || existing["x-inspectra"] is not JsonObject existingInspectra)
        {
            return;
        }

        var regeneratedInspectra = regenerated["x-inspectra"] as JsonObject ?? new JsonObject();
        regenerated["x-inspectra"] = regeneratedInspectra;

        RestoreExistingCliMetadataEnrichment(
            regeneratedInfo,
            regeneratedInspectra,
            existingInfo,
            existingInspectra,
            infoPropertyName: "title",
            cliParsedPropertyName: "cliParsedTitle");
        RestoreExistingCliMetadataEnrichment(
            regeneratedInfo,
            regeneratedInspectra,
            existingInfo,
            existingInspectra,
            infoPropertyName: "description",
            cliParsedPropertyName: "cliParsedDescription");
    }

    private static void RestoreExistingCliMetadataEnrichment(
        JsonObject regeneratedInfo,
        JsonObject regeneratedInspectra,
        JsonObject existingInfo,
        JsonObject existingInspectra,
        string infoPropertyName,
        string cliParsedPropertyName)
    {
        var existingInfoValue = existingInfo[infoPropertyName]?.GetValue<string>();
        var existingCliParsedValue = existingInspectra[cliParsedPropertyName]?.GetValue<string>();
        var regeneratedInfoValue = regeneratedInfo[infoPropertyName]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(regeneratedInfoValue) && !string.IsNullOrWhiteSpace(existingInfoValue))
        {
            regeneratedInfo[infoPropertyName] = existingInfoValue;
            if (!string.IsNullOrWhiteSpace(existingCliParsedValue))
            {
                regeneratedInspectra[cliParsedPropertyName] = existingCliParsedValue;
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(existingInfoValue)
            || string.IsNullOrWhiteSpace(existingCliParsedValue)
            || string.IsNullOrWhiteSpace(regeneratedInfoValue)
            || !string.Equals(regeneratedInfoValue, existingCliParsedValue, StringComparison.Ordinal))
        {
            return;
        }

        regeneratedInfo[infoPropertyName] = existingInfoValue;
        regeneratedInspectra[cliParsedPropertyName] = existingCliParsedValue;
    }
}

