namespace InSpectra.Discovery.Tool.Analysis;

using System.Text.Json.Nodes;

internal static class NonSpectreResultSupport
{
    public static JsonObject CreateInitialResult(
        string packageId,
        string version,
        string? commandName,
        string batchId,
        int attempt,
        string source,
        string? cliFramework,
        string analysisMode,
        DateTimeOffset analyzedAt)
    {
        var result = ResultSupport.CreateInitialResult(packageId, version, batchId, attempt, source, analyzedAt);
        result["command"] = commandName;
        result["cliFramework"] = cliFramework;
        result["analysisMode"] = analysisMode;
        result["timings"]!.AsObject()["crawlMs"] = null;
        result["artifacts"]!.AsObject()["crawlArtifact"] = null;
        return result;
    }

    public static void ApplyRetryableFailure(JsonObject result, string phase, string classification, string? message)
    {
        result["disposition"] = "retryable-failure";
        result["retryEligible"] = true;
        result["phase"] = phase;
        result["classification"] = classification;
        result["failureMessage"] = message;
    }

    public static void ApplyUnexpectedRetryableFailure(JsonObject result, string? message)
    {
        var phase = result["phase"]?.GetValue<string>();
        var classification = result["classification"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(phase))
        {
            phase = "bootstrap";
        }

        if (string.IsNullOrWhiteSpace(classification) || string.Equals(classification, "uninitialized", StringComparison.Ordinal))
        {
            classification = "unexpected-exception";
        }

        ApplyRetryableFailure(result, phase, classification, message);
    }

    public static void ApplyTerminalFailure(JsonObject result, string phase, string classification, string? message)
    {
        result["disposition"] = "terminal-failure";
        result["retryEligible"] = false;
        result["phase"] = phase;
        result["classification"] = classification;
        result["failureMessage"] = message;
    }

    public static void ApplySuccess(JsonObject result, string classification, string artifactSource)
    {
        result["disposition"] = "success";
        result["retryEligible"] = false;
        result["phase"] = "complete";
        result["classification"] = classification;
        result["failureMessage"] = null;
        result["failureSignature"] = null;

        var openCliStep = new JsonObject
        {
            ["status"] = "ok",
            ["classification"] = classification,
            ["artifactSource"] = artifactSource,
        };
        result["steps"]!.AsObject()["opencli"] = openCliStep.DeepClone();
        result["introspection"]!.AsObject()["opencli"] = openCliStep.DeepClone();
    }

    public static void FinalizeFailureSignature(JsonObject result)
    {
        var disposition = result["disposition"]?.GetValue<string>();
        if (disposition is "retryable-failure" or "terminal-failure")
        {
            result["failureSignature"] = ResultSupport.GetFailureSignature(
                result["phase"]?.GetValue<string>() ?? "unknown",
                result["classification"]?.GetValue<string>() ?? "unknown",
                result["failureMessage"]?.GetValue<string>());
        }
        else
        {
            result["failureSignature"] = null;
        }
    }
}


