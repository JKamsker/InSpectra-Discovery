internal static class ToolHelpItemParser
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
                && ToolHelpItemStartParserSupport.TryParsePositionalArgumentRow(rawLine.TrimStart(), out _, out _, out _))
            {
                continue;
            }

            var currentIndentation = GetIndentation(rawLine);
            var canStartNewItem = ToolHelpItemStartParserSupport.TryParseItemStart(
                    rawLine,
                    kind,
                    out var parsedKey,
                    out var parsedRequired,
                    out var parsedDescription)
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
            if (ToolHelpItemStartParserSupport.TryParseItemStart(rawLine, ToolHelpItemKind.Option, out _, out _, out _))
            {
                target = options;
                currentOptionIndentation = currentIndentation;
            }
            else if (target == options
                && rawLine.Length > 0
                && char.IsWhiteSpace(rawLine, 0)
                && currentOptionIndentation >= 0
                && currentIndentation > currentOptionIndentation
                && !ToolHelpItemStartParserSupport.TryParsePositionalArgumentRow(rawLine.TrimStart(), out _, out _, out _))
            {
                target = options;
            }
            else if (ToolHelpItemStartParserSupport.TryParseItemStart(rawLine, ToolHelpItemKind.Argument, out _, out _, out _))
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

    private static bool IsNoiseContinuationLine(ToolHelpItemKind kind, string rawLine)
    {
        var trimmed = rawLine.Trim();
        return ToolHelpTextNoiseClassifier.IsFrameworkNoiseLine(trimmed)
            || (kind == ToolHelpItemKind.Argument && ToolHelpTextNoiseClassifier.IsArgumentNoiseLine(trimmed))
            || (kind == ToolHelpItemKind.Command && ToolHelpTextNoiseClassifier.LooksLikeSubcommandHelpHint(trimmed));
    }
}
