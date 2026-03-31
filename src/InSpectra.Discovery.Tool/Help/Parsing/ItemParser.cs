namespace InSpectra.Discovery.Tool.Help.Parsing;

using InSpectra.Discovery.Tool.Help.Inference.Text;

using InSpectra.Discovery.Tool.Help.Signatures;

using InSpectra.Discovery.Tool.Help.Inference.Inventory;

using InSpectra.Discovery.Tool.Help.Documents;

internal static class ItemParser
{
    public static IReadOnlyList<Item> ParseItems(IReadOnlyList<string> lines, ItemKind kind)
    {
        var items = new List<Item>();
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

            if (kind == ItemKind.Option
                && ItemStartParserSupport.TryParsePositionalArgumentRow(rawLine.TrimStart(), out _, out _, out _))
            {
                continue;
            }

            var currentIndentation = GetIndentation(rawLine);
            var canStartNewItem = ItemStartParserSupport.TryParseItemStart(
                    rawLine,
                    kind,
                    out var parsedKey,
                    out var parsedRequired,
                    out var parsedDescription)
                && !(key is not null && currentIndentation > indentation && kind != ItemKind.Option);
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
            if (ItemStartParserSupport.TryParseItemStart(rawLine, ItemKind.Option, out _, out _, out _))
            {
                target = options;
                currentOptionIndentation = currentIndentation;
            }
            else if (target == options
                && rawLine.Length > 0
                && char.IsWhiteSpace(rawLine, 0)
                && currentOptionIndentation >= 0
                && currentIndentation > currentOptionIndentation
                && !ItemStartParserSupport.TryParsePositionalArgumentRow(rawLine.TrimStart(), out _, out _, out _))
            {
                target = options;
            }
            else if (ItemStartParserSupport.TryParseItemStart(rawLine, ItemKind.Argument, out _, out _, out _))
            {
                target = arguments;
                currentOptionIndentation = -1;
            }

            target?.Add(rawLine);
        }

        argumentLines = arguments;
        optionLines = options;
    }

    public static IReadOnlyList<Item> InferCommands(
        IReadOnlyList<string> preamble,
        bool sawInventoryHeader)
    {
        if (sawInventoryHeader)
        {
            return [];
        }

        var inventoryLines = RootCommandInventoryInference.InferLines(preamble);
        if (inventoryLines.Count == 0)
        {
            return [];
        }

        var parsedCommands = ParseItems(inventoryLines, ItemKind.Command);
        var describedCommands = parsedCommands
            .Where(item => !string.IsNullOrWhiteSpace(item.Description))
            .ToArray();
        if (describedCommands.Any(item => !SignatureNormalizer.IsBuiltinAuxiliaryCommand(item.Key)))
        {
            return describedCommands;
        }

        return parsedCommands
            .Where(item => string.IsNullOrWhiteSpace(item.Description))
            .Where(item => !SignatureNormalizer.IsBuiltinAuxiliaryCommand(item.Key))
            .ToArray();
    }

    private static void FlushItem(ICollection<Item> items, string? key, bool isRequired, string? description)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        items.Add(new Item(key, isRequired, string.IsNullOrWhiteSpace(description) ? null : description.Trim()));
    }

    private static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();

    private static bool IsNoiseContinuationLine(ItemKind kind, string rawLine)
    {
        var trimmed = rawLine.Trim();
        return TextNoiseClassifier.IsFrameworkNoiseLine(trimmed)
            || (kind == ItemKind.Argument && TextNoiseClassifier.IsArgumentNoiseLine(trimmed))
            || (kind == ItemKind.Command && TextNoiseClassifier.LooksLikeSubcommandHelpHint(trimmed));
    }
}

