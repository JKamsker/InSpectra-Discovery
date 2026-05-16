namespace InSpectra.Discovery.Tool.Analysis.Auto.Results;

using System.Text.Json.Nodes;

internal static class AutoResultInspector
{
    public static bool ShouldTryHelpFallback(JsonObject? nativeResult)
        => nativeResult is null
            || !IsSuccessful(nativeResult)
            || !HasOpenCliArtifact(nativeResult);

    public static bool ShouldUseFallbackResult(JsonObject result)
        => IsSuccessful(result)
            && HasOpenCliArtifact(result);

    public static bool ShouldUseFailedFallbackResult(JsonObject initialResult, JsonObject fallbackResult)
        => !IsTerminalFailure(initialResult)
            && IsTerminalFailure(fallbackResult);

    public static bool ShouldPreserveNativeResult(JsonObject? nativeResult, JsonObject helpResult)
        => nativeResult is not null
            && IsSuccessful(nativeResult)
            && !HasOpenCliArtifact(nativeResult)
            && !IsSuccessful(helpResult);

    private static bool IsSuccessful(JsonObject result)
        => string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal);

    private static bool HasOpenCliArtifact(JsonObject result)
        => !string.IsNullOrWhiteSpace(result["artifacts"]?["opencliArtifact"]?.GetValue<string>());

    private static bool IsTerminalFailure(JsonObject result)
        => string.Equals(result["disposition"]?.GetValue<string>(), "terminal-failure", StringComparison.Ordinal);
}

