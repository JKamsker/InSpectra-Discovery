namespace InSpectra.Discovery.Tool.Help.Parsing;

using InSpectra.Discovery.Tool.Help.Inference.Text;

using InSpectra.Discovery.Tool.Help.Inference.Descriptions;

using InSpectra.Discovery.Tool.Help.Signatures;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.OpenCli.Structure;

using System.Text.RegularExpressions;

internal static partial class ItemStartParserSupport
{
    public static bool TryParseItemStart(
        string rawLine,
        ItemKind kind,
        out string key,
        out bool isRequired,
        out string? description)
    {
        key = string.Empty;
        description = null;
        isRequired = false;

        if (kind == ItemKind.Command)
        {
            rawLine = SignatureNormalizer.NormalizeCommandItemLine(rawLine);
        }

        var trimmedStart = rawLine.TrimStart();
        if (kind == ItemKind.Command && SignatureNormalizer.LooksLikeMarkdownTableLine(trimmedStart))
        {
            return false;
        }

        if (kind == ItemKind.Argument && TryParsePositionalArgumentRow(trimmedStart, out key, out isRequired, out description))
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

        if (kind == ItemKind.Option && !key.StartsWith("-", StringComparison.Ordinal) && !key.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (kind == ItemKind.Option)
        {
            key = SignatureNormalizer.NormalizeOptionSignatureKey(key);
            if (!SignatureNormalizer.LooksLikeOptionSignature(key))
            {
                return false;
            }

            var optionSignature = OptionSignatureSupport.Parse(key);
            if (!OpenCliNameValidationSupport.IsPublishableOptionName(optionSignature.PrimaryName)
                || optionSignature.Aliases.Any(alias => !OpenCliNameValidationSupport.IsPublishableOptionName(alias)))
            {
                return false;
            }

            if (SignatureNormalizer.TryExtractLeadingAliasFromDescription(description, out var alias, out var normalizedDescription))
            {
                key = $"{key} | {alias}";
                description = normalizedDescription;
            }
        }

        if (kind == ItemKind.Command)
        {
            if (!char.IsWhiteSpace(rawLine, 0) && string.IsNullOrWhiteSpace(description))
            {
                if (!CommandPrototypeSupport.AllowsBlankDescriptionLine(key))
                {
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(description)
                && key.Contains(' ', StringComparison.Ordinal)
                && !CommandPrototypeSupport.LooksLikeCommandPrototype(key))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(description)
                && !SignatureNormalizer.LooksLikeCommandDescription(description))
            {
                if (SignatureNormalizer.LooksLikeOpaqueCommandDescription(description))
                {
                    description = null;
                }
                else
                {
                    return false;
                }
            }

            key = SignatureNormalizer.NormalizeCommandKey(key);
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }

            if (CommandPrototypeSupport.IsNarrativeBareCommandToken(key))
            {
                return false;
            }
        }

        if (kind == ItemKind.Argument)
        {
            key = SignatureNormalizer.NormalizeArgumentKey(key);
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

        key = SignatureNormalizer.NormalizeArgumentKey(match.Groups["key"].Value);
        description = match.Groups["description"].Success
            ? match.Groups["description"].Value.Trim()
            : null;
        if (RequiredDescriptionSupport.StartsWithRequiredPrefix(description))
        {
            isRequired = true;
            description = RequiredDescriptionSupport.TrimLeadingRequiredPrefix(description);
        }

        return key.Length > 0;
    }

    private static bool IsNoiseItemKey(ItemKind kind, string key)
    {
        var trimmed = key.Trim();
        return TextNoiseClassifier.IsFrameworkNoiseLine(trimmed)
            || (kind == ItemKind.Argument && TextNoiseClassifier.IsArgumentNoiseLine(trimmed));
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
