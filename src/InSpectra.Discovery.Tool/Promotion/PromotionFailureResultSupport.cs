using System.Text.Json.Nodes;

internal static class PromotionFailureResultSupport
{
    public static JsonObject NewSyntheticFailureResult(JsonObject item, int attempt, string classification, string message, string batchId, DateTimeOffset now)
        => new()
        {
            ["schemaVersion"] = 1,
            ["packageId"] = item["packageId"]?.GetValue<string>(),
            ["version"] = item["version"]?.GetValue<string>(),
            ["batchId"] = batchId,
            ["attempt"] = attempt,
            ["trusted"] = false,
            ["source"] = "workflow_run",
            ["analyzedAt"] = now.ToString("O"),
            ["disposition"] = "retryable-failure",
            ["retryEligible"] = true,
            ["phase"] = "infra",
            ["classification"] = classification,
            ["failureMessage"] = message,
            ["failureSignature"] = $"infra|{classification}|{message}",
            ["packageUrl"] = item["packageUrl"]?.GetValue<string>(),
            ["totalDownloads"] = item["totalDownloads"]?.GetValue<long?>(),
            ["packageContentUrl"] = item["packageContentUrl"]?.GetValue<string>(),
            ["registrationLeafUrl"] = null,
            ["catalogEntryUrl"] = item["catalogEntryUrl"]?.GetValue<string>(),
            ["command"] = null,
            ["entryPoint"] = null,
            ["runner"] = null,
            ["toolSettingsPath"] = null,
            ["publishedAt"] = null,
            ["detection"] = new JsonObject
            {
                ["hasSpectreConsole"] = false,
                ["hasSpectreConsoleCli"] = false,
                ["matchedPackageEntries"] = new JsonArray(),
                ["matchedDependencyIds"] = new JsonArray(),
            },
            ["introspection"] = new JsonObject
            {
                ["opencli"] = null,
                ["xmldoc"] = null,
            },
            ["timings"] = new JsonObject
            {
                ["totalMs"] = null,
                ["installMs"] = null,
                ["opencliMs"] = null,
                ["xmldocMs"] = null,
            },
            ["steps"] = new JsonObject
            {
                ["install"] = null,
                ["opencli"] = null,
                ["xmldoc"] = null,
            },
            ["artifacts"] = new JsonObject
            {
                ["opencliArtifact"] = null,
                ["xmldocArtifact"] = null,
            },
        };

    public static string GetNonSuccessReason(JsonObject result, JsonObject stateRecord)
        => result["failureMessage"]?.GetValue<string>() ??
           stateRecord["lastFailureMessage"]?.GetValue<string>() ??
           GetDefaultReasonMessage(stateRecord["currentStatus"]?.GetValue<string>(), result["classification"]?.GetValue<string>());

    private static string GetDefaultReasonMessage(string? status, string? classification)
        => classification switch
        {
            "spectre-cli-missing" => "No Spectre.Console.Cli evidence was found in the published package.",
            "missing-result-artifact" => "No result artifact was uploaded for this matrix item.",
            "missing-success-artifact" => "The analyzer reported success, but the expected success artifact was missing.",
            "missing-result" => "No result was recorded for this matrix item.",
            "environment-missing-runtime" => "The runner did not have the .NET runtime required by this tool.",
            "environment-missing-dependency" => "The tool required a native dependency that is not available on the runner.",
            "requires-interactive-input" => "The tool attempted to prompt for interactive input, which is not available in batch mode.",
            "requires-interactive-authentication" => "The tool attempted an interactive authentication flow.",
            "unsupported-platform" => "The tool does not support the runner operating system.",
            "unsupported-command" => "The tool does not implement the expected introspection command.",
            "invalid-json" => "The tool exited, but its JSON output could not be parsed.",
            _ when string.Equals(status, "terminal-negative", StringComparison.Ordinal) => "The package did not satisfy the selected analyzer preconditions.",
            _ => "No explicit reason was recorded.",
        };
}
