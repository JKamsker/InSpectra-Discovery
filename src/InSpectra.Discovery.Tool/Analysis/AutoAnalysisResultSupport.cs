using System.Text.Json.Nodes;

internal static class AutoAnalysisResultSupport
{
    public static JsonObject? LoadResult(string resultPath)
        => JsonNodeFileLoader.TryLoadJsonObject(resultPath);

    public static void ApplyDescriptor(JsonObject result, ToolAnalysisDescriptor descriptor, string analysisMode, JsonObject? fallbackResult)
    {
        result["analysisMode"] = analysisMode;
        result["analysisSelection"] = new JsonObject
        {
            ["preferredMode"] = descriptor.PreferredAnalysisMode,
            ["selectedMode"] = analysisMode,
            ["reason"] = descriptor.SelectionReason,
        };

        if (CliFrameworkProviderRegistry.ShouldReplace(result["cliFramework"]?.GetValue<string>(), descriptor.CliFramework))
        {
            result["cliFramework"] = descriptor.CliFramework;
        }
        else if (result["cliFramework"] is null && !string.IsNullOrWhiteSpace(descriptor.CliFramework))
        {
            result["cliFramework"] = descriptor.CliFramework;
        }

        if (result["command"] is null && !string.IsNullOrWhiteSpace(descriptor.CommandName))
        {
            result["command"] = descriptor.CommandName;
        }

        if (result["packageUrl"] is null)
        {
            result["packageUrl"] = descriptor.PackageUrl;
        }

        if (result["packageContentUrl"] is null && !string.IsNullOrWhiteSpace(descriptor.PackageContentUrl))
        {
            result["packageContentUrl"] = descriptor.PackageContentUrl;
        }

        if (result["catalogEntryUrl"] is null && !string.IsNullOrWhiteSpace(descriptor.CatalogEntryUrl))
        {
            result["catalogEntryUrl"] = descriptor.CatalogEntryUrl;
        }

        if (fallbackResult is null)
        {
            return;
        }

        result["fallback"] = new JsonObject
        {
            ["from"] = "native",
            ["disposition"] = fallbackResult["disposition"]?.GetValue<string>(),
            ["classification"] = fallbackResult["classification"]?.GetValue<string>(),
            ["message"] = fallbackResult["failureMessage"]?.GetValue<string>(),
        };
    }

    public static JsonObject CreateFailureResult(string packageId, string version, string batchId, int attempt, string source, string message)
        => new()
        {
            ["schemaVersion"] = 1,
            ["packageId"] = packageId,
            ["version"] = version,
            ["batchId"] = batchId,
            ["attempt"] = attempt,
            ["source"] = source,
            ["analyzedAt"] = DateTimeOffset.UtcNow.ToString("O"),
            ["disposition"] = "retryable-failure",
            ["phase"] = "selection",
            ["classification"] = "analysis-selection-failed",
            ["failureMessage"] = message,
            ["timings"] = new JsonObject { ["totalMs"] = null },
            ["steps"] = new JsonObject { ["install"] = null, ["opencli"] = null, ["xmldoc"] = null },
            ["artifacts"] = new JsonObject { ["opencliArtifact"] = null, ["xmldocArtifact"] = null },
        };

    public static Task<int> WriteResultAsync(
        string packageId,
        string version,
        string resultPath,
        JsonObject result,
        bool json,
        bool suppressOutput,
        CancellationToken cancellationToken)
    {
        if (suppressOutput)
        {
            return Task.FromResult(0);
        }

        return AnalysisCommandOutputSupport.WriteResultAsync(
            packageId,
            version,
            resultPath,
            result["disposition"]?.GetValue<string>(),
            json,
            cancellationToken,
            result["analysisMode"]?.GetValue<string>(),
            result["analysisSelection"]?["reason"]?.GetValue<string>(),
            result["fallback"]?["from"]?.GetValue<string>());
    }
}
