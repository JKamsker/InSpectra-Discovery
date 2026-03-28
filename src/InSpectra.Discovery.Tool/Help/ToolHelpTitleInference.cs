using System.Text.RegularExpressions;

internal static partial class ToolHelpTitleInference
{
    public static (string? Title, string? Version, int DescriptionStartIndex) ParseTitleAndVersion(IReadOnlyList<string> preamble)
    {
        int? firstNonEmptyIndex = null;
        string? markdownTitle = null;
        string? usageDerivedTitle = null;

        for (var index = 0; index < preamble.Count; index++)
        {
            var line = preamble[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            firstNonEmptyIndex ??= index;
            if (index > firstNonEmptyIndex.Value && string.IsNullOrWhiteSpace(preamble[index - 1]))
            {
                break;
            }

            var trimmed = line.Trim();
            markdownTitle ??= TryGetMarkdownTitle(trimmed);
            usageDerivedTitle ??= TryGetUsageDerivedTitle(trimmed);

            var match = TitleLineRegex().Match(trimmed);
            if (!match.Success)
            {
                continue;
            }

            var title = match.Groups["title"].Value.Trim();
            var version = match.Groups["version"].Value.Trim();
            if (LooksLikeTitleVersionLine(trimmed, title, version))
            {
                return (title, version, index + 1);
            }
        }

        if (firstNonEmptyIndex is null)
        {
            return (null, null, 0);
        }

        if (!string.IsNullOrWhiteSpace(markdownTitle))
        {
            return (markdownTitle, null, firstNonEmptyIndex.Value + 1);
        }

        var firstLine = preamble[firstNonEmptyIndex.Value].Trim();
        if (LooksLikeStatusTitle(firstLine) && !string.IsNullOrWhiteSpace(usageDerivedTitle))
        {
            return (usageDerivedTitle, null, firstNonEmptyIndex.Value + 1);
        }

        return (firstLine, null, firstNonEmptyIndex.Value + 1);
    }

    private static string? TryGetMarkdownTitle(string line)
    {
        var match = MarkdownTitleRegex().Match(line);
        return match.Success ? match.Groups["title"].Value.Trim() : null;
    }

    private static string? TryGetUsageDerivedTitle(string line)
    {
        if (!line.Contains('[', StringComparison.Ordinal)
            && !line.Contains('<', StringComparison.Ordinal)
            && !line.Contains("--", StringComparison.Ordinal))
        {
            return null;
        }

        var token = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(token) || !LooksLikeUsageTitleToken(token)
            ? null
            : token;
    }

    private static bool LooksLikeUsageTitleToken(string token)
        => char.IsLetter(token[0])
            && token.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.');

    private static bool LooksLikeStatusTitle(string line)
        => line.StartsWith("running ", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeTitleVersionLine(string line, string title, string version)
        => !StackTraceLineRegex().IsMatch(line)
            && !title.Contains(":line", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(title, "Version", StringComparison.OrdinalIgnoreCase)
            && version.Count(char.IsDigit) > 1;

    [GeneratedRegex(@"^(?<title>.+?)\s+(?<version>v?\d[\w\.\-\+]*)$", RegexOptions.Compiled)]
    private static partial Regex TitleLineRegex();

    [GeneratedRegex(@"^#\s+(?<title>\S.*)$", RegexOptions.Compiled)]
    private static partial Regex MarkdownTitleRegex();

    [GeneratedRegex(@"^\s*at\s+.+\s+in\s+.+:line\s+\d+\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex StackTraceLineRegex();
}
