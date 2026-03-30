using System.Text.RegularExpressions;

internal static partial class ToolHelpItemParser
{
    public static IReadOnlyList<ToolHelpItem> ParseItems(IReadOnlyList<string> lines, ToolHelpItemKind kind)
    {
        var items = new List<ToolHelpItem>();
        string? key = null;
        string? description = null;
        var isRequired = false;
        var indentation = -1;

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            if (IsNoiseContinuationLine(kind, rawLine))
            {
                continue;
            }

            if (kind == ToolHelpItemKind.Option
                && TryParsePositionalArgumentRow(rawLine.TrimStart(), out _, out _, out _))
            {
                continue;
            }

            var currentIndentation = GetIndentation(rawLine);
            var canStartNewItem = TryParseItemStart(rawLine, kind, out var parsedKey, out var parsedRequired, out var parsedDescription)
                && !(key is not null && currentIndentation > indentation && kind != ToolHelpItemKind.Option);
            if (canStartNewItem)
            {
                FlushItem(items, key, isRequired, description);
                key = parsedKey;
                isRequired = parsedRequired;
                description = parsedDescription;
                indentation = currentIndentation;
                continue;
            }

            if (key is not null)
            {
                description = string.IsNullOrWhiteSpace(description)
                    ? rawLine.Trim()
                    : $"{description}\n{rawLine.Trim()}";
            }
        }

        FlushItem(items, key, isRequired, description);
        return items;
    }

    public static void SplitArgumentSectionLines(
        IReadOnlyList<string> lines,
        out IReadOnlyList<string> argumentLines,
        out IReadOnlyList<string> optionLines)
    {
        var arguments = new List<string>();
        var options = new List<string>();
        List<string>? target = arguments;
        var currentOptionIndentation = -1;

        foreach (var rawLine in lines)
        {
            var currentIndentation = GetIndentation(rawLine);
            if (TryParseItemStart(rawLine, ToolHelpItemKind.Option, out _, out _, out _))
            {
                target = options;
                currentOptionIndentation = currentIndentation;
            }
            else if (target == options
                && rawLine.Length > 0
                && char.IsWhiteSpace(rawLine, 0)
                && currentOptionIndentation >= 0
                && currentIndentation > currentOptionIndentation
                && !TryParsePositionalArgumentRow(rawLine.TrimStart(), out _, out _, out _))
            {
                target = options;
            }
            else if (TryParseItemStart(rawLine, ToolHelpItemKind.Argument, out _, out _, out _))
            {
                target = arguments;
                currentOptionIndentation = -1;
            }

            target?.Add(rawLine);
        }

        argumentLines = arguments;
        optionLines = options;
    }

    public static IReadOnlyList<ToolHelpItem> InferCommands(
        IReadOnlyList<string> preamble,
        bool sawInventoryHeader)
    {
        if (sawInventoryHeader)
        {
            return [];
        }

        var inventoryLines = ToolHelpRootCommandInventoryInference.InferLines(preamble);
        if (inventoryLines.Count == 0)
        {
            return [];
        }

        var parsedCommands = ParseItems(inventoryLines, ToolHelpItemKind.Command);
        var describedCommands = parsedCommands
            .Where(item => !string.IsNullOrWhiteSpace(item.Description))
            .ToArray();
        if (describedCommands.Any(item => !ToolHelpSignatureNormalizer.IsBuiltinAuxiliaryCommand(item.Key)))
        {
            return describedCommands;
        }

        return parsedCommands
            .Where(item => string.IsNullOrWhiteSpace(item.Description))
            .Where(item => !ToolHelpSignatureNormalizer.IsBuiltinAuxiliaryCommand(item.Key))
            .ToArray();
    }

    private static bool TryParseItemStart(
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

    private static bool TryParsePositionalArgumentRow(string rawLine, out string key, out bool isRequired, out string? description)
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
        if (StartsWithRequiredPrefix(description))
        {
            isRequired = true;
            description = TrimLeadingRequiredPrefix(description);
        }

        return key.Length > 0;
    }

    private static void FlushItem(ICollection<ToolHelpItem> items, string? key, bool isRequired, string? description)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        items.Add(new ToolHelpItem(key, isRequired, string.IsNullOrWhiteSpace(description) ? null : description.Trim()));
    }

    private static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();

    private static bool IsNoiseItemKey(ToolHelpItemKind kind, string key)
    {
        var trimmed = key.Trim();
        return ToolHelpTextNoiseClassifier.IsFrameworkNoiseLine(trimmed)
            || (kind == ToolHelpItemKind.Argument && ToolHelpTextNoiseClassifier.IsArgumentNoiseLine(trimmed));
    }

    private static bool IsNoiseContinuationLine(ToolHelpItemKind kind, string rawLine)
    {
        var trimmed = rawLine.Trim();
        return ToolHelpTextNoiseClassifier.IsFrameworkNoiseLine(trimmed)
            || (kind == ToolHelpItemKind.Argument && ToolHelpTextNoiseClassifier.IsArgumentNoiseLine(trimmed))
            || (kind == ToolHelpItemKind.Command && ToolHelpTextNoiseClassifier.LooksLikeSubcommandHelpHint(trimmed));
    }

    private static bool LooksLikeArgumentKey(string key)
        => key.Length > 0
            && !key.Contains(' ', StringComparison.Ordinal)
            && key.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.');

    private static bool StartsWithRequiredPrefix(string? description)
        => !string.IsNullOrWhiteSpace(description)
            && (
                description.TrimStart().StartsWith("Required.", StringComparison.OrdinalIgnoreCase)
                || description.TrimStart().StartsWith("Required ", StringComparison.OrdinalIgnoreCase)
                || description.TrimStart().StartsWith("(REQUIRED)", StringComparison.OrdinalIgnoreCase)
                || description.TrimStart().StartsWith("[REQUIRED]", StringComparison.OrdinalIgnoreCase));

    private static string? TrimLeadingRequiredPrefix(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return description;
        }

        var normalized = description.TrimStart();
        if (normalized.StartsWith("Required.", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["Required.".Length..].TrimStart();
        }

        if (normalized.StartsWith("Required ", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["Required ".Length..].TrimStart();
        }

        if (normalized.StartsWith("(REQUIRED)", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["(REQUIRED)".Length..].TrimStart();
        }

        if (normalized.StartsWith("[REQUIRED]", StringComparison.OrdinalIgnoreCase))
        {
            return normalized["[REQUIRED]".Length..].TrimStart();
        }

        return description;
    }

    [GeneratedRegex(@"^(?<prefix>\* )?(?<key>\S.*?)(?:\s{2,}(?<description>\S.*))?$", RegexOptions.Compiled)]
    private static partial Regex ItemRegex();

    [GeneratedRegex(@"^(?<key>\S(?:.*?\S)?)\s+(?:\(pos\.\s*\d+\)|pos\.\s*\d+)(?:\s+(?<description>\S.*))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PositionalArgumentRowRegex();
}
