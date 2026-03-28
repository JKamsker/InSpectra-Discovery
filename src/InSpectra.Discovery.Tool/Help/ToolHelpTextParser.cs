using System.Text.RegularExpressions;

internal sealed partial class ToolHelpTextParser
{
    private const string IgnoredSectionName = "__ignored__";
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

    private static readonly HashSet<string> IgnoredSectionHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "RAW OUTPUT",
        "REDIRECTION WARNING",
    };

    public ToolHelpDocument Parse(string text)
    {
        var lines = Normalize(text);
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var preamble = new List<string>();
        string? currentSection = null;
        string? commandHeader = null;
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (TryParseIgnoredSectionHeader(line))
            {
                currentSection = IgnoredSectionName;
                continue;
            }

            if (TryParseSectionHeader(line, out var sectionName, out var inlineValue, out var matchedHeader))
            {
                if (string.Equals(matchedHeader, "COMMAND", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(inlineValue))
                {
                    if (!HasContentSections(sections))
                    {
                        commandHeader = NormalizeCommandKey(inlineValue);
                        currentSection = "description";
                        if (!sections.ContainsKey(currentSection))
                        {
                            sections[currentSection] = [];
                        }
                    }
                    else
                    {
                        currentSection = IgnoredSectionName;
                    }

                    continue;
                }

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
                if (!ShouldIgnorePreambleLine(line))
                {
                    preamble.Add(line);
                }
            }
            else if (string.Equals(currentSection, IgnoredSectionName, StringComparison.Ordinal))
            {
                continue;
            }
            else
            {
                sections[currentSection].Add(line);
            }
        }
        var (title, version) = ParseTitleAndVersion(preamble);
        if (!string.IsNullOrWhiteSpace(commandHeader))
        {
            title = commandHeader;
        }

        sections.TryGetValue("description", out var descriptionLines);
        sections.TryGetValue("usage", out var usageLines);
        sections.TryGetValue("arguments", out var argumentLines);
        sections.TryGetValue("options", out var optionLines);
        sections.TryGetValue("commands", out var commandLines);
        var parsedUsageLines = TrimNonEmpty(usageLines ?? ToolHelpPreambleInference.InferUsageLines(preamble));
        var parsedOptions = ParseItems(
            ToolHelpLegacyOptionTable.NormalizeOptionLines(
                optionLines ?? ToolHelpLegacyOptionTable.InferOptionLines(preamble, title, parsedUsageLines)),
            ItemKind.Option);

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

    private static bool TryParseSectionHeader(string line, out string sectionName, out string? inlineValue, out string matchedHeader)
    {
        sectionName = string.Empty;
        inlineValue = null;
        matchedHeader = string.Empty;

        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        if (SectionAliases.TryGetValue(trimmed, out var matchedSectionName))
        {
            sectionName = matchedSectionName;
            matchedHeader = trimmed;
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

        matchedHeader = alias;
        sectionName = matchedSectionName;
        inlineValue = string.IsNullOrWhiteSpace(match.Groups["value"].Value)
            ? null
            : match.Groups["value"].Value.Trim();
        return true;
    }

    private static bool TryParseIgnoredSectionHeader(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return false;
        }

        var match = SectionHeaderRegex().Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        return IgnoredSectionHeaders.Contains(match.Groups["header"].Value.Trim());
    }

    private static IReadOnlyList<ToolHelpItem> ParseItems(IReadOnlyList<string> lines, ItemKind kind)
    {
        var items = new List<ToolHelpItem>();
        string? key = null;
        string? description = null;
        var isRequired = false;
        var indentation = -1;

        foreach (var rawLine in lines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            if (IsNoiseContinuationLine(kind, rawLine))
            {
                continue;
            }

            var currentIndentation = GetIndentation(rawLine);
            var canStartNewItem = TryParseItemStart(rawLine, kind, out var parsedKey, out var parsedRequired, out var parsedDescription)
                && !(kind == ItemKind.Argument && key is not null && currentIndentation > indentation);
            if (canStartNewItem)
            {
                FlushItem(items, kind, key, isRequired, description);
                key = parsedKey;
                isRequired = parsedRequired;
                description = parsedDescription;
                indentation = currentIndentation;
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
        if (kind == ItemKind.Command && LooksLikeMarkdownTableLine(trimmedStart))
        {
            return false;
        }

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

        if (kind == ItemKind.Option && !LooksLikeOptionSignature(key))
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

            if (!string.IsNullOrWhiteSpace(description) && !LooksLikeCommandDescription(description))
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
            if (!LooksLikeArgumentKey(key))
            {
                return false;
            }
        }

        if (IsNoiseItemKey(kind, key))
        {
            return false;
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
        var normalizedKey = key.Trim();
        var aliasSeparator = normalizedKey.IndexOf(',');
        if (aliasSeparator >= 0)
        {
            normalizedKey = normalizedKey[..aliasSeparator];
        }

        var segments = normalizedKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalized = segments
            .TakeWhile(segment => !segment.StartsWith("<", StringComparison.Ordinal)
                && !segment.StartsWith("[", StringComparison.Ordinal)
                && !segment.StartsWith("-", StringComparison.Ordinal)
                && !segment.StartsWith("/", StringComparison.Ordinal))
            .Select(segment => segment.TrimEnd(':'))
            .Where(segment => segment.Length > 0)
            .ToArray();
        return normalized.Length == 0 || normalized.Any(segment => !LooksLikeCommandSegment(segment))
            ? string.Empty
            : string.Join(' ', normalized);
    }

    private static string NormalizeArgumentKey(string key)
        => key.Trim().TrimStart('[', '<').TrimEnd(']', '>');

    private static bool HasContentSections(IReadOnlyDictionary<string, List<string>> sections)
        => sections.Any(pair => pair.Value.Count > 0 || string.Equals(pair.Key, "commands", StringComparison.OrdinalIgnoreCase));

    private static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();

    private static bool ShouldIgnorePreambleLine(string line)
    {
        var trimmed = line.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? false
            : IsFrameworkNoiseLine(trimmed)
                || IsStructuredLogLine(trimmed)
                || trimmed.StartsWith("Visit http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Visit https://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("NSwag bin directory:", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("CLI Version:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoiseItemKey(ItemKind kind, string key)
    {
        var trimmed = key.Trim();
        return IsFrameworkNoiseLine(trimmed)
            || (kind == ItemKind.Argument && IsArgumentNoiseLine(trimmed));
    }

    private static bool IsNoiseContinuationLine(ItemKind kind, string rawLine)
        => kind == ItemKind.Argument && IsArgumentNoiseLine(rawLine.Trim());

    private static bool IsArgumentNoiseLine(string line)
        => line.StartsWith("Press <enter>", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Duration:", StringComparison.OrdinalIgnoreCase);

    private static bool IsFrameworkNoiseLine(string line)
        => string.Equals(line, "Error parsing", StringComparison.OrdinalIgnoreCase)
            || CommandLineErrorTokenRegex().IsMatch(line);

    private static bool IsStructuredLogLine(string line)
        => StructuredLogPrefixRegex().IsMatch(line);

    private static bool LooksLikeArgumentKey(string key)
        => key.Length > 0
            && !key.Contains(' ', StringComparison.Ordinal)
            && key.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.');

    private static bool LooksLikeCommandSegment(string segment)
        => segment.Length > 0
            && char.IsLetter(segment[0])
            && !CommandLineErrorTokenRegex().IsMatch(segment)
            && segment.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.' or ':' or '+');

    private static bool LooksLikeOptionSignature(string key)
    {
        var match = OptionTokenRegex().Match(key);
        return match.Success && match.Index == 0;
    }

    private static bool LooksLikeCommandDescription(string description)
    {
        var trimmed = description.TrimStart();
        return trimmed.Length > 0
            && char.IsLetter(trimmed[0]);
    }

    private static bool LooksLikeMarkdownTableLine(string line)
        => line.StartsWith("|", StringComparison.Ordinal)
            && line.EndsWith("|", StringComparison.Ordinal)
            && line.Count(ch => ch == '|') >= 2;

    [GeneratedRegex(@"^(?<header>[\p{L}\p{M}\s]+):\s*(?<value>\S.*)?$", RegexOptions.Compiled)]
    private static partial Regex SectionHeaderRegex();

    [GeneratedRegex(@"^(?<prefix>\* )?(?<key>\S.*?)(?:\s{2,}(?<description>\S.*))?$", RegexOptions.Compiled)]
    private static partial Regex ItemRegex();

    [GeneratedRegex(@"(?<option>(?:--[A-Za-z0-9][A-Za-z0-9\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9\?\-]*|/[A-Za-z0-9][A-Za-z0-9\?\-]*))", RegexOptions.Compiled)]
    private static partial Regex OptionTokenRegex();

    [GeneratedRegex(@"^(?<title>.+?)\s+(?<version>v?\d[\w\.\-\+]*)$", RegexOptions.Compiled)]
    private static partial Regex TitleLineRegex();

    [GeneratedRegex(@"^CommandLine\.[A-Za-z]+Error$", RegexOptions.Compiled)]
    private static partial Regex CommandLineErrorTokenRegex();

    [GeneratedRegex(@"^(?:trace|debug|info|warn|warning|fail|error):\s", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex StructuredLogPrefixRegex();

    private enum ItemKind
    {
        Argument,
        Command,
        Option,
    }
}
