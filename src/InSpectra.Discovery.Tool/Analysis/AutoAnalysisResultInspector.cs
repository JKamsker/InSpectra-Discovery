using System.Text.Json.Nodes;

internal static class AutoAnalysisResultInspector
{
    public static bool ShouldTryHelpFallback(JsonObject? nativeResult)
        => nativeResult is null
            || !IsSuccessful(nativeResult)
            || !HasOpenCliArtifact(nativeResult);

    public static bool ShouldPreserveNativeResult(JsonObject? nativeResult, JsonObject helpResult)
        => nativeResult is not null
            && IsSuccessful(nativeResult)
            && !HasOpenCliArtifact(nativeResult)
            && !IsSuccessful(helpResult);

    private static bool IsSuccessful(JsonObject result)
        => string.Equals(result["disposition"]?.GetValue<string>(), "success", StringComparison.Ordinal);

    private static bool HasOpenCliArtifact(JsonObject result)
        => !string.IsNullOrWhiteSpace(result["artifacts"]?["opencliArtifact"]?.GetValue<string>());
}
