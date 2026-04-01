namespace InSpectra.Discovery.Tool.Help.Parsing;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Signatures;

using System.Text.RegularExpressions;

internal static partial class CommandScopedSectionSupport
{
    public static CommandScopedSectionParseResult Extract(
        IReadOnlyList<string> lines,
        IReadOnlyList<Item> knownCommands)
    {
        var commandLookup = BuildCommandLookup(knownCommands);
        if (commandLookup.Count == 0)
        {
            return CommandScopedSectionParseResult.Empty;
        }

        var consumedLineIndexes = new HashSet<int>();
        var commandSections = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase);
        string? currentCommandKey = null;
        string? currentSectionName = null;

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (TryParseCommandScopedSectionHeader(line, commandLookup, out var commandKey, out var sectionName, out var inlineValue))
            {
                currentCommandKey = commandKey;
                currentSectionName = sectionName;
                consumedLineIndexes.Add(index);
                AppendInlineValue(commandSections, commandKey, sectionName, inlineValue);
                continue;
            }

            if (currentCommandKey is null || currentSectionName is null)
            {
                continue;
            }

            if (SectionHeaderSupport.TryParseSectionHeader(
                    line,
                    HelpSectionCatalog.Aliases,
                    out _,
                    out _,
                    out _)
                || SectionHeaderSupport.TryParseIgnoredSectionHeader(line, HelpSectionCatalog.IgnoredHeaders))
            {
                currentCommandKey = null;
                currentSectionName = null;
                continue;
            }

            consumedLineIndexes.Add(index);
            EnsureSection(commandSections, currentCommandKey, currentSectionName).Add(line.TrimEnd());
        }

        return new CommandScopedSectionParseResult(
            BuildDocuments(commandSections),
            consumedLineIndexes);
    }

    private static IReadOnlyDictionary<string, Document> BuildDocuments(
        IReadOnlyDictionary<string, Dictionary<string, List<string>>> commandSections)
    {
        var documents = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in commandSections)
        {
            var sections = pair.Value;
            sections.TryGetValue("description", out var descriptionLines);
            sections.TryGetValue("usage", out var usageLines);
            sections.TryGetValue("arguments", out var argumentLines);
            sections.TryGetValue("options", out var optionLines);
            sections.TryGetValue("commands", out var commandLines);

            ItemParser.SplitArgumentSectionLines(argumentLines ?? [], out var parsedArgumentLines, out var optionStyleArgumentLines);
            var normalizedOptionLines = LegacyOptionTable.NormalizeOptionLines(optionLines ?? []);
            var parsedOptions = ItemParser.ParseItems(normalizedOptionLines.Concat(optionStyleArgumentLines).ToArray(), ItemKind.Option);
            var parsedCommands = ItemParser.ParseItems(commandLines ?? [], ItemKind.Command);

            documents[pair.Key] = new Document(
                Title: null,
                Version: null,
                ApplicationDescription: null,
                CommandDescription: JoinLines(descriptionLines ?? []),
                UsageLines: TrimNonEmpty(usageLines ?? []),
                Arguments: ItemParser.ParseItems(parsedArgumentLines, ItemKind.Argument),
                Options: parsedOptions,
                Commands: parsedCommands);
        }

        return documents;
    }

    private static Dictionary<string, string> BuildCommandLookup(IReadOnlyList<Item> knownCommands)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var command in knownCommands)
        {
            var normalizedKey = SignatureNormalizer.NormalizeCommandKey(command.Key);
            if (string.IsNullOrWhiteSpace(normalizedKey))
            {
                continue;
            }

            lookup.TryAdd(normalizedKey, normalizedKey);
            var leafSegment = normalizedKey
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault();
            if (!string.IsNullOrWhiteSpace(leafSegment))
            {
                lookup.TryAdd(leafSegment, normalizedKey);
            }
        }

        return lookup;
    }

    private static bool TryParseCommandScopedSectionHeader(
        string line,
        IReadOnlyDictionary<string, string> commandLookup,
        out string commandKey,
        out string sectionName,
        out string? inlineValue)
    {
        commandKey = string.Empty;
        sectionName = string.Empty;
        inlineValue = null;

        var match = CommandScopedSectionHeaderRegex().Match(line.Trim());
        if (!match.Success)
        {
            return false;
        }

        var normalizedCommand = SignatureNormalizer.NormalizeCommandKey(
            match.Groups["command"].Value.Trim().Trim('\'', '"', '`'));
        if (string.IsNullOrWhiteSpace(normalizedCommand)
            || !commandLookup.TryGetValue(normalizedCommand, out var resolvedCommandKey))
        {
            return false;
        }

        commandKey = resolvedCommandKey;

        if (!HelpSectionCatalog.TryResolveAlias(match.Groups["header"].Value.Trim(), out sectionName))
        {
            return false;
        }

        inlineValue = string.IsNullOrWhiteSpace(match.Groups["value"].Value)
            ? null
            : match.Groups["value"].Value.Trim();
        return true;
    }

    private static List<string> EnsureSection(
        IDictionary<string, Dictionary<string, List<string>>> commandSections,
        string commandKey,
        string sectionName)
    {
        if (!commandSections.TryGetValue(commandKey, out var sections))
        {
            sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            commandSections[commandKey] = sections;
        }

        if (!sections.TryGetValue(sectionName, out var lines))
        {
            lines = [];
            sections[sectionName] = lines;
        }

        return lines;
    }

    private static void AppendInlineValue(
        IDictionary<string, Dictionary<string, List<string>>> commandSections,
        string commandKey,
        string sectionName,
        string? inlineValue)
    {
        if (!string.IsNullOrWhiteSpace(inlineValue))
        {
            EnsureSection(commandSections, commandKey, sectionName).Add(inlineValue);
        }
    }

    private static IReadOnlyList<string> TrimNonEmpty(IEnumerable<string> lines)
        => lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    private static string? JoinLines(IEnumerable<string> lines)
    {
        var joined = string.Join("\n", lines.Select(line => line.Trim()).Where(line => line.Length > 0));
        return joined.Length == 0 ? null : joined;
    }

    [GeneratedRegex(
        @"^(?<command>(?:'[^']+'|""[^""]+""|`[^`]+`|[A-Za-z][A-Za-z0-9_.:+-]*(?:\s+[A-Za-z][A-Za-z0-9_.:+-]*)*))\s+(?<header>[\p{L}\p{M}\s]+):\s*(?<value>\S.*)?$",
        RegexOptions.Compiled)]
    private static partial Regex CommandScopedSectionHeaderRegex();
}

internal sealed record CommandScopedSectionParseResult(
    IReadOnlyDictionary<string, Document> Documents,
    IReadOnlySet<int> ConsumedLineIndexes)
{
    public static CommandScopedSectionParseResult Empty { get; } = new(
        new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase),
        new HashSet<int>());
}
