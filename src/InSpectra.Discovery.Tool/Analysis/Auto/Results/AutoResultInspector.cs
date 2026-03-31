namespace InSpectra.Discovery.Tool.Analysis.Auto.Results;

using System.Text.Json.Nodes;

internal static class AutoResultInspector
{
    public static bool ShouldTryHelpFallback(JsonObject? nativeResult)
        => nativeResult is null
            || !IsSuccessful(nativeResult)
            || !HasOpenCliArtifact(nativeResult);

    public static bool ShouldTryStaticFallback(string selectedMode, string? preferredMode, JsonObject result)
        => string.Equals(selectedMode, "hook", StringComparison.Ordinal)
            && string.Equals(preferredMode, "static", StringComparison.OrdinalIgnoreCase)
            && ShouldTryHelpFallback(result);

    public static bool ShouldUseStaticFallback(JsonObject result)
        => !ShouldTryHelpFallback(result);

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

