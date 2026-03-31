namespace InSpectra.Discovery.Tool.Help.Inference.Inventory;

using InSpectra.Discovery.Tool.Help.Signatures;

internal static class RootCommandAliasInventorySupport
{
    public static IReadOnlyList<string> InferAliasInventoryLines(IReadOnlyList<string> lines)
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

            var isAliasInventoryEntry = TryParseAliasInventoryEntry(rawLine, out var aliasDescription);
            var isBuiltinAuxiliaryInventoryEntry = !isAliasInventoryEntry && TryParseBuiltinAuxiliaryInventoryEntry(rawLine);
            if (isAliasInventoryEntry || isBuiltinAuxiliaryInventoryEntry)
            {
                if (!sawBlank && currentLines.Count == 0)
                {
                    continue;
                }

                currentIndentation = GetIndentation(rawLine);
                currentLines.Add(rawLine);
                if (isAliasInventoryEntry)
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

    private static bool TryParseAliasInventoryEntry(string rawLine, out string description)
    {
        description = string.Empty;
        if (!CommandPrototypeSupport.TryParseBareShortLongAliasRow(rawLine, out _, out var parsedLongName, out var parsedDescription))
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

