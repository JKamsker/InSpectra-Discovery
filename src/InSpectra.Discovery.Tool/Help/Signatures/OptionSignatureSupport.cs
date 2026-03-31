namespace InSpectra.Discovery.Tool.Help.Signatures;

using InSpectra.Discovery.Tool.Help.Inference.Usage;

internal static class OptionSignatureSupport
{
    public static OptionSignature Parse(string key)
        => OptionTokenParsingSupport.Parse(key);

    public static IEnumerable<string> EnumerateTokens(OptionSignature signature)
        => OptionTokenParsingSupport.EnumerateTokens(signature);

    public static bool LooksLikeOptionPlaceholder(string value)
        => OptionTokenParsingSupport.LooksLikeOptionPlaceholder(value);

    public static bool AppearsInOptionClause(string line, System.Text.RegularExpressions.Match match)
        => OptionTokenParsingSupport.AppearsInOptionClause(line, match);

    public static string NormalizeArgumentName(string key)
        => OptionValueInferenceSupport.NormalizeArgumentName(key);

    public static bool HasValueLikeOptionName(string primaryOption)
        => OptionValueInferenceSupport.HasValueLikeOptionName(primaryOption);

    public static string? InferArgumentNameFromOption(string? primaryOption)
        => OptionValueInferenceSupport.InferArgumentNameFromOption(primaryOption);
}

