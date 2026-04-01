namespace InSpectra.Discovery.Tool.Help.Parsing;

using InSpectra.Discovery.Tool.Help.Inference.Text;
using InSpectra.Discovery.Tool.Help.Signatures;

using System.Text.RegularExpressions;

internal static partial class LegacyOptionRowSupport
{
    public static int FindLegacyTableHeaderIndex(IReadOnlyList<string> lines)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (IsLegacyTableHeader(lines[index]))
            {
                return index;
            }
        }

        return -1;
    }

    public static bool TryBuildTableRow(string rawLine, ISet<string> usageArguments, out string syntheticLine)
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

    public static bool TryBuildCommandLineParserBlockRow(string rawLine, out string rowLine)
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

    public static bool ShouldContinueCommandLineParserOptionRow(string rawLine)
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

        return !StructuredHeadingRegex().IsMatch(trimmed)
            && !TextNoiseClassifier.ShouldIgnoreSectionLine(trimmed)
            && !TextNoiseClassifier.LooksLikeHelpHintFooter(trimmed);
    }

    public static bool LooksLikeLooseOptionRow(string rawLine)
    {
        var trimmed = rawLine.TrimStart();
        if (CommandPrototypeSupport.LooksLikeBareShortLongOptionRow(rawLine))
        {
            return true;
        }

        if (SignatureNormalizer.LooksLikeOptionSignature(trimmed))
        {
            return true;
        }

        var optionMatch = OptionTokenRegex().Match(trimmed);
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
            || StartsWithAdditionalOptionToken(trimmedRemainder);
    }

    public static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();

    public static HashSet<string> ExtractUsageArgumentNames(IReadOnlyList<string> usageLines)
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

    private static bool IsLegacyTableHeader(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Contains("Description", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains("Option", StringComparison.OrdinalIgnoreCase);
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
