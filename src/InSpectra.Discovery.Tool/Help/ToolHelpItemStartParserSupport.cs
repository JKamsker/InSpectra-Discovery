namespace InSpectra.Discovery.Tool.Help;

using System.Text.RegularExpressions;

internal static partial class ToolHelpItemStartParserSupport
{
    public static bool TryParseItemStart(
        string rawLine,
        ToolHelpItemKind kind,
        out string key,
        out bool isRequired,
        out string? description)
    {
        key = string.Empty;
        description = null;
        isRequired = false;

        if (kind == ToolHelpItemKind.Command)
        {
            rawLine = ToolHelpSignatureNormalizer.NormalizeCommandItemLine(rawLine);
        }

        var trimmedStart = rawLine.TrimStart();
        if (kind == ToolHelpItemKind.Command && ToolHelpSignatureNormalizer.LooksLikeMarkdownTableLine(trimmedStart))
        {
            return false;
        }

        if (kind == ToolHelpItemKind.Argument && TryParsePositionalArgumentRow(trimmedStart, out key, out isRequired, out description))
        {
            return true;
        }

        var match = ItemRegex().Match(trimmedStart);
        if (!match.Success)
        {
            return false;
        }

        key = match.Groups["key"].Value.Trim();
        description = match.Groups["description"].Success ? match.Groups["description"].Value.Trim() : null;
        isRequired = string.Equals(match.Groups["prefix"].Value, "* ", StringComparison.Ordinal);

        if (kind == ToolHelpItemKind.Option && !key.StartsWith("-", StringComparison.Ordinal) && !key.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (kind == ToolHelpItemKind.Option)
        {
            key = ToolHelpSignatureNormalizer.NormalizeOptionSignatureKey(key);
            if (!ToolHelpSignatureNormalizer.LooksLikeOptionSignature(key))
            {
                return false;
            }

            if (ToolHelpSignatureNormalizer.TryExtractLeadingAliasFromDescription(description, out var alias, out var normalizedDescription))
            {
                key = $"{key} | {alias}";
                description = normalizedDescription;
            }
        }

        if (kind == ToolHelpItemKind.Command)
        {
            if (!char.IsWhiteSpace(rawLine, 0) && string.IsNullOrWhiteSpace(description))
            {
                if (!ToolHelpCommandPrototypeSupport.AllowsBlankDescriptionLine(key))
                {
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(description)
                && key.Contains(' ', StringComparison.Ordinal)
                && !ToolHelpCommandPrototypeSupport.LooksLikeCommandPrototype(key))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(description)
                && !ToolHelpSignatureNormalizer.LooksLikeCommandDescription(description))
            {
                return false;
            }

            key = ToolHelpSignatureNormalizer.NormalizeCommandKey(key);
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }
        }

        if (kind == ToolHelpItemKind.Argument)
        {
            key = ToolHelpSignatureNormalizer.NormalizeArgumentKey(key);
            if (!LooksLikeArgumentKey(key))
            {
                return false;
            }
        }

        if (IsNoiseItemKey(kind, key))
        {
            return false;
        }

        return true;
    }

    public static bool TryParsePositionalArgumentRow(string rawLine, out string key, out bool isRequired, out string? description)
    {
        key = string.Empty;
        description = null;
        isRequired = false;

        var match = PositionalArgumentRowRegex().Match(rawLine);
        if (!match.Success)
        {
            return false;
        }

        key = ToolHelpSignatureNormalizer.NormalizeArgumentKey(match.Groups["key"].Value);
        description = match.Groups["description"].Success
            ? match.Groups["description"].Value.Trim()
            : null;
        if (ToolHelpRequiredDescriptionSupport.StartsWithRequiredPrefix(description))
        {
            isRequired = true;
            description = ToolHelpRequiredDescriptionSupport.TrimLeadingRequiredPrefix(description);
        }

        return key.Length > 0;
    }

    private static bool IsNoiseItemKey(ToolHelpItemKind kind, string key)
    {
        var trimmed = key.Trim();
        return ToolHelpTextNoiseClassifier.IsFrameworkNoiseLine(trimmed)
            || (kind == ToolHelpItemKind.Argument && ToolHelpTextNoiseClassifier.IsArgumentNoiseLine(trimmed));
    }

    private static bool LooksLikeArgumentKey(string key)
        => key.Length > 0
            && !key.Contains(' ', StringComparison.Ordinal)
            && key.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.');

    [GeneratedRegex(@"^(?<prefix>\* )?(?<key>\S.*?)(?:\s{2,}(?<description>\S.*))?$", RegexOptions.Compiled)]
    private static partial Regex ItemRegex();

    [GeneratedRegex(@"^(?<key>\S(?:.*?\S)?)\s+(?:\(pos\.\s*\d+\)|pos\.\s*\d+)(?:\s+(?<description>\S.*))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PositionalArgumentRowRegex();
}

