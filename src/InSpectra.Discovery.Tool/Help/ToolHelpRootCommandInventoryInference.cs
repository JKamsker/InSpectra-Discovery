internal static class ToolHelpRootCommandInventoryInference
{
    public static IReadOnlyList<string> InferLines(IReadOnlyList<string> preamble)
    {
        var aliasInventoryLines = InferAliasInventoryLines(preamble.Skip(1).ToArray());
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
        => InferAliasInventoryLines(lines).Count > 0;

    private static IReadOnlyList<string> InferAliasInventoryLines(IReadOnlyList<string> lines)
    {
        var bestLines = Array.Empty<string>();
        var currentLines = new List<string>();
        var currentIndentation = -1;
        var aliasRowCount = 0;
        var aliasDescriptions = new List<string>();
        var sawBlank = true;
        var previousWasRow = false;

        void Commit()
        {
            if (aliasRowCount >= 2 && !LooksLikeOptionHeavyAliasInventory(aliasDescriptions))
            {
                bestLines = currentLines.ToArray();
            }

            currentLines.Clear();
            currentIndentation = -1;
            aliasRowCount = 0;
            aliasDescriptions.Clear();
            previousWasRow = false;
        }

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                if (currentLines.Count > 0)
                {
                    currentLines.Add(rawLine);
                }

                sawBlank = true;
                previousWasRow = false;
                continue;
            }

            if (TryParseAliasInventoryEntry(rawLine, out var aliasDescription)
                || TryParseBuiltinAuxiliaryInventoryEntry(rawLine))
            {
                if (!sawBlank && currentLines.Count == 0)
                {
                    continue;
                }

                currentIndentation = GetIndentation(rawLine);
                currentLines.Add(rawLine);
                if (TryParseAliasInventoryEntry(rawLine, out aliasDescription))
                {
                    aliasRowCount++;
                    aliasDescriptions.Add(aliasDescription);
                }

                sawBlank = false;
                previousWasRow = true;
                continue;
            }

            if (previousWasRow && GetIndentation(rawLine) > currentIndentation)
            {
                currentLines.Add(rawLine);
                sawBlank = false;
                continue;
            }

            Commit();
            sawBlank = false;
        }

        Commit();
        return bestLines;
    }

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

        if (ToolHelpCommandPrototypeSupport.LooksLikeBareShortLongOptionRow(rawLine))
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
            || ToolHelpCommandPrototypeSupport.LooksLikeBareShortLongOptionRow(rawLine);

    private static bool TryParseAliasInventoryEntry(string rawLine, out string description)
    {
        description = string.Empty;
        if (!ToolHelpCommandPrototypeSupport.TryParseBareShortLongAliasRow(rawLine, out _, out var parsedLongName, out var parsedDescription))
        {
            return false;
        }

        if (string.Equals(parsedLongName, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parsedLongName, "version", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        description = parsedDescription;
        return true;
    }

    private static bool TryParseBuiltinAuxiliaryInventoryEntry(string rawLine)
    {
        var trimmed = rawLine.Trim();
        if (trimmed.StartsWith("-", StringComparison.Ordinal) || trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.StartsWith("help", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("version", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePositionalArgumentRow(string rawLine)
    {
        var trimmed = rawLine.TrimStart();
        var markerIndex = trimmed.IndexOf("pos.", StringComparison.OrdinalIgnoreCase);
        return markerIndex > 0 && trimmed.Contains(' ', StringComparison.Ordinal);
    }

    private static bool LooksLikeOptionHeavyAliasInventory(IReadOnlyList<string> descriptions)
        => descriptions.Any(description =>
        {
            var trimmed = description.TrimStart();
            return trimmed.StartsWith("Required.", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Optional.", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("(Default:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Path to ", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("List of ", StringComparison.OrdinalIgnoreCase);
        });

    private static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();
}
