using System.Text.RegularExpressions;

internal static partial class ToolHelpLegacyOptionTable
{
    public static IReadOnlyList<string> InferOptionLines(
        IReadOnlyList<string> preamble,
        string? title,
        IReadOnlyList<string> usageLines)
    {
        var candidateLines = preamble.Skip(string.IsNullOrWhiteSpace(title) ? 0 : 1).ToArray();
        var tableLines = TryExtractTableLines(candidateLines, usageLines);
        return tableLines.Count > 0 ? tableLines : candidateLines;
    }

    private static IReadOnlyList<string> TryExtractTableLines(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> usageLines)
    {
        var headerIndex = Array.FindIndex(lines.ToArray(), IsTableHeader);
        if (headerIndex < 0)
        {
            return [];
        }

        var usageArguments = ExtractUsageArgumentNames(usageLines);
        var results = new List<string>();
        var hasRows = false;
        var currentRowCaptured = false;

        foreach (var rawLine in lines.Skip(headerIndex + 1))
        {
            if (TryBuildRow(rawLine, usageArguments, out var syntheticLine))
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

    private static bool TryBuildRow(string rawLine, ISet<string> usageArguments, out string syntheticLine)
    {
        syntheticLine = string.Empty;
        var match = TableRowRegex().Match(rawLine);
        if (!match.Success)
        {
            return false;
        }

        var rowName = match.Groups["name"].Value.TrimEnd('*').Trim();
        if (usageArguments.Contains(rowName))
        {
            return false;
        }

        var shortOption = match.Groups["short"].Value.Trim();
        var description = match.Groups["description"].Value.Trim();
        var longOption = "--" + KebabCaseRegex().Replace(rowName, "-$1").ToLowerInvariant();
        syntheticLine = $"{shortOption}, {longOption}  {description}";
        return true;
    }

    private static bool IsTableHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Contains("Description", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains("Option", StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> ExtractUsageArgumentNames(IReadOnlyList<string> usageLines)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in usageLines)
        {
            foreach (Match match in UsageArgumentRegex().Matches(line))
            {
                names.Add(match.Groups["name"].Value.Trim());
            }
        }

        return names;
    }

    [GeneratedRegex(@"^(?<name>[A-Za-z][A-Za-z0-9]*)\*?\s+\((?<short>-[A-Za-z][A-Za-z0-9]*)\)\s{2,}(?<description>\S.*)$", RegexOptions.Compiled)]
    private static partial Regex TableRowRegex();

    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex KebabCaseRegex();

    [GeneratedRegex(@"\[?<(?<name>[^>]+)>\]?", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();
}
