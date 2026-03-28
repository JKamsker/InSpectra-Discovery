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
        ["COMMAND LIST"] = "commands",
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
        var firstMeaningfulLine = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line))?.Trim();
        if (LooksLikeRejectedHelpInvocation(firstMeaningfulLine))
        {
            return new ToolHelpDocument(null, null, null, null, [], [], [], []);
        }

        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var preamble = new List<string>();
        string? currentSection = null;
        string? commandHeader = null;
        var sawInventoryHeader = false;
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (ShouldRejectHelpCapture(preamble, sections, commandHeader, line))
            {
                return new ToolHelpDocument(null, null, null, null, [], [], [], []);
            }

            sawInventoryHeader |= LooksLikeInventoryHeaderLine(line.Trim());
            if (ToolHelpSectionHeaderSupport.TryParseIgnoredSectionHeader(line, IgnoredSectionHeaders))
            {
                currentSection = IgnoredSectionName;
                continue;
            }

            if (ToolHelpSectionHeaderSupport.TryParseSectionHeader(line, SectionAliases, out var sectionName, out var inlineValue, out var matchedHeader))
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
        var (title, version, descriptionStartIndex) = ToolHelpTitleInference.ParseTitleAndVersion(preamble);
        if (!string.IsNullOrWhiteSpace(commandHeader))
        {
            title = commandHeader;
        }

        sections.TryGetValue("description", out var descriptionLines);
        sections.TryGetValue("usage", out var usageLines);
        sections.TryGetValue("arguments", out var argumentLines);
        sections.TryGetValue("options", out var optionLines);
        sections.TryGetValue("commands", out var commandLines);
        var usageSectionParts = ToolHelpUsageSectionSplitter.Split(usageLines ?? []);
        var rawArgumentLines = new List<string>(argumentLines ?? []);
        rawArgumentLines.AddRange(usageSectionParts.ArgumentLines);
        rawArgumentLines.AddRange(ToolHelpPreambleArgumentInference.InferArgumentLines(preamble, title));
        SplitArgumentSectionLines(rawArgumentLines, out var parsedArgumentLines, out var optionStyleArgumentLines);
        var parsedUsageLines = TrimNonEmpty(
            usageSectionParts.UsageLines.Count > 0
                ? usageSectionParts.UsageLines
                : ToolHelpPreambleInference.InferUsageLines(preamble));
        var rawOptionLines = new List<string>(optionLines ?? ToolHelpLegacyOptionTable.InferOptionLines(preamble, title, parsedUsageLines));
        rawOptionLines.AddRange(usageSectionParts.OptionLines);
        rawOptionLines.AddRange(optionStyleArgumentLines);
        var parsedOptions = ParseItems(
            ToolHelpLegacyOptionTable.NormalizeOptionLines(rawOptionLines),
            ItemKind.Option);

        var commands = ParseItems(commandLines ?? [], ItemKind.Command);
        if (commands.Count == 0)
        {
            commands = InferCommands(preamble, sections, parsedUsageLines, parsedOptions, sawInventoryHeader);
        }

        var applicationDescription = JoinLines(preamble.Skip(descriptionStartIndex));
        var commandDescription = JoinLines(descriptionLines ?? []);
        return new ToolHelpDocument(
            Title: title,
            Version: version,
            ApplicationDescription: applicationDescription,
            CommandDescription: commandDescription,
            UsageLines: parsedUsageLines,
            Arguments: ParseItems(parsedArgumentLines, ItemKind.Argument),
            Options: parsedOptions,
            Commands: commands);
    }

    private static string[] Normalize(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

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

            if (kind == ItemKind.Option
                && TryParsePositionalArgumentRow(rawLine.TrimStart(), out _, out _, out _))
            {
                continue;
            }

            var currentIndentation = GetIndentation(rawLine);
            var canStartNewItem = TryParseItemStart(rawLine, kind, out var parsedKey, out var parsedRequired, out var parsedDescription)
                && !(key is not null && currentIndentation > indentation);
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

    private static void SplitArgumentSectionLines(
        IReadOnlyList<string> lines,
        out IReadOnlyList<string> argumentLines,
        out IReadOnlyList<string> optionLines)
    {
        var arguments = new List<string>();
        var options = new List<string>();
        List<string>? target = arguments;

        foreach (var rawLine in lines)
        {
            if (TryParseItemStart(rawLine, ItemKind.Option, out _, out _, out _))
            {
                target = options;
            }
            else if (TryParseItemStart(rawLine, ItemKind.Argument, out _, out _, out _))
            {
                target = arguments;
            }

            target?.Add(rawLine);
        }

        argumentLines = arguments;
        optionLines = options;
    }

    private static bool TryParseItemStart(string rawLine, ItemKind kind, out string key, out bool isRequired, out string? description)
    {
        key = string.Empty;
        description = null;
        isRequired = false;

        if (kind == ItemKind.Command)
        {
            rawLine = NormalizeCommandItemLine(rawLine);
        }

        var trimmedStart = rawLine.TrimStart();
        if (kind == ItemKind.Command && LooksLikeMarkdownTableLine(trimmedStart))
        {
            return false;
        }

        if (kind == ItemKind.Argument && TryParsePositionalArgumentRow(trimmedStart, out key, out isRequired, out description))
        {
            return true;
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

        if (kind == ItemKind.Option)
        {
            key = NormalizeOptionSignatureKey(key);
        }

        if (kind == ItemKind.Option && TryExtractLeadingAliasFromDescription(description, out var alias, out var normalizedDescription))
        {
            key = $"{key} | {alias}";
            description = normalizedDescription;
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

    private static bool TryParsePositionalArgumentRow(string rawLine, out string key, out bool isRequired, out string? description)
    {
        key = string.Empty;
        description = null;
        isRequired = false;

        var match = PositionalArgumentRowRegex().Match(rawLine);
        if (!match.Success)
        {
            return false;
        }

        key = NormalizeArgumentKey(match.Groups["key"].Value);
        description = match.Groups["description"].Success
            ? match.Groups["description"].Value.Trim()
            : null;
        if (!string.IsNullOrWhiteSpace(description)
            && description.StartsWith("Required.", StringComparison.OrdinalIgnoreCase))
        {
            isRequired = true;
            description = description["Required.".Length..].TrimStart();
        }

        return LooksLikeArgumentKey(key);
    }

    private static IReadOnlyList<ToolHelpItem> InferCommands(
        IReadOnlyList<string> preamble,
        IReadOnlyDictionary<string, List<string>> sections,
        IReadOnlyList<string> usageLines,
        IReadOnlyList<ToolHelpItem> options,
        bool sawInventoryHeader)
    {
        if (sections.ContainsKey("usage")
            || sections.ContainsKey("options")
            || sections.ContainsKey("arguments")
            || usageLines.Count > 0
            || options.Count > 0
            || sawInventoryHeader)
        {
            return [];
        }

        var parsedCommands = ParseItems(preamble.Skip(1).ToArray(), ItemKind.Command);
        var describedCommands = parsedCommands
            .Where(item => !string.IsNullOrWhiteSpace(item.Description))
            .ToArray();
        if (describedCommands.Any(item => !IsBuiltinAuxiliaryCommand(item.Key)))
        {
            return describedCommands;
        }

        var blankInventoryCommands = parsedCommands
            .Where(item => string.IsNullOrWhiteSpace(item.Description))
            .Where(item => !IsBuiltinAuxiliaryCommand(item.Key))
            .ToArray();
        return blankInventoryCommands;
    }

    private static void FlushItem(ICollection<ToolHelpItem> items, ItemKind kind, string? key, bool isRequired, string? description)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        items.Add(new ToolHelpItem(key, isRequired, string.IsNullOrWhiteSpace(description) ? null : description.Trim()));
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
        var rawSegments = normalizedKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var segments = new List<string>();
        for (var index = 0; index < rawSegments.Length; index++)
        {
            var aliases = new List<string> { rawSegments[index].TrimEnd(',', ':') };
            while (rawSegments[index].EndsWith(",", StringComparison.Ordinal) && index + 1 < rawSegments.Length)
            {
                index++;
                aliases.Add(rawSegments[index].TrimEnd(',', ':'));
            }

            segments.Add(aliases
                .Where(alias => alias.Length > 0)
                .OrderByDescending(alias => alias.Length)
                .FirstOrDefault() ?? string.Empty);
        }

        var normalized = segments
            .TakeWhile(segment => !segment.StartsWith("<", StringComparison.Ordinal)
                && !segment.StartsWith("[", StringComparison.Ordinal)
                && !segment.StartsWith("-", StringComparison.Ordinal)
                && !segment.StartsWith("/", StringComparison.Ordinal))
            .Where(segment => segment.Length > 0)
            .ToArray();
        return normalized.Length == 0 || normalized.Any(segment => !LooksLikeCommandSegment(segment))
            ? string.Empty
            : string.Join(' ', normalized);
    }

    private static string NormalizeArgumentKey(string key)
        => key.Trim().TrimStart('[', '<').TrimEnd(']', '>');

    private static string NormalizeOptionSignatureKey(string key)
    {
        var matches = OptionTokenRegex().Matches(key);
        if (matches.Count == 0)
        {
            return key.Trim();
        }

        var trailing = key[(matches[^1].Index + matches[^1].Length)..].Trim();
        if (string.IsNullOrWhiteSpace(trailing))
        {
            return key.Trim();
        }

        if (trailing.StartsWith("=", StringComparison.Ordinal) || trailing.StartsWith(":", StringComparison.Ordinal))
        {
            return key.Trim();
        }

        if (trailing.StartsWith("<", StringComparison.Ordinal) || trailing.StartsWith("[", StringComparison.Ordinal))
        {
            return $"{key[..(matches[^1].Index + matches[^1].Length)].Trim()} {trailing}";
        }

        if (!IsBareOptionPlaceholder(trailing))
        {
            return key.Trim();
        }

        return $"{key[..(matches[^1].Index + matches[^1].Length)].Trim()} <{trailing.ToUpperInvariant()}>";
    }

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
                || LooksLikeSetupPreambleLine(trimmed)
                || LooksLikeInventoryHeaderLine(trimmed)
                || trimmed.StartsWith("Visit http://", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("Visit https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("NSwag bin directory:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("CLI Version:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldRejectHelpCapture(
        IReadOnlyList<string> preamble,
        IReadOnlyDictionary<string, List<string>> sections,
        string? commandHeader,
        string line)
    {
        if (!LooksLikeRejectedHelpInvocation(line.Trim()))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(commandHeader) || HasContentSections(sections))
        {
            return false;
        }

        var meaningfulPreambleLineCount = preamble
            .Select(entry => entry.Trim())
            .Count(entry => !string.IsNullOrWhiteSpace(entry) && !ShouldIgnorePreambleLine(entry));
        return meaningfulPreambleLineCount <= 1;
    }

    private static bool IsNoiseItemKey(ItemKind kind, string key)
    {
        var trimmed = key.Trim();
        return IsFrameworkNoiseLine(trimmed)
            || (kind == ItemKind.Argument && IsArgumentNoiseLine(trimmed));
    }

    private static bool IsNoiseContinuationLine(ItemKind kind, string rawLine)
    {
        var trimmed = rawLine.Trim();
        return IsFrameworkNoiseLine(trimmed)
            || (kind == ItemKind.Argument && IsArgumentNoiseLine(trimmed))
            || (kind == ItemKind.Command && LooksLikeSubcommandHelpHint(trimmed));
    }

    private static bool IsArgumentNoiseLine(string line)
        => line.StartsWith("Press <enter>", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Duration:", StringComparison.OrdinalIgnoreCase);

    private static bool IsFrameworkNoiseLine(string line)
        => string.Equals(line, "Error parsing", StringComparison.OrdinalIgnoreCase)
            || CommandLineErrorTokenRegex().IsMatch(line)
            || LooksLikeRejectedHelpInvocation(line);

    private static bool IsStructuredLogLine(string line)
        => StructuredLogPrefixRegex().IsMatch(line);

    private static bool LooksLikeSetupPreambleLine(string line)
        => line.StartsWith("No parameters file found", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Creating a template parameters file", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Parameters file created:", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Please edit this file with your", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeRejectedHelpInvocation(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        return RejectedHelpInvocationRegex().IsMatch(line.Trim());
    }

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

    private static bool TryExtractLeadingAliasFromDescription(string? description, out string alias, out string? normalizedDescription)
    {
        alias = string.Empty;
        normalizedDescription = description;
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var trimmed = description.Trim().TrimStart('|').TrimStart();
        if (!TryConsumeLeadingOptionAliasGroup(trimmed, out alias, out var remainder))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(remainder))
        {
            normalizedDescription = null;
            return true;
        }

        if (!char.IsWhiteSpace(remainder[0]))
        {
            return false;
        }

        var trimmedRemainder = remainder.TrimStart();
        var separatorIndex = trimmedRemainder.IndexOf(' ');
        var candidatePlaceholder = separatorIndex >= 0
            ? trimmedRemainder[..separatorIndex]
            : trimmedRemainder;
        var candidateDescription = separatorIndex >= 0
            ? trimmedRemainder[(separatorIndex + 1)..].TrimStart()
            : null;
        if (LooksLikeSplitColumnPlaceholder(candidatePlaceholder)
            || candidatePlaceholder.StartsWith("<", StringComparison.Ordinal)
            || candidatePlaceholder.StartsWith("[", StringComparison.Ordinal))
        {
            alias = NormalizeOptionSignatureKey($"{alias} {candidatePlaceholder}");
            normalizedDescription = string.IsNullOrWhiteSpace(candidateDescription) ? null : candidateDescription;
            return true;
        }

        normalizedDescription = trimmedRemainder;
        return true;
    }

    private static bool TryConsumeLeadingOptionAliasGroup(string text, out string aliasGroup, out string remainder)
    {
        aliasGroup = string.Empty;
        remainder = text;

        var match = LeadingOptionAliasGroupRegex().Match(text);
        if (!match.Success || match.Index != 0)
        {
            return false;
        }

        aliasGroup = string.Join(
            " | ",
            match.Groups["group"].Value
                .Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(segment => !string.IsNullOrWhiteSpace(segment)));
        remainder = text[match.Length..];
        return !string.IsNullOrWhiteSpace(aliasGroup);
    }

    private static bool LooksLikeCommandDescription(string description)
    {
        var trimmed = description.TrimStart();
        while (trimmed.StartsWith("(", StringComparison.Ordinal))
        {
            var closingIndex = trimmed.IndexOf(')');
            if (closingIndex < 0)
            {
                break;
            }

            trimmed = trimmed[(closingIndex + 1)..].TrimStart();
        }

        return trimmed.Length > 0
            && char.IsLetter(trimmed[0]);
    }

    private static bool IsBuiltinAuxiliaryCommand(string key)
        => string.Equals(key, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, "version", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeCommandItemLine(string rawLine)
    {
        var trimmedStart = rawLine.TrimStart();
        if (!trimmedStart.StartsWith(">", StringComparison.Ordinal))
        {
            return rawLine;
        }

        var commandLine = trimmedStart[1..].TrimStart();
        var separatorIndex = commandLine.IndexOf(':');
        if (separatorIndex < 0)
        {
            return commandLine;
        }

        var commandKey = commandLine[..separatorIndex].Trim();
        var commandDescription = commandLine[(separatorIndex + 1)..].Trim();
        return string.IsNullOrWhiteSpace(commandDescription)
            ? commandKey
            : $"{commandKey}  {commandDescription}";
    }

    private static bool LooksLikeSubcommandHelpHint(string line)
        => line.StartsWith("Use '", StringComparison.OrdinalIgnoreCase)
            && line.Contains("--help", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeInventoryHeaderLine(string line)
        => DotnetToolListHeaderRegex().IsMatch(line)
            || TemplateInstallHeaderRegex().IsMatch(line);

    private static bool LooksLikeMarkdownTableLine(string line)
        => line.StartsWith("|", StringComparison.Ordinal)
            && line.EndsWith("|", StringComparison.Ordinal)
            && line.Count(ch => ch == '|') >= 2;

    private static bool IsBareOptionPlaceholder(string value)
        => !string.IsNullOrWhiteSpace(value)
            && !value.Contains(' ', StringComparison.Ordinal)
            && !value.StartsWith("<", StringComparison.Ordinal)
            && !value.StartsWith("[", StringComparison.Ordinal)
            && !value.StartsWith("-", StringComparison.Ordinal)
            && !value.StartsWith("/", StringComparison.Ordinal);

    private static bool LooksLikeSplitColumnPlaceholder(string value)
        => IsBareOptionPlaceholder(value)
            && value.Any(char.IsLetter)
            && value.Where(char.IsLetter).All(char.IsUpper);

    [GeneratedRegex(@"^(?<prefix>\* )?(?<key>\S.*?)(?:\s{2,}(?<description>\S.*))?$", RegexOptions.Compiled)]
    private static partial Regex ItemRegex();

    [GeneratedRegex(@"^(?<key>[A-Za-z][A-Za-z0-9_.-]*)\s+(?:\(pos\.\s*\d+\)|pos\.\s*\d+)(?:\s{2,}(?<description>\S.*))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PositionalArgumentRowRegex();

    [GeneratedRegex(@"(?<option>(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*))", RegexOptions.Compiled)]
    private static partial Regex OptionTokenRegex();

    [GeneratedRegex(@"^(?<group>(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*)(?:\s*(?:\||,)\s*(?:--[A-Za-z0-9][A-Za-z0-9_\.\?\-]*|-[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9][A-Za-z0-9_\.\?\-]*))*)", RegexOptions.Compiled)]
    private static partial Regex LeadingOptionAliasGroupRegex();

    [GeneratedRegex(@"^CommandLine\.[A-Za-z]+Error$", RegexOptions.Compiled)]
    private static partial Regex CommandLineErrorTokenRegex();

    [GeneratedRegex(@"^(?:trace|debug|info|warn|warning|fail|error):\s|^\[\d{2}:\d{2}:\d{2}\s+[A-Z]{3}\]\s", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex StructuredLogPrefixRegex();

    [GeneratedRegex(@"^(?:--help|-h|/\?)\s+is an unknown (?:parameter|option|argument)\b|^Invalid usage\b|^Unknown argument or flag for value --help\b|^(?:unknown|unrecognized)\s+(?:option|parameter|argument)\b.*(?:--help|-h|/\?)\b|^(?:unknown|unrecognized)\s+command\b.*\bhelp\b|^usage error\b.*(?:--help|-h|/\?)\b|^error\(\d+\):\s+unknown command-line option\s+(?:--help|-h|/\?)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex RejectedHelpInvocationRegex();

    [GeneratedRegex(@"^Package Id\s{2,}Version\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex DotnetToolListHeaderRegex();

    [GeneratedRegex(@"^Template Name\s{2,}Short Name\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TemplateInstallHeaderRegex();

    private enum ItemKind
    {
        Argument,
        Command,
        Option,
    }
}
