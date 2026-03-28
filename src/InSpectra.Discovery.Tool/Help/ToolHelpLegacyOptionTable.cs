using System.Text.RegularExpressions;

internal static partial class ToolHelpLegacyOptionTable
{
    public static IReadOnlyList<string> NormalizeOptionLines(IReadOnlyList<string> lines)
    {
        var structuredTableLines = TryExtractStructuredOptionLines(lines);
        return structuredTableLines.Count > 0 ? structuredTableLines : lines;
    }

    public static IReadOnlyList<string> InferOptionLines(
        IReadOnlyList<string> preamble,
        string? title,
        IReadOnlyList<string> usageLines)
    {
        var candidateLines = preamble.Skip(string.IsNullOrWhiteSpace(title) ? 0 : 1).ToArray();
        var commandLineParserOptionLines = TryExtractCommandLineParserOptionLines(candidateLines);
        if (commandLineParserOptionLines.Count > 0)
        {
            return commandLineParserOptionLines;
        }

        var tableLines = TryExtractTableLines(candidateLines, usageLines);
        return tableLines.Count > 0 ? tableLines : candidateLines;
    }

    private static IReadOnlyList<string> TryExtractCommandLineParserOptionLines(IReadOnlyList<string> lines)
    {
        var results = new List<string>();
        var hasRows = false;
        var currentRowCaptured = false;

        foreach (var rawLine in lines)
        {
            if (TryBuildCommandLineParserOptionRow(rawLine, out var syntheticLine))
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

    private static IReadOnlyList<string> TryExtractTableLines(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> usageLines)
    {
        var structuredTableLines = TryExtractStructuredOptionLines(lines);
        if (structuredTableLines.Count > 0)
        {
            return structuredTableLines;
        }

        var headerIndex = Array.FindIndex(lines.ToArray(), IsLegacyTableHeader);
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

    private static IReadOnlyList<string> TryExtractStructuredOptionLines(IReadOnlyList<string> lines)
    {
        var markdownTableLines = TryExtractMarkdownTableLines(lines);
        if (markdownTableLines.Count > 0)
        {
            return markdownTableLines;
        }

        var boxTableLines = TryExtractBoxTableLines(lines);
        return boxTableLines.Count > 0 ? boxTableLines : [];
    }

    private static IReadOnlyList<string> TryExtractMarkdownTableLines(IReadOnlyList<string> lines)
    {
        var headerIndex = Array.FindIndex(lines.ToArray(), IsMarkdownOptionTableHeader);
        if (headerIndex < 0)
        {
            return [];
        }

        var results = new List<string>();
        var hasRows = false;

        foreach (var rawLine in lines.Skip(headerIndex + 1))
        {
            if (IsMarkdownTableSeparator(rawLine))
            {
                continue;
            }

            if (TryBuildMarkdownRow(rawLine, out var syntheticLine))
            {
                results.Add(syntheticLine);
                hasRows = true;
                continue;
            }

            if (hasRows)
            {
                break;
            }
        }

        return hasRows ? results : [];
    }

    private static IReadOnlyList<string> TryExtractBoxTableLines(IReadOnlyList<string> lines)
    {
        var headerIndex = Array.FindIndex(lines.ToArray(), IsBoxOptionTableHeader);
        if (headerIndex < 0)
        {
            return [];
        }

        var results = new List<string>();
        var hasRows = false;
        var currentRowCaptured = false;

        foreach (var rawLine in lines.Skip(headerIndex + 1))
        {
            if (IsBoxTableBorder(rawLine))
            {
                continue;
            }

            if (!TrySplitBoxTableRow(rawLine, out var cells) || cells.Count < 2)
            {
                if (hasRows)
                {
                    break;
                }

                continue;
            }

            var optionSpec = cells[0];
            var description = cells[1];
            if (!string.IsNullOrWhiteSpace(optionSpec)
                && !string.IsNullOrWhiteSpace(description)
                && (optionSpec.StartsWith("-", StringComparison.Ordinal) || optionSpec.StartsWith("/", StringComparison.Ordinal)))
            {
                results.Add($"{NormalizeMarkdownOptionSpec(optionSpec)}  {description}");
                hasRows = true;
                currentRowCaptured = true;
                continue;
            }

            if (currentRowCaptured && string.IsNullOrWhiteSpace(optionSpec) && !string.IsNullOrWhiteSpace(description))
            {
                results.Add(description);
                continue;
            }

            if (hasRows)
            {
                break;
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

    private static bool TryBuildCommandLineParserOptionRow(string rawLine, out string syntheticLine)
    {
        syntheticLine = string.Empty;
        var match = CommandLineParserOptionRowRegex().Match(rawLine);
        if (!match.Success)
        {
            return false;
        }

        var shortName = match.Groups["short"].Value.Trim();
        var longName = match.Groups["long"].Value.Trim();
        var description = match.Groups["description"].Value.Trim();
        if (string.IsNullOrWhiteSpace(shortName)
            || string.IsNullOrWhiteSpace(longName)
            || string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        syntheticLine = $"-{shortName}, --{longName}  {description}";
        return true;
    }

    private static bool TryBuildMarkdownRow(string rawLine, out string syntheticLine)
    {
        syntheticLine = string.Empty;
        if (!TrySplitMarkdownTableRow(rawLine, out var cells) || cells.Count < 2)
        {
            return false;
        }

        var optionSpec = cells[0];
        var description = cells[1];
        if (string.IsNullOrWhiteSpace(optionSpec)
            || string.IsNullOrWhiteSpace(description)
            || (!optionSpec.StartsWith("-", StringComparison.Ordinal) && !optionSpec.StartsWith("/", StringComparison.Ordinal)))
        {
            return false;
        }

        syntheticLine = $"{NormalizeMarkdownOptionSpec(optionSpec)}  {description}";
        return true;
    }

    private static bool IsLegacyTableHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Contains("Description", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains("Option", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMarkdownOptionTableHeader(string line)
    {
        if (!TrySplitMarkdownTableRow(line, out var cells))
        {
            return false;
        }

        return cells.Count >= 2
            && cells.Any(cell => cell.Contains("description", StringComparison.OrdinalIgnoreCase))
            && cells.Any(cell =>
                cell.Contains("argument", StringComparison.OrdinalIgnoreCase)
                || cell.Contains("arguments", StringComparison.OrdinalIgnoreCase)
                || cell.Contains("option", StringComparison.OrdinalIgnoreCase)
                || cell.Contains("options", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBoxOptionTableHeader(string line)
    {
        if (!TrySplitBoxTableRow(line, out var cells))
        {
            return false;
        }

        return cells.Count >= 2
            && cells.Any(cell => cell.Contains("description", StringComparison.OrdinalIgnoreCase))
            && cells.Any(cell =>
                cell.Contains("argument", StringComparison.OrdinalIgnoreCase)
                || cell.Contains("arguments", StringComparison.OrdinalIgnoreCase)
                || cell.Contains("option", StringComparison.OrdinalIgnoreCase)
                || cell.Contains("options", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsMarkdownTableSeparator(string line)
    {
        if (!TrySplitMarkdownTableRow(line, out var cells) || cells.Count == 0)
        {
            return false;
        }

        return cells.All(cell => cell.All(ch => ch is '-' or ':' or ' '));
    }

    private static bool IsBoxTableBorder(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 0
            && trimmed.All(ch => ch is '┌' or '┐' or '└' or '┘' or '├' or '┤' or '┬' or '┴' or '┼' or '─');
    }

    private static bool TrySplitMarkdownTableRow(string line, out IReadOnlyList<string> cells)
    {
        cells = [];
        var trimmed = line.Trim();
        if (trimmed.Length < 2
            || !trimmed.StartsWith("|", StringComparison.Ordinal)
            || !trimmed.EndsWith("|", StringComparison.Ordinal))
        {
            return false;
        }

        cells = trimmed[1..^1]
            .Split('|', StringSplitOptions.TrimEntries)
            .Select(cell => cell.Trim())
            .ToArray();
        return cells.Count > 0;
    }

    private static bool TrySplitBoxTableRow(string line, out IReadOnlyList<string> cells)
    {
        cells = [];
        var trimmed = line.Trim();
        if (trimmed.Length < 2
            || !trimmed.StartsWith('│')
            || !trimmed.EndsWith('│'))
        {
            return false;
        }

        cells = trimmed[1..^1]
            .Split('│', StringSplitOptions.TrimEntries)
            .Select(cell => cell.Trim())
            .ToArray();
        return cells.Count > 0;
    }

    private static string NormalizeMarkdownOptionSpec(string optionSpec)
    {
        var matches = OptionTokenRegex().Matches(optionSpec);
        if (matches.Count == 0)
        {
            return optionSpec.Trim();
        }

        var lastMatch = matches[^1];
        var normalized = optionSpec[..(lastMatch.Index + lastMatch.Length)].Trim();
        var trailing = optionSpec[(lastMatch.Index + lastMatch.Length)..].Trim();
        if (trailing.StartsWith("<", StringComparison.Ordinal) || trailing.StartsWith("[", StringComparison.Ordinal))
        {
            return $"{normalized} {trailing}";
        }

        if (!IsBarePlaceholder(trailing))
        {
            return normalized;
        }

        return $"{normalized} <{trailing.ToUpperInvariant()}>";
    }

    private static bool IsBarePlaceholder(string trailing)
        => !string.IsNullOrWhiteSpace(trailing)
            && !trailing.Contains(' ', StringComparison.Ordinal)
            && !trailing.StartsWith("<", StringComparison.Ordinal)
            && !trailing.StartsWith("[", StringComparison.Ordinal)
            && !trailing.StartsWith("-", StringComparison.Ordinal)
            && !trailing.StartsWith("/", StringComparison.Ordinal);

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

    [GeneratedRegex(@"^\s*(?<short>[A-Za-z0-9\?])\s*,\s*(?<long>[A-Za-z][A-Za-z0-9\-]*)\s{2,}(?<description>\S.*)$", RegexOptions.Compiled)]
    private static partial Regex CommandLineParserOptionRowRegex();

    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex KebabCaseRegex();

    [GeneratedRegex(@"\[?<(?<name>[^>]+)>\]?", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();

    [GeneratedRegex(@"(?<option>(?:--[A-Za-z0-9][A-Za-z0-9\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9\?\-]*|/[A-Za-z0-9][A-Za-z0-9\?\-]*))", RegexOptions.Compiled)]
    private static partial Regex OptionTokenRegex();
}
