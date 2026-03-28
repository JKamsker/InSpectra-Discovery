using System.Text.RegularExpressions;

internal static partial class ToolHelpLegacyOptionTable
{
    public static IReadOnlyList<string> NormalizeOptionLines(IReadOnlyList<string> lines)
    {
        var structuredTableLines = TryExtractStructuredOptionLines(lines);
        return structuredTableLines.Count > 0 ? structuredTableLines : lines;
    }

    public static IReadOnlyList<string> InferOptionLines(
        IReadOnlyList<string> candidateLines,
        IReadOnlyList<string> usageLines)
    {
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

            if (currentRowCaptured && GetIndentation(rawLine) > 0)
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
        var headerIndex = -1;
        StructuredOptionTableSchema? schema = null;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!TryGetMarkdownOptionTableSchema(lines[index], out var candidateSchema))
            {
                continue;
            }

            headerIndex = index;
            schema = candidateSchema;
            break;
        }

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

            if (!TrySplitMarkdownTableRow(rawLine, out var cells) || schema is null)
            {
                if (hasRows)
                {
                    break;
                }

                continue;
            }

            var rowKind = TryBuildStructuredRow(cells, schema.Value, out var syntheticLine);
            if (rowKind == StructuredRowKind.Entry)
            {
                results.Add(syntheticLine);
                hasRows = true;
                continue;
            }

            if (rowKind == StructuredRowKind.Continuation && hasRows)
            {
                results.Add(syntheticLine);
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
        var headerIndex = -1;
        StructuredOptionTableSchema? schema = null;
        for (var index = 0; index < lines.Count; index++)
        {
            if (!TryGetBoxOptionTableSchema(lines[index], out var candidateSchema))
            {
                continue;
            }

            headerIndex = index;
            schema = candidateSchema;
            break;
        }

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

            if (!TrySplitBoxTableRow(rawLine, out var cells) || schema is null)
            {
                if (hasRows)
                {
                    break;
                }

                continue;
            }

            var rowKind = TryBuildStructuredRow(cells, schema.Value, out var syntheticLine);
            if (rowKind == StructuredRowKind.Entry)
            {
                results.Add(syntheticLine);
                hasRows = true;
                currentRowCaptured = true;
                continue;
            }

            if (rowKind == StructuredRowKind.Continuation && currentRowCaptured)
            {
                results.Add(syntheticLine);
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

    private static StructuredRowKind TryBuildStructuredRow(
        IReadOnlyList<string> cells,
        StructuredOptionTableSchema schema,
        out string syntheticLine)
    {
        syntheticLine = string.Empty;
        if (schema.DescriptionIndex >= cells.Count)
        {
            return StructuredRowKind.None;
        }

        var description = cells[schema.DescriptionIndex].Trim();
        var optionParts = schema.OptionColumnIndexes
            .Where(index => index < cells.Count)
            .Select(index => cells[index].Trim())
            .ToArray();
        var optionSpec = string.Join(", ", optionParts.Where(part => !string.IsNullOrWhiteSpace(part)));

        if (string.IsNullOrWhiteSpace(optionSpec))
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return StructuredRowKind.None;
            }

            syntheticLine = description;
            return StructuredRowKind.Continuation;
        }

        if (string.IsNullOrWhiteSpace(description)
            || (!optionSpec.StartsWith("-", StringComparison.Ordinal) && !optionSpec.StartsWith("/", StringComparison.Ordinal)))
        {
            return StructuredRowKind.None;
        }

        syntheticLine = $"{NormalizeMarkdownOptionSpec(optionSpec)}  {description}";
        return StructuredRowKind.Entry;
    }

    private static bool IsLegacyTableHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Contains("Description", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains("Option", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMarkdownOptionTableHeader(string line)
        => TryGetMarkdownOptionTableSchema(line, out _);

    private static bool IsBoxOptionTableHeader(string line)
        => TryGetBoxOptionTableSchema(line, out _);

    private static bool TryGetMarkdownOptionTableSchema(string line, out StructuredOptionTableSchema schema)
    {
        schema = default;
        if (!TrySplitMarkdownTableRow(line, out var cells))
        {
            return false;
        }

        return TryCreateStructuredOptionTableSchema(cells, out schema);
    }

    private static bool TryGetBoxOptionTableSchema(string line, out StructuredOptionTableSchema schema)
    {
        schema = default;
        if (!TrySplitBoxTableRow(line, out var cells))
        {
            return false;
        }

        return TryCreateStructuredOptionTableSchema(cells, out schema);
    }

    private static bool TryCreateStructuredOptionTableSchema(
        IReadOnlyList<string> headerCells,
        out StructuredOptionTableSchema schema)
    {
        schema = default;
        if (headerCells.Count < 2)
        {
            return false;
        }

        var descriptionIndex = -1;
        var optionColumnIndexes = new List<int>();

        for (var index = 0; index < headerCells.Count; index++)
        {
            var header = headerCells[index].Trim();
            if (header.Contains("description", StringComparison.OrdinalIgnoreCase))
            {
                descriptionIndex = index;
                continue;
            }

            if (LooksLikeStructuredOptionHeader(header))
            {
                optionColumnIndexes.Add(index);
            }
        }

        if (descriptionIndex < 0 || optionColumnIndexes.Count == 0)
        {
            return false;
        }

        schema = new StructuredOptionTableSchema(descriptionIndex, optionColumnIndexes);
        return true;
    }

    private static bool LooksLikeStructuredOptionHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        return header.Contains("argument", StringComparison.OrdinalIgnoreCase)
            || header.Contains("arguments", StringComparison.OrdinalIgnoreCase)
            || header.Contains("option", StringComparison.OrdinalIgnoreCase)
            || header.Contains("options", StringComparison.OrdinalIgnoreCase)
            || header.Contains("parameter", StringComparison.OrdinalIgnoreCase)
            || header.Contains("parameters", StringComparison.OrdinalIgnoreCase)
            || header.Contains("flag", StringComparison.OrdinalIgnoreCase)
            || header.Contains("flags", StringComparison.OrdinalIgnoreCase)
            || header.Contains("alias", StringComparison.OrdinalIgnoreCase)
            || header.Contains("aliases", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "short", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "long", StringComparison.OrdinalIgnoreCase)
            || string.Equals(header, "name", StringComparison.OrdinalIgnoreCase);
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

    private static bool LooksLikeLooseOptionRow(string rawLine)
    {
        var trimmed = rawLine.TrimStart();
        var optionMatch = OptionTokenRegex().Match(trimmed);
        return (optionMatch.Success && optionMatch.Index == 0)
            || ToolHelpCommandPrototypeSupport.LooksLikeBareShortLongOptionRow(rawLine);
    }

    private static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();

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

    [GeneratedRegex(@"^\s*(?<short>[A-Za-z0-9\?])\s*,\s*(?<long>[A-Za-z][A-Za-z0-9_\.\-]*)\s{2,}(?<description>\S.*)$", RegexOptions.Compiled)]
    private static partial Regex CommandLineParserOptionRowRegex();

    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex KebabCaseRegex();

    [GeneratedRegex(@"\[?<(?<name>[^>]+)>\]?", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();

    [GeneratedRegex(@"(?<option>(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*))", RegexOptions.Compiled)]
    private static partial Regex OptionTokenRegex();

    private readonly record struct StructuredOptionTableSchema(
        int DescriptionIndex,
        IReadOnlyList<int> OptionColumnIndexes);

    private enum StructuredRowKind
    {
        None,
        Entry,
        Continuation,
    }
}
