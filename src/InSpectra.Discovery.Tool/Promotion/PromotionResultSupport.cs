using System.Text.Json.Nodes;

internal static class PromotionResultSupport
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

    public static void MergePlanItemIntoResult(JsonObject item, JsonObject result)
    {
        if (result["totalDownloads"] is null && item["totalDownloads"] is not null)
        {
            result["totalDownloads"] = item["totalDownloads"]?.DeepClone();
        }

        if (result["command"] is null && item["command"] is not null)
        {
            result["command"] = item["command"]?.DeepClone();
        }

        if (CliFrameworkSupport.ShouldReplace(result["cliFramework"]?.GetValue<string>(), item["cliFramework"]?.GetValue<string>()))
        {
            result["cliFramework"] = item["cliFramework"]?.DeepClone();
        }
        else if (result["cliFramework"] is null && item["cliFramework"] is not null)
        {
            result["cliFramework"] = item["cliFramework"]?.DeepClone();
        }

        if (item["analysisMode"] is not null)
        {
            result["analysisMode"] = item["analysisMode"]?.DeepClone();

            if (result["analysisSelection"] is JsonObject analysisSelection)
            {
                analysisSelection["selectedMode"] = item["analysisMode"]?.DeepClone();
                if (analysisSelection["preferredMode"] is null)
                {
                    analysisSelection["preferredMode"] = item["analysisMode"]?.DeepClone();
                }
            }
        }
    }

    public static string GetNonSuccessReason(JsonObject result, JsonObject stateRecord)
        => result["failureMessage"]?.GetValue<string>() ??
           stateRecord["lastFailureMessage"]?.GetValue<string>() ??
           GetDefaultReasonMessage(stateRecord["currentStatus"]?.GetValue<string>(), result["classification"]?.GetValue<string>());

    public static int GetBackoffHours(int attempt)
        => attempt switch
        {
            <= 1 => 1,
            2 => 6,
            _ => 24,
        };

    public static JsonObject UpdateStateRecord(JsonObject? existingState, JsonObject result, JsonObject? indexedPaths, DateTimeOffset now)
    {
        var sameSignature =
            !string.IsNullOrWhiteSpace(existingState?["lastFailureSignature"]?.GetValue<string>()) &&
            string.Equals(existingState?["lastFailureSignature"]?.GetValue<string>(), result["failureSignature"]?.GetValue<string>(), StringComparison.Ordinal);
        var consecutiveFailures = string.Equals(result["disposition"]?.GetValue<string>(), "retryable-failure", StringComparison.Ordinal)
            ? sameSignature
                ? (existingState?["consecutiveFailureCount"]?.GetValue<int?>() ?? 0) + 1
                : 1
            : 0;
        var attemptCount = result["attempt"]?.GetValue<int?>() ?? 1;
        var allowTerminalEscalation =
            !string.Equals(result["disposition"]?.GetValue<string>(), "retryable-failure", StringComparison.Ordinal) ||
            !string.Equals(result["classification"]?.GetValue<string>(), "environment-missing-runtime", StringComparison.Ordinal);

        var status = result["disposition"]?.GetValue<string>() switch
        {
            "success" => "success",
            "terminal-negative" => "terminal-negative",
            "terminal-failure" => "terminal-failure",
            _ => allowTerminalEscalation && consecutiveFailures >= 3 ? "terminal-failure" : "retryable-failure",
        };

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["packageId"] = result["packageId"]?.GetValue<string>(),
            ["version"] = result["version"]?.GetValue<string>(),
            ["trusted"] = false,
            ["currentStatus"] = status,
            ["lastDisposition"] = result["disposition"]?.GetValue<string>(),
            ["attemptCount"] = attemptCount,
            ["consecutiveFailureCount"] = consecutiveFailures,
            ["lastFailureSignature"] = status.Contains("failure", StringComparison.Ordinal) ? result["failureSignature"]?.GetValue<string>() : null,
            ["lastFailurePhase"] = status.Contains("failure", StringComparison.Ordinal) ? result["phase"]?.GetValue<string>() : null,
            ["lastFailureMessage"] = status.Contains("failure", StringComparison.Ordinal) ? result["failureMessage"]?.GetValue<string>() : null,
            ["firstEvaluatedAt"] = existingState?["firstEvaluatedAt"]?.GetValue<string>() ?? result["analyzedAt"]?.GetValue<string>(),
            ["lastEvaluatedAt"] = result["analyzedAt"]?.GetValue<string>(),
            ["lastBatchId"] = result["batchId"]?.GetValue<string>(),
            ["retryEligible"] = status == "retryable-failure",
            ["nextAttemptAt"] = status == "retryable-failure" ? now.AddHours(GetBackoffHours(attemptCount)).ToString("O") : null,
            ["lastSuccessfulAt"] = status == "success"
                ? result["analyzedAt"]?.GetValue<string>()
                : existingState?["lastSuccessfulAt"]?.GetValue<string>(),
            ["indexedPaths"] = indexedPaths?.DeepClone(),
        };
    }

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
