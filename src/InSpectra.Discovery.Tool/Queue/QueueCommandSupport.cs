using System.Text.Json.Nodes;

internal static class QueueCommandSupport
{
    public static async Task<IReadOnlyList<RegistrationPageLeaf>> GetRegistrationLeavesAsync(
        NuGetApiClient client,
        RegistrationIndex index,
        CancellationToken cancellationToken)
    {
        var leaves = new List<RegistrationPageLeaf>();
        foreach (var page in index.Items)
        {
            if (page.Items is { Count: > 0 })
            {
                leaves.AddRange(page.Items);
                continue;
            }

            var pageDocument = await client.GetRegistrationPageAsync(page.Id, cancellationToken);
            leaves.AddRange(pageDocument.Items);
        }

        return leaves;
    }

    public static string GetSanitizedBatchPrefix(string prefix, string branchName, string? timestamp)
    {
        var normalizedPrefix = NormalizeSegment(prefix);
        var normalizedBranch = NormalizeSegment(branchName);
        var normalizedTimestamp = string.IsNullOrWhiteSpace(timestamp)
            ? DateTimeOffset.UtcNow.ToString("yyyyMMddTHHmmssZ").ToLowerInvariant()
            : new string(timestamp.ToLowerInvariant().Where(ch => char.IsAsciiDigit(ch) || ch is 't' or 'z').ToArray());

        return $"{normalizedPrefix}-{normalizedBranch}-{normalizedTimestamp}";
    }

    public static string GetArtifactName(string lowerId, string lowerVersion)
        => new string($"analysis-{lowerId}-{lowerVersion}".Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '-').ToArray()).Trim('-');

    public static bool IsLegacyTerminalNegativeState(JsonObject? state)
    {
        if (!string.Equals(state?["currentStatus"]?.GetValue<string>(), "terminal-negative", StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(state?["lastDisposition"]?.GetValue<string>(), "terminal-negative", StringComparison.Ordinal))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(state?["lastFailureSignature"]?.GetValue<string>())
            && string.IsNullOrWhiteSpace(state?["lastFailureMessage"]?.GetValue<string>());
    }

    public static string? GetCurrentBackfillReason(JsonObject? state, DateTimeOffset now)
    {
        if (state is null)
        {
            return "missing-current-analysis";
        }

        if (IsLegacyTerminalNegativeState(state))
        {
            return "legacy-terminal-negative-reanalysis";
        }

        if (IsSupersededTerminalFailureState(state))
        {
            return "legacy-terminal-failure-reanalysis";
        }

        if (!string.Equals(state["currentStatus"]?.GetValue<string>(), "retryable-failure", StringComparison.Ordinal))
        {
            return null;
        }

        var nextAttemptAtText = state["nextAttemptAt"]?.GetValue<string>();
        return DateTimeOffset.TryParse(nextAttemptAtText, out var nextAttemptAt) && nextAttemptAt > now
            ? null
            : "retryable-current-reanalysis";
    }

    private static bool IsSupersededTerminalFailureState(JsonObject? state)
    {
        if (!string.Equals(state?["currentStatus"]?.GetValue<string>(), "terminal-failure", StringComparison.Ordinal)
            || !string.Equals(state?["lastDisposition"]?.GetValue<string>(), "terminal-failure", StringComparison.Ordinal))
        {
            return false;
        }

        var signature = state!["lastFailureSignature"]?.GetValue<string>();
        return signature is not null && (
            signature.StartsWith("opencli|unsupported-command|", StringComparison.Ordinal) ||
            signature.StartsWith("opencli|invalid-json|", StringComparison.Ordinal) ||
            signature.StartsWith("opencli|requires-configuration|", StringComparison.Ordinal) ||
            signature.StartsWith("opencli|timeout|", StringComparison.Ordinal) ||
            signature.StartsWith("xmldoc|timeout|", StringComparison.Ordinal));
    }

    private static string NormalizeSegment(string value)
    {
        var normalized = new string(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized;
    }
}
