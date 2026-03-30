internal static class ToolHelpOptionSignatureSupport
{
    public static ToolHelpOptionSignature Parse(string key)
        => ToolHelpOptionTokenParsingSupport.Parse(key);

    public static IEnumerable<string> EnumerateTokens(ToolHelpOptionSignature signature)
        => ToolHelpOptionTokenParsingSupport.EnumerateTokens(signature);

    public static bool LooksLikeOptionPlaceholder(string value)
        => ToolHelpOptionTokenParsingSupport.LooksLikeOptionPlaceholder(value);

    public static bool AppearsInOptionClause(string line, System.Text.RegularExpressions.Match match)
        => ToolHelpOptionTokenParsingSupport.AppearsInOptionClause(line, match);

    public static string NormalizeArgumentName(string key)
        => ToolHelpOptionValueInferenceSupport.NormalizeArgumentName(key);

    public static bool HasValueLikeOptionName(string primaryOption)
        => ToolHelpOptionValueInferenceSupport.HasValueLikeOptionName(primaryOption);

    public static string? InferArgumentNameFromOption(string? primaryOption)
        => ToolHelpOptionValueInferenceSupport.InferArgumentNameFromOption(primaryOption);
}

internal sealed record ToolHelpOptionSignature(
    string? PrimaryName,
    IReadOnlyList<string> Aliases,
    string? ArgumentName,
    bool ArgumentRequired);
