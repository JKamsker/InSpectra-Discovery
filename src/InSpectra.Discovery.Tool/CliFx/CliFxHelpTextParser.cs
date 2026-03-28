using System.Text.RegularExpressions;

internal sealed partial class CliFxHelpTextParser
{
    private static readonly HashSet<string> SectionHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "USAGE",
        "DESCRIPTION",
        "PARAMETERS",
        "OPTIONS",
        "COMMANDS",
    };

    public CliFxHelpDocument Parse(string text)
    {
        var lines = Normalize(text);
        var firstSectionIndex = Array.FindIndex(lines, IsSectionHeader);
        var preamble = firstSectionIndex >= 0 ? lines[..firstSectionIndex] : lines;
        var sections = ParseSections(firstSectionIndex >= 0 ? lines[firstSectionIndex..] : []);

        var (title, version, descriptionStartIndex) = ParseTitleAndVersion(preamble);
        var appDescription = JoinLines(preamble.Skip(descriptionStartIndex));

        sections.TryGetValue("DESCRIPTION", out var descriptionLines);
        sections.TryGetValue("USAGE", out var usageLines);
        sections.TryGetValue("PARAMETERS", out var parameterLines);
        sections.TryGetValue("OPTIONS", out var optionLines);
        sections.TryGetValue("COMMANDS", out var commandLines);

        return new CliFxHelpDocument(
            Title: title,
            Version: version,
            ApplicationDescription: appDescription,
            CommandDescription: JoinLines(descriptionLines ?? []),
            UsageLines: TrimNonEmpty(usageLines ?? []),
            Parameters: ParseItems(parameterLines ?? []),
            Options: ParseItems(optionLines ?? []),
            Commands: ParseItems(commandLines ?? []));
    }

    private static string[] Normalize(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static Dictionary<string, List<string>> ParseSections(IReadOnlyList<string> lines)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        string? currentHeader = null;

        foreach (var line in lines)
        {
            if (IsSectionHeader(line))
            {
                currentHeader = line.Trim();
                sections[currentHeader] = [];
                continue;
            }

            if (currentHeader is not null)
            {
                sections[currentHeader].Add(line);
            }
        }

        return sections;
    }

    private static bool IsSectionHeader(string? line)
        => !string.IsNullOrWhiteSpace(line)
            && SectionHeaders.Contains(line.Trim());

    private static IReadOnlyList<CliFxHelpItem> ParseItems(IReadOnlyList<string> lines)
    {
        var items = new List<CliFxHelpItem>();
        string? key = null;
        string? description = null;
        var isRequired = false;

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var match = ItemStartRegex().Match(rawLine);
            if (match.Success)
            {
                FlushItem(items, key, isRequired, description);
                key = match.Groups["key"].Value.Trim();
                description = match.Groups["description"].Success ? match.Groups["description"].Value.Trim() : null;
                isRequired = string.Equals(match.Groups["prefix"].Value, "* ", StringComparison.Ordinal);
                continue;
            }

            if (key is not null)
            {
                description = string.IsNullOrWhiteSpace(description)
                    ? rawLine.Trim()
                    : $"{description}\n{rawLine.Trim()}";
            }
        }

        FlushItem(items, key, isRequired, description);
        return items;
    }

    private static void FlushItem(ICollection<CliFxHelpItem> items, string? key, bool isRequired, string? description)
    {
        if (!string.IsNullOrWhiteSpace(key))
        {
            items.Add(new CliFxHelpItem(key, isRequired, string.IsNullOrWhiteSpace(description) ? null : description.Trim()));
        }
    }

    private static (string? Title, string? Version, int DescriptionStartIndex) ParseTitleAndVersion(IReadOnlyList<string> preamble)
    {
        int? firstNonEmptyIndex = null;

        for (var index = 0; index < preamble.Count; index++)
        {
            var line = preamble[index];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            firstNonEmptyIndex ??= index;
            var match = TitleLineRegex().Match(line.Trim());
            if (match.Success)
            {
                return (match.Groups["title"].Value.Trim(), match.Groups["version"].Value.Trim(), index + 1);
            }
        }

        if (firstNonEmptyIndex is null)
        {
            return (null, null, 0);
        }

        var firstLine = preamble[firstNonEmptyIndex.Value].Trim();
        return (firstLine, null, firstNonEmptyIndex.Value + 1);
    }

    private static IReadOnlyList<string> TrimNonEmpty(IEnumerable<string> lines)
        => lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    private static string? JoinLines(IEnumerable<string> lines)
    {
        var joined = string.Join("\n", lines.Select(line => line.Trim()).Where(line => line.Length > 0));
        return joined.Length == 0 ? null : joined;
    }

    [GeneratedRegex(@"^(?<prefix>\* |  )(?<key>.+?)(?:\s{2,}(?<description>\S.*))?$", RegexOptions.Compiled)]
    private static partial Regex ItemStartRegex();

    [GeneratedRegex(@"^(?<title>.+?)\s+(?<version>v?\d[\w\.\-\+]*)$", RegexOptions.Compiled)]
    private static partial Regex TitleLineRegex();
}
