namespace InSpectra.Discovery.Tool.Help.Signatures;

internal static class SignatureNormalizer
{
    public static string NormalizeCommandKey(string key)
        => CommandSignatureSupport.NormalizeCommandKey(key);

    public static string NormalizeArgumentKey(string key)
        => key.Trim().TrimStart('[', '<').TrimEnd(']', '>');

    public static string NormalizeOptionSignatureKey(string key)
        => OptionSignatureNormalizationSupport.NormalizeOptionSignatureKey(key);

    public static bool LooksLikeOptionSignature(string key)
        => OptionSignatureNormalizationSupport.LooksLikeOptionSignature(key);

    public static bool TryExtractLeadingAliasFromDescription(string? description, out string alias, out string? normalizedDescription)
        => OptionSignatureNormalizationSupport.TryExtractLeadingAliasFromDescription(description, out alias, out normalizedDescription);

    public static bool LooksLikeCommandDescription(string description)
        => CommandSignatureSupport.LooksLikeCommandDescription(description);

    public static bool LooksLikeOpaqueCommandDescription(string description)
        => CommandSignatureSupport.LooksLikeOpaqueCommandDescription(description);

    public static bool IsBuiltinAuxiliaryCommand(string key)
        => CommandSignatureSupport.IsBuiltinAuxiliaryCommand(key);

    public static string NormalizeCommandItemLine(string rawLine)
        => CommandSignatureSupport.NormalizeCommandItemLine(rawLine);

    public static bool LooksLikeMarkdownTableLine(string line)
        => CommandSignatureSupport.LooksLikeMarkdownTableLine(line);
}
