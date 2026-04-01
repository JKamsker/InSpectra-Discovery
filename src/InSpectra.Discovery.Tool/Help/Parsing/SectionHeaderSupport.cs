namespace InSpectra.Discovery.Tool.Help.Parsing;

using System.Text.RegularExpressions;

internal static partial class SectionHeaderSupport
{
    public static bool TryParseSectionHeader(
        string line,
        IReadOnlyDictionary<string, string> sectionAliases,
        out string sectionName,
        out string? inlineValue,
        out string matchedHeader)
    {
        sectionName = string.Empty;
        inlineValue = null;
        matchedHeader = string.Empty;

        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (sectionAliases.TryGetValue(trimmed, out var matchedSectionName))
        {
            sectionName = matchedSectionName;
            matchedHeader = trimmed;
            return true;
        }

        var match = SectionHeaderRegex().Match(trimmed);
        if (!match.Success)
        {
            match = MarkdownSectionHeaderRegex().Match(trimmed);
            if (!match.Success)
            {
                return false;
            }
        }

        var alias = match.Groups["header"].Value.Trim();
        if (!sectionAliases.TryGetValue(alias, out matchedSectionName)
            && !HelpSectionCatalog.TryResolveAlias(alias, out matchedSectionName))
        {
            return false;
        }

        matchedHeader = alias;
        sectionName = matchedSectionName;
        inlineValue = string.IsNullOrWhiteSpace(match.Groups["value"].Value)
            ? null
            : match.Groups["value"].Value.Trim();
        return true;
    }

    public static bool TryParseIgnoredSectionHeader(string line, IReadOnlySet<string> ignoredHeaders)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        var match = SectionHeaderRegex().Match(trimmed);
        if (!match.Success)
        {
            match = MarkdownSectionHeaderRegex().Match(trimmed);
            if (!match.Success)
            {
                return false;
            }
        }

        return ignoredHeaders.Contains(match.Groups["header"].Value.Trim());
    }

    public static bool LooksLikeUnrecognizedMarkdownSectionHeader(
        string line,
        IReadOnlyDictionary<string, string> sectionAliases,
        IReadOnlySet<string> ignoredHeaders)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        var match = MarkdownSectionHeaderRegex().Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        var alias = match.Groups["header"].Value.Trim();
        return !sectionAliases.ContainsKey(alias)
            && !HelpSectionCatalog.TryResolveAlias(alias, out _)
            && !ignoredHeaders.Contains(alias);
    }

    [GeneratedRegex(@"^(?<header>[\p{L}\p{M}\s]+):\s*(?<value>\S.*)?$", RegexOptions.Compiled)]
    private static partial Regex SectionHeaderRegex();

    [GeneratedRegex(@"^#+\s*(?<header>[\p{L}\p{M}\s]+?)(?:\s*:\s*(?<value>\S.*))?$", RegexOptions.Compiled)]
    private static partial Regex MarkdownSectionHeaderRegex();
}
