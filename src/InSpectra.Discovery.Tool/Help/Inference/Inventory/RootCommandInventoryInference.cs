namespace InSpectra.Discovery.Tool.Help.Inference.Inventory;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Parsing;
using InSpectra.Discovery.Tool.Help.Signatures;

internal static class RootCommandInventoryInference
{
    public static IReadOnlyList<string> InferLines(IReadOnlyList<string> preamble)
    {
        var aliasInventoryLines = RootCommandAliasInventorySupport.InferAliasInventoryLines(preamble.Skip(1).ToArray());
        if (aliasInventoryLines.Count > 0)
        {
            return aliasInventoryLines;
        }

        var candidateLines = new List<string>();
        var startedInventory = false;
        var sawBlank = true;
        var currentIndentation = -1;

        foreach (var rawLine in preamble.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                sawBlank = true;
                if (startedInventory)
                {
                    candidateLines.Add(rawLine);
                }

                continue;
            }

            if (LooksLikeOptionRow(rawLine) || LooksLikePositionalArgumentRow(rawLine))
            {
                if (startedInventory)
                {
                    break;
                }

                sawBlank = false;
                continue;
            }

            if (LooksLikeInventoryEntry(rawLine, sawBlank, startedInventory, currentIndentation))
            {
                startedInventory = true;
                currentIndentation = GetIndentation(rawLine);
                candidateLines.Add(rawLine);
                sawBlank = false;
                continue;
            }

            if (startedInventory && GetIndentation(rawLine) > currentIndentation)
            {
                candidateLines.Add(rawLine);
                sawBlank = false;
                continue;
            }

            if (startedInventory)
            {
                break;
            }

            sawBlank = false;
        }

        return candidateLines;
    }

    public static bool LooksLikeAliasCommandInventoryBlock(IReadOnlyList<string> lines)
        => RootCommandAliasInventorySupport.InferAliasInventoryLines(lines).Count > 0;

    private static bool LooksLikeInventoryEntry(
        string rawLine,
        bool previousWasBlank,
        bool startedInventory,
        int currentIndentation)
    {
        var indentation = GetIndentation(rawLine);
        if ((!previousWasBlank && (!startedInventory || indentation != currentIndentation))
            || indentation == 0)
        {
            return false;
        }

        if (CommandPrototypeSupport.LooksLikeBareShortLongOptionRow(rawLine))
        {
            return false;
        }

        var trimmed = rawLine.Trim();
        return trimmed.Length > 0
            && char.IsLetter(trimmed[0])
            && (!trimmed.Contains(' ', StringComparison.Ordinal) || trimmed.Contains("  ", StringComparison.Ordinal))
            && !trimmed.Contains('[', StringComparison.Ordinal)
            && !trimmed.Contains('<', StringComparison.Ordinal)
            && !LooksLikeOptionStyleDescription(trimmed)
            && !trimmed.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase)
            && ItemStartParserSupport.TryParseItemStart(rawLine, ItemKind.Command, out var commandKey, out _, out _)
            && LooksLikeInventoryCommandKey(commandKey);
    }

    private static bool LooksLikeOptionRow(string rawLine)
        => rawLine.TrimStart().StartsWith("-", StringComparison.Ordinal)
            || rawLine.TrimStart().StartsWith("/", StringComparison.Ordinal)
            || CommandPrototypeSupport.LooksLikeBareShortLongOptionRow(rawLine);

    private static bool LooksLikePositionalArgumentRow(string rawLine)
    {
        var trimmed = rawLine.TrimStart();
        var markerIndex = trimmed.IndexOf("pos.", StringComparison.OrdinalIgnoreCase);
        return markerIndex > 0 && trimmed.Contains(' ', StringComparison.Ordinal);
    }

    private static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();

    private static bool LooksLikeInventoryCommandKey(string commandKey)
    {
        if (CommandPrototypeSupport.LooksLikeCommandPrototype(commandKey))
        {
            return true;
        }

        var tokens = commandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length is > 0 and <= 4;
    }

    private static bool LooksLikeOptionStyleDescription(string line)
    {
        var description = TryExtractDescription(line);
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        return description.StartsWith("If specified", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Path to ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Required.", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Optional.", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("(Default:", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractDescription(string line)
    {
        for (var index = 0; index < line.Length - 1; index++)
        {
            if (!char.IsWhiteSpace(line[index]) || !char.IsWhiteSpace(line[index + 1]))
            {
                continue;
            }

            var description = line[(index + 2)..].Trim();
            return description.Length == 0 ? null : description;
        }

        return null;
    }
}
