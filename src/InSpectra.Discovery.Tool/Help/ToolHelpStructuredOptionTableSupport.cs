using System.Text.RegularExpressions;

internal static partial class ToolHelpStructuredOptionTableSupport
{
    public static IReadOnlyList<string> TryExtractStructuredOptionLines(IReadOnlyList<string> lines)
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

    private static bool TryGetMarkdownOptionTableSchema(string line, out StructuredOptionTableSchema schema)
    {
        schema = default;
        return TrySplitMarkdownTableRow(line, out var cells)
            && TryCreateStructuredOptionTableSchema(cells, out schema);
    }

    private static bool TryGetBoxOptionTableSchema(string line, out StructuredOptionTableSchema schema)
    {
        schema = default;
        return TrySplitBoxTableRow(line, out var cells)
            && TryCreateStructuredOptionTableSchema(cells, out schema);
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
        => TrySplitMarkdownTableRow(line, out var cells)
            && cells.Count > 0
            && cells.All(cell => cell.All(ch => ch is '-' or ':' or ' '));

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
