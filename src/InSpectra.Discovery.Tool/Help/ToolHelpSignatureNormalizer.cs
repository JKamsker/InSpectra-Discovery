namespace InSpectra.Discovery.Tool.Help;

internal static class ToolHelpSignatureNormalizer
{
    public static string NormalizeCommandKey(string key)
        => ToolHelpCommandSignatureSupport.NormalizeCommandKey(key);

    public static string NormalizeArgumentKey(string key)
        => key.Trim().TrimStart('[', '<').TrimEnd(']', '>');

    public static string NormalizeOptionSignatureKey(string key)
        => ToolHelpOptionSignatureNormalizationSupport.NormalizeOptionSignatureKey(key);

    public static bool LooksLikeOptionSignature(string key)
        => ToolHelpOptionSignatureNormalizationSupport.LooksLikeOptionSignature(key);

    public static bool TryExtractLeadingAliasFromDescription(string? description, out string alias, out string? normalizedDescription)
        => ToolHelpOptionSignatureNormalizationSupport.TryExtractLeadingAliasFromDescription(description, out alias, out normalizedDescription);

    public static bool LooksLikeCommandDescription(string description)
        => ToolHelpCommandSignatureSupport.LooksLikeCommandDescription(description);

    public static bool IsBuiltinAuxiliaryCommand(string key)
        => ToolHelpCommandSignatureSupport.IsBuiltinAuxiliaryCommand(key);

    public static string NormalizeCommandItemLine(string rawLine)
        => ToolHelpCommandSignatureSupport.NormalizeCommandItemLine(rawLine);

    public static bool LooksLikeMarkdownTableLine(string line)
        => ToolHelpCommandSignatureSupport.LooksLikeMarkdownTableLine(line);
}

