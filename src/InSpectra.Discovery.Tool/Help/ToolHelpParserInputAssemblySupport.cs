namespace InSpectra.Discovery.Tool.Help;

internal static class ToolHelpParserInputAssemblySupport
{
    public static IReadOnlyList<string> BuildRawArgumentLines(
        IReadOnlyList<string> preamble,
        string? title,
        IReadOnlyDictionary<string, List<string>> sections,
        ToolHelpUsageSectionSplitter.ToolHelpUsageSectionParts usageSectionParts,
        ToolHelpTrailingStructuredBlockInference.ToolHelpTrailingStructuredBlock trailingStructuredBlock)
    {
        var rawArgumentLines = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        AppendDistinctLines(rawArgumentLines, seen, sections.TryGetValue("arguments", out var argumentLines) ? argumentLines : []);
        AppendDistinctLines(rawArgumentLines, seen, usageSectionParts.ArgumentLines);
        AppendDistinctLines(rawArgumentLines, seen, ToolHelpPreambleArgumentInference.InferArgumentLines(preamble, title));
        AppendDistinctLines(rawArgumentLines, seen, trailingStructuredBlock.ArgumentLines);
        return rawArgumentLines;
    }

    public static IReadOnlyList<string> BuildRawOptionLines(
        IReadOnlyList<string> allLines,
        IReadOnlyList<string> preamble,
        string? title,
        IReadOnlyDictionary<string, List<string>> sections,
        IReadOnlyList<string> parsedUsageLines,
        ToolHelpUsageSectionSplitter.ToolHelpUsageSectionParts usageSectionParts,
        ToolHelpTrailingStructuredBlockInference.ToolHelpTrailingStructuredBlock trailingStructuredBlock,
        IReadOnlyList<string> optionStyleArgumentLines,
        IReadOnlyList<string> rawArgumentLines,
        out IReadOnlyList<string> updatedRawArgumentLines)
    {
        var optionCandidateLines = preamble
            .Skip(string.IsNullOrWhiteSpace(title) ? 0 : 1)
            .Concat(trailingStructuredBlock.OptionLines)
            .ToArray();
        IReadOnlyList<string> seededOptionLines = sections.TryGetValue("options", out var optionLines)
            ? optionLines
            : ToolHelpLegacyOptionTable.InferOptionLines(optionCandidateLines, parsedUsageLines);
        var updatedArguments = rawArgumentLines.ToList();
        var argumentLineSet = new HashSet<string>(updatedArguments, StringComparer.Ordinal);

        if (!sections.ContainsKey("options"))
        {
            ToolHelpItemParser.SplitArgumentSectionLines(seededOptionLines, out var inferredArgumentLines, out var inferredOptionLines);
            AppendDistinctLines(updatedArguments, argumentLineSet, inferredArgumentLines);
            seededOptionLines = inferredOptionLines;
        }

        var fullTextInferredOptionLines = ToolHelpLegacyOptionTable.InferOptionLines(allLines, parsedUsageLines);
        ToolHelpItemParser.SplitArgumentSectionLines(fullTextInferredOptionLines, out var fullTextInferredArgumentLines, out var fullTextInferredOptionOnlyLines);
        AppendDistinctLines(updatedArguments, argumentLineSet, fullTextInferredArgumentLines);

        var rawOptionLines = new List<string>();
        var optionLineSet = new HashSet<string>(StringComparer.Ordinal);
        AppendDistinctLines(rawOptionLines, optionLineSet, seededOptionLines);
        AppendDistinctLines(rawOptionLines, optionLineSet, fullTextInferredOptionOnlyLines);
        AppendDistinctLines(rawOptionLines, optionLineSet, usageSectionParts.OptionLines);
        AppendDistinctLines(rawOptionLines, optionLineSet, optionStyleArgumentLines);

        updatedRawArgumentLines = updatedArguments;
        return rawOptionLines;
    }

    private static void AppendDistinctLines(ICollection<string> target, ISet<string> seen, IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (seen.Add(line))
            {
                target.Add(line);
            }
        }
    }
}

