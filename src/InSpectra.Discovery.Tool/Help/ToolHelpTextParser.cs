using System.Text.RegularExpressions;

internal sealed partial class ToolHelpTextParser
{
    private static readonly Dictionary<string, string> SectionAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ARGUMENT"] = "arguments",
        ["ARGUMENTE"] = "arguments",
        ["ARGUMENTS"] = "arguments",
        ["BEFEHL"] = "commands",
        ["BEFEHLE"] = "commands",
        ["COMMAND"] = "commands",
        ["COMMANDS"] = "commands",
        ["DESCRIPTION"] = "description",
        ["EXAMPLES"] = "examples",
        ["OPTION"] = "options",
        ["OPTIONEN"] = "options",
        ["OPTIONS"] = "options",
        ["PARAMETER"] = "arguments",
        ["PARAMETERS"] = "arguments",
        ["SUBCOMMANDS"] = "commands",
        ["SYNOPSIS"] = "usage",
        ["USAGE"] = "usage",
        ["VERBS"] = "commands",
        ["VERWENDUNG"] = "usage",
    };
    public ToolHelpDocument Parse(string text)
    {
        var lines = Normalize(text);
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var preamble = new List<string>();
        string? currentSection = null;
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (TryParseSectionHeader(line, out var sectionName, out var inlineValue))
            {
                currentSection = sectionName;
                if (!sections.ContainsKey(sectionName))
                {
                    sections[sectionName] = [];
                }

                if (!string.IsNullOrWhiteSpace(inlineValue))
                {
                    sections[sectionName].Add(inlineValue);
                }

                continue;
            }

            if (currentSection is null)
            {
                preamble.Add(line);
            }
            else
            {
                sections[currentSection].Add(line);
            }
        }
        var (title, version) = ParseTitleAndVersion(preamble);
        sections.TryGetValue("description", out var descriptionLines);
        sections.TryGetValue("usage", out var usageLines);
        sections.TryGetValue("arguments", out var argumentLines);
        sections.TryGetValue("options", out var optionLines);
        sections.TryGetValue("commands", out var commandLines);
        var parsedUsageLines = TrimNonEmpty(usageLines ?? ToolHelpPreambleInference.InferUsageLines(preamble));
        var parsedOptions = ParseItems(optionLines ?? ToolHelpLegacyOptionTable.InferOptionLines(preamble, title, parsedUsageLines), ItemKind.Option);

        var commands = ParseItems(commandLines ?? [], ItemKind.Command);
        if (commands.Count == 0)
        {
            commands = InferCommands(preamble, sections, parsedUsageLines, parsedOptions);
        }

        var applicationDescription = JoinLines(preamble.Skip(string.IsNullOrWhiteSpace(title) ? 0 : 1));
        var commandDescription = JoinLines(descriptionLines ?? []);
        return new ToolHelpDocument(
            Title: title,
            Version: version,
            ApplicationDescription: applicationDescription,
            CommandDescription: commandDescription,
            UsageLines: parsedUsageLines,
            Arguments: ParseItems(argumentLines ?? [], ItemKind.Argument),
            Options: parsedOptions,
            Commands: commands);
    }

    private static string[] Normalize(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static bool TryParseSectionHeader(string line, out string sectionName, out string? inlineValue)
    {
        sectionName = string.Empty;
        inlineValue = null;

        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (SectionAliases.TryGetValue(trimmed, out var matchedSectionName))
        {
            sectionName = matchedSectionName;
            return true;
        }

        var match = SectionHeaderRegex().Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        var alias = match.Groups["header"].Value.Trim();
        if (!SectionAliases.TryGetValue(alias, out matchedSectionName))
        {
            return false;
        }

        sectionName = matchedSectionName;
        inlineValue = string.IsNullOrWhiteSpace(match.Groups["value"].Value)
            ? null
            : match.Groups["value"].Value.Trim();
        return true;
    }

    private static IReadOnlyList<ToolHelpItem> ParseItems(IReadOnlyList<string> lines, ItemKind kind)
    {
        var items = new List<ToolHelpItem>();
        string? key = null;
        string? description = null;
        var isRequired = false;

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            if (TryParseItemStart(rawLine, kind, out var parsedKey, out var parsedRequired, out var parsedDescription))
            {
                FlushItem(items, kind, key, isRequired, description);
                key = parsedKey;
                isRequired = parsedRequired;
                description = parsedDescription;
                continue;
            }

            if (key is not null)
            {
                description = string.IsNullOrWhiteSpace(description)
                    ? rawLine.Trim()
                    : $"{description}\n{rawLine.Trim()}";
            }
        }

        FlushItem(items, kind, key, isRequired, description);
        return items;
    }

    private static bool TryParseItemStart(string rawLine, ItemKind kind, out string key, out bool isRequired, out string? description)
    {
        key = string.Empty;
        description = null;
        isRequired = false;

        var trimmedStart = rawLine.TrimStart();
        var match = ItemRegex().Match(trimmedStart);
        if (!match.Success)
        {
            return false;
        }

        key = match.Groups["key"].Value.Trim();
        description = match.Groups["description"].Success ? match.Groups["description"].Value.Trim() : null;
        isRequired = string.Equals(match.Groups["prefix"].Value, "* ", StringComparison.Ordinal);

        if (kind == ItemKind.Option && !key.StartsWith("-", StringComparison.Ordinal) && !key.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (kind == ItemKind.Command)
        {
            if (!char.IsWhiteSpace(rawLine, 0) && string.IsNullOrWhiteSpace(description))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(description)
                && key.Contains(' ', StringComparison.Ordinal)
                && !ToolHelpPreambleInference.LooksLikeCommandSignature(key))
            {
                return false;
            }

            key = NormalizeCommandKey(key);
            if (string.IsNullOrWhiteSpace(key))
            {
                return false;
            }
        }

        if (kind == ItemKind.Argument)
        {
            key = NormalizeArgumentKey(key);
        }

        return true;
    }

    private static IReadOnlyList<ToolHelpItem> InferCommands(
        IReadOnlyList<string> preamble,
        IReadOnlyDictionary<string, List<string>> sections,
        IReadOnlyList<string> usageLines,
        IReadOnlyList<ToolHelpItem> options)
    {
        if (sections.ContainsKey("usage")
            || sections.ContainsKey("options")
            || sections.ContainsKey("arguments")
            || usageLines.Count > 0
            || options.Count > 0)
        {
            return [];
        }

        return ParseItems(preamble.Skip(1).ToArray(), ItemKind.Command)
            .Where(item => !string.IsNullOrWhiteSpace(item.Description))
            .ToArray();
    }

    private static void FlushItem(ICollection<ToolHelpItem> items, ItemKind kind, string? key, bool isRequired, string? description)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        items.Add(new ToolHelpItem(key, isRequired, string.IsNullOrWhiteSpace(description) ? null : description.Trim()));
    }

    private static (string? Title, string? Version) ParseTitleAndVersion(IReadOnlyList<string> preamble)
    {
        var firstLine = preamble.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim();
        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return (null, null);
        }

        var match = TitleLineRegex().Match(firstLine);
        return match.Success
            ? (match.Groups["title"].Value.Trim(), match.Groups["version"].Value.Trim())
            : (firstLine, null);
    }

    private static IReadOnlyList<string> TrimNonEmpty(IEnumerable<string> lines)
        => lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    private static string? JoinLines(IEnumerable<string> lines)
    {
        var joined = string.Join("\n", lines.Select(line => line.Trim()).Where(line => line.Length > 0));
        return joined.Length == 0 ? null : joined;
    }

    private static string NormalizeCommandKey(string key)
    {
        var segments = key.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalized = segments
            .TakeWhile(segment => !segment.StartsWith("<", StringComparison.Ordinal)
                && !segment.StartsWith("[", StringComparison.Ordinal)
                && !segment.StartsWith("-", StringComparison.Ordinal)
                && !segment.StartsWith("/", StringComparison.Ordinal))
            .ToArray();
        return string.Join(' ', normalized);
    }

    private static string NormalizeArgumentKey(string key)
        => key.Trim().TrimStart('[', '<').TrimEnd(']', '>');

    [GeneratedRegex(@"^(?<header>[\p{L}\p{M}\s]+):\s*(?<value>\S.*)?$", RegexOptions.Compiled)]
    private static partial Regex SectionHeaderRegex();

    [GeneratedRegex(@"^(?<prefix>\* )?(?<key>\S.*?)(?:\s{2,}(?<description>\S.*))?$", RegexOptions.Compiled)]
    private static partial Regex ItemRegex();

    [GeneratedRegex(@"^(?<title>.+?)\s+(?<version>v?\d[\w\.\-\+]*)$", RegexOptions.Compiled)]
    private static partial Regex TitleLineRegex();

    private enum ItemKind
    {
        Argument,
        Command,
        Option,
    }
}
