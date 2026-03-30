namespace InSpectra.Discovery.Tool.Help;

using System.Text.RegularExpressions;

internal static partial class LegacyOptionTable
{
    public static IReadOnlyList<string> NormalizeOptionLines(IReadOnlyList<string> lines)
    {
        var structuredTableLines = StructuredOptionTableSupport.TryExtractStructuredOptionLines(lines);
        return structuredTableLines.Count > 0 ? structuredTableLines : lines;
    }

    public static IReadOnlyList<string> InferOptionLines(
        IReadOnlyList<string> candidateLines,
        IReadOnlyList<string> usageLines)
    {
        if (RootCommandInventoryInference.LooksLikeAliasCommandInventoryBlock(candidateLines))
        {
            return [];
        }

        var commandLineParserOptionLines = TryExtractCommandLineParserOptionLines(candidateLines);
        if (commandLineParserOptionLines.Count > 0)
        {
            return commandLineParserOptionLines;
        }

        var tableLines = TryExtractTableLines(candidateLines, usageLines);
        if (tableLines.Count > 0)
        {
            return tableLines;
        }

        var looseBlockLines = TryExtractLooseBlockLines(candidateLines);
        return looseBlockLines.Count > 0 ? looseBlockLines : [];
    }

    private static IReadOnlyList<string> TryExtractCommandLineParserOptionLines(IReadOnlyList<string> lines)
    {
        var results = new List<string>();
        var hasRows = false;
        var capturedAnyBlockRows = false;
        var currentRowCaptured = false;

        foreach (var rawLine in lines)
        {
            if (LegacyOptionRowSupport.TryBuildCommandLineParserBlockRow(rawLine, out var rowLine))
            {
                results.Add(rowLine);
                hasRows = true;
                capturedAnyBlockRows = true;
                currentRowCaptured = true;
                continue;
            }

            if (currentRowCaptured && LegacyOptionRowSupport.ShouldContinueCommandLineParserOptionRow(rawLine))
            {
                results.Add(rawLine);
                continue;
            }

            if (capturedAnyBlockRows && string.IsNullOrWhiteSpace(rawLine))
            {
                results.Add(rawLine);
                currentRowCaptured = false;
                continue;
            }

            if (capturedAnyBlockRows)
            {
                break;
            }

            currentRowCaptured = false;
        }

        return hasRows ? results : [];
    }

    private static IReadOnlyList<string> TryExtractTableLines(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> usageLines)
    {
        var structuredTableLines = StructuredOptionTableSupport.TryExtractStructuredOptionLines(lines);
        if (structuredTableLines.Count > 0)
        {
            return structuredTableLines;
        }

        var headerIndex = LegacyOptionRowSupport.FindLegacyTableHeaderIndex(lines);
        if (headerIndex < 0)
        {
            return [];
        }

        var usageArguments = LegacyOptionRowSupport.ExtractUsageArgumentNames(usageLines);
        var results = new List<string>();
        var hasRows = false;
        var currentRowCaptured = false;

        foreach (var rawLine in lines.Skip(headerIndex + 1))
        {
            if (LegacyOptionRowSupport.TryBuildTableRow(rawLine, usageArguments, out var syntheticLine))
            {
                results.Add(syntheticLine);
                hasRows = true;
                currentRowCaptured = true;
                continue;
            }

            if (currentRowCaptured && rawLine.Length > 0 && char.IsWhiteSpace(rawLine, 0))
            {
                results.Add(rawLine);
                continue;
            }

            currentRowCaptured = false;
        }

        return hasRows ? results : [];
    }

    private static IReadOnlyList<string> TryExtractLooseBlockLines(IReadOnlyList<string> lines)
    {
        var bestLines = Array.Empty<string>();
        var currentLines = new List<string>();
        var rowCount = 0;
        var currentRowCaptured = false;
        var previousWasBlank = true;

        void Commit()
        {
            if (rowCount >= 2)
            {
                bestLines = currentLines.ToArray();
            }

            currentLines.Clear();
            rowCount = 0;
            currentRowCaptured = false;
        }

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                if (currentLines.Count > 0)
                {
                    currentLines.Add(rawLine);
                }

                previousWasBlank = true;
                continue;
            }

            if ((previousWasBlank || currentLines.Count > 0) && LooksLikeLooseOptionRow(rawLine))
            {
                currentLines.Add(rawLine);
                rowCount++;
                currentRowCaptured = true;
                previousWasBlank = false;
                continue;
            }

            if (currentRowCaptured && LegacyOptionRowSupport.GetIndentation(rawLine) > 0)
            {
                currentLines.Add(rawLine);
                previousWasBlank = false;
                continue;
            }

            Commit();
            previousWasBlank = false;
        }

        Commit();
        return bestLines;
    }

    private static bool LooksLikeLooseOptionRow(string rawLine)
        => LegacyOptionRowSupport.LooksLikeLooseOptionRow(rawLine);
}

