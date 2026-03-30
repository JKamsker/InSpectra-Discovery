namespace InSpectra.Discovery.Tool.Help;

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
            && !trimmed.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase);
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
}

