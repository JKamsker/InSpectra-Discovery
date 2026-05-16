namespace InSpectra.Discovery.Tool.Promotion.State;

using System.Text.Json.Nodes;

internal static class PromotionStateRecordSupport
{
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

        if (CanPreserveExistingSuccessState(existingState, status, indexedPaths))
        {
            return existingState!.DeepClone().AsObject();
        }

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

    private static bool CanPreserveExistingSuccessState(JsonObject? existingState, string status, JsonObject? indexedPaths)
        => existingState is not null
           && string.Equals(status, "success", StringComparison.Ordinal)
           && string.Equals(existingState["currentStatus"]?.GetValue<string>(), "success", StringComparison.Ordinal)
           && string.Equals(existingState["lastDisposition"]?.GetValue<string>(), "success", StringComparison.Ordinal)
           && JsonNode.DeepEquals(existingState["indexedPaths"], indexedPaths);

    private static int GetBackoffHours(int attempt)
        => attempt switch
        {
            <= 1 => 1,
            2 => 6,
            _ => 24,
        };
}
