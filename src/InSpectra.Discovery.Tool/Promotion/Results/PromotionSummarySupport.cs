namespace InSpectra.Discovery.Tool.Promotion.Results;

using System.Text.Json.Nodes;

internal static class PromotionSummarySupport
{
    public static void IncrementSummaryCount(JsonObject summary, string? status)
    {
        switch (status)
        {
            case "success":
                summary["successCount"] = (summary["successCount"]?.GetValue<int>() ?? 0) + 1;
                break;
            case "terminal-negative":
                summary["terminalNegativeCount"] = (summary["terminalNegativeCount"]?.GetValue<int>() ?? 0) + 1;
                break;
            case "retryable-failure":
                summary["retryableFailureCount"] = (summary["retryableFailureCount"]?.GetValue<int>() ?? 0) + 1;
                break;
            case "terminal-failure":
                summary["terminalFailureCount"] = (summary["terminalFailureCount"]?.GetValue<int>() ?? 0) + 1;
                break;
        }
    }

    public static void UpdatePackageChangeSummary(JsonObject summary, JsonObject? existingPackageIndex, JsonObject result)
    {
        if (!string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal))
        {
            return;
        }

        if (existingPackageIndex is null)
        {
            ((JsonArray)summary["createdPackages"]!).Add(new JsonObject
            {
                ["packageId"] = result["packageId"]?.GetValue<string>(),
                ["version"] = result["version"]?.GetValue<string>(),
            });
            return;
        }

        var previousVersion = existingPackageIndex["latestVersion"]?.GetValue<string>();
        var newVersion = result["version"]?.GetValue<string>();
        if (!string.Equals(previousVersion, newVersion, StringComparison.OrdinalIgnoreCase))
        {
            ((JsonArray)summary["updatedPackages"]!).Add(new JsonObject
            {
                ["packageId"] = result["packageId"]?.GetValue<string>(),
                ["previousVersion"] = previousVersion,
                ["version"] = newVersion,
            });
        }
    }
}

