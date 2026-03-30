using System.Text.RegularExpressions;

internal static partial class ToolHelpLegacyOptionTable
{
    public static IReadOnlyList<string> NormalizeOptionLines(IReadOnlyList<string> lines)
    {
        var structuredTableLines = ToolHelpStructuredOptionTableSupport.TryExtractStructuredOptionLines(lines);
        return structuredTableLines.Count > 0 ? structuredTableLines : lines;
    }

    public static IReadOnlyList<string> InferOptionLines(
        IReadOnlyList<string> candidateLines,
        IReadOnlyList<string> usageLines)
    {
        if (ToolHelpRootCommandInventoryInference.LooksLikeAliasCommandInventoryBlock(candidateLines))
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
        var currentRowIndentation = -1;

        foreach (var rawLine in lines)
        {
            if (TryBuildCommandLineParserBlockRow(rawLine, out var rowLine))
            {
                results.Add(rowLine);
                hasRows = true;
                capturedAnyBlockRows = true;
                currentRowCaptured = true;
                currentRowIndentation = GetIndentation(rowLine);
                continue;
            }

            if (currentRowCaptured && ShouldContinueCommandLineParserOptionRow(rawLine, currentRowIndentation))
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
        var structuredTableLines = ToolHelpStructuredOptionTableSupport.TryExtractStructuredOptionLines(lines);
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

    private static bool ShouldContinueCommandLineParserOptionRow(string rawLine, int currentRowIndentation)
    {
        if (rawLine.Length == 0 || !char.IsWhiteSpace(rawLine, 0))
        {
            return false;
        }

        var trimmed = rawLine.TrimStart();
        if (PositionalArgumentRowRegex().IsMatch(trimmed)
            || LooksLikeLooseOptionRow(rawLine))
        {
            return false;
        }

        return !StructuredHeadingRegex().IsMatch(trimmed);
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
        var trimmedEnd = rawLine.TrimEnd();
        var match = CommandLineParserOptionRowRegex().Match(trimmedEnd);
        if (!match.Success)
        {
            return false;
        }

        var shortName = match.Groups["short"].Value.Trim();
        var longName = match.Groups["long"].Value.Trim();
        var description = match.Groups["description"].Success
            ? match.Groups["description"].Value.Trim()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(shortName)
            || string.IsNullOrWhiteSpace(longName))
        {
            return false;
        }

        var indentation = rawLine[..GetIndentation(rawLine)];
        syntheticLine = string.IsNullOrWhiteSpace(description)
            ? $"{indentation}-{shortName}, --{longName}"
            : $"{indentation}-{shortName}, --{longName}  {description}";
        return true;
    }

    private static bool TryBuildCommandLineParserBlockRow(string rawLine, out string rowLine)
    {
        rowLine = string.Empty;
        if (TryBuildCommandLineParserOptionRow(rawLine, out var syntheticLine))
        {
            rowLine = syntheticLine;
            return true;
        }

        if (LooksLikeLooseOptionRow(rawLine) || PositionalArgumentRowRegex().IsMatch(rawLine.TrimStart()))
        {
            rowLine = rawLine;
            return true;
        }

        return false;
    }

    private static bool IsLegacyTableHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Contains("Description", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains("Option", StringComparison.OrdinalIgnoreCase);
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
        if (ToolHelpCommandPrototypeSupport.LooksLikeBareShortLongOptionRow(rawLine))
        {
            return true;
        }

        if (!optionMatch.Success || optionMatch.Index != 0)
        {
            return false;
        }

        var remainder = trimmed[optionMatch.Length..];
        var trimmedRemainder = remainder.TrimStart();
        return string.IsNullOrWhiteSpace(remainder)
            || remainder.Contains("  ", StringComparison.Ordinal)
            || trimmedRemainder.StartsWith("<", StringComparison.Ordinal)
            || trimmedRemainder.StartsWith("[", StringComparison.Ordinal)
            || StartsWithAdditionalOptionToken(trimmedRemainder)
            || ToolHelpCommandPrototypeSupport.LooksLikeBareShortLongOptionRow(rawLine);
    }

    private static bool StartsWithAdditionalOptionToken(string remainder)
    {
        if (!(remainder.StartsWith(",", StringComparison.Ordinal) || remainder.StartsWith("|", StringComparison.Ordinal)))
        {
            return false;
        }

        var candidate = remainder[1..].TrimStart();
        return candidate.StartsWith("-", StringComparison.Ordinal) || candidate.StartsWith("/", StringComparison.Ordinal);
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

    [GeneratedRegex(@"^\s*(?<short>[A-Za-z0-9\?])\s*,\s*(?<long>[A-Za-z][A-Za-z0-9_\.\-]*)(?:\s{2,}(?<description>\S.*))?$", RegexOptions.Compiled)]
    private static partial Regex CommandLineParserOptionRowRegex();

    [GeneratedRegex(@"^\S(?:.*?\S)?\s+(?:\(pos\.\s*\d+\)|pos\.\s*\d+)(?:\s+\S.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PositionalArgumentRowRegex();

    [GeneratedRegex(@"^(?:#+\s*[A-Za-z][\p{L}\p{M}\s]*|[A-Za-z][\p{L}\p{M}\s]+:)\s*$", RegexOptions.Compiled)]
    private static partial Regex StructuredHeadingRegex();

    [GeneratedRegex(@"(?<=[a-z0-9])([A-Z])", RegexOptions.Compiled)]
    private static partial Regex KebabCaseRegex();

    [GeneratedRegex(@"\[?<(?<name>[^>]+)>\]?", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();

    [GeneratedRegex(@"(?<option>(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*))", RegexOptions.Compiled)]
    private static partial Regex OptionTokenRegex();

}
