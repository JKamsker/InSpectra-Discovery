namespace InSpectra.Discovery.Tool.Help.Parsing;

using InSpectra.Discovery.Tool.Help.Inference.Text;

using System.Text.RegularExpressions;

internal static partial class StructuredOptionTableParsingSupport
{
    public static bool TryGetMarkdownOptionTableSchema(string line, out StructuredOptionTableSchema schema)
    {
        schema = default;
        return TrySplitMarkdownTableRow(line, out var cells)
            && TryCreateStructuredOptionTableSchema(cells, out schema);
    }

    public static bool TryGetBoxOptionTableSchema(string line, out StructuredOptionTableSchema schema)
    {
        schema = default;
        return TrySplitBoxTableRow(line, out var cells)
            && TryCreateStructuredOptionTableSchema(cells, out schema);
    }

    public static bool IsMarkdownTableSeparator(string line)
        => TrySplitMarkdownTableRow(line, out var cells)
            && cells.Count > 0
            && cells.All(cell => cell.All(ch => ch is '-' or ':' or ' '));

    public static bool IsBoxTableBorder(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length > 0
            && trimmed.All(ch => ch is '┌' or '┐' or '└' or '┘' or '├' or '┤' or '┬' or '┴' or '┼' or '─');
    }

    public static bool TrySplitMarkdownTableRow(string line, out IReadOnlyList<string> cells)
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

    public static bool TrySplitBoxTableRow(string line, out IReadOnlyList<string> cells)
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

    public static StructuredRowKind TryBuildStructuredRow(
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
            if (string.IsNullOrWhiteSpace(description)
                || TextNoiseClassifier.ShouldIgnoreSectionLine(description))
            {
                return StructuredRowKind.None;
            }

            syntheticLine = description;
            return StructuredRowKind.Continuation;
        }

        if (string.IsNullOrWhiteSpace(description)
            || TextNoiseClassifier.ShouldIgnoreSectionLine(description)
            || (!optionSpec.StartsWith("-", StringComparison.Ordinal) && !optionSpec.StartsWith("/", StringComparison.Ordinal)))
        {
            return StructuredRowKind.None;
        }

        syntheticLine = $"{NormalizeOptionSpec(optionSpec)}  {description}";
        return StructuredRowKind.Entry;
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

    private static string NormalizeOptionSpec(string optionSpec)
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

    internal readonly record struct StructuredOptionTableSchema(
        int DescriptionIndex,
        IReadOnlyList<int> OptionColumnIndexes);

    internal enum StructuredRowKind
    {
        None,
        Entry,
        Continuation,
    }
}
