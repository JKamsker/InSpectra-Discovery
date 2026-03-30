namespace InSpectra.Discovery.Tool.Help;

internal sealed class TextParser
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

    public Document Parse(string text)
    {
        var lines = Normalize(text);
        var firstMeaningfulLines = lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(2)
            .ToArray();
        if (TextNoiseClassifier.LooksLikeRejectedHelpInvocation(
            firstMeaningfulLines.FirstOrDefault(),
            firstMeaningfulLines.Skip(1).FirstOrDefault()))
        {
            return new Document(null, null, null, null, [], [], [], []);
        }

        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var preamble = new List<string>();
        string? currentSection = null;
        string? commandHeader = null;
        var sawInventoryHeader = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (TextNoiseClassifier.ShouldRejectHelpCapture(preamble, sections, commandHeader, line))
            {
                return new Document(null, null, null, null, [], [], [], []);
            }

            sawInventoryHeader |= TextNoiseClassifier.LooksLikeInventoryHeaderLine(line.Trim());
            if (SectionHeaderSupport.TryParseIgnoredSectionHeader(line, IgnoredSectionHeaders))
            {
                currentSection = IgnoredSectionName;
                continue;
            }

            if (SectionHeaderSupport.TryParseSectionHeader(line, SectionAliases, out var sectionName, out var inlineValue, out var matchedHeader))
            {
                if (string.Equals(matchedHeader, "COMMAND", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(inlineValue))
                {
                    if (!TextNoiseClassifier.HasContentSections(sections))
                    {
                        commandHeader = SignatureNormalizer.NormalizeCommandKey(inlineValue);
                        currentSection = "description";
                        sections.TryAdd(currentSection, []);
                    }
                    else
                    {
                        currentSection = IgnoredSectionName;
                    }

                    continue;
                }

                if (string.Equals(matchedHeader, "COMMAND", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(currentSection, "examples", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(inlineValue))
                {
                    continue;
                }

                currentSection = sectionName;
                sections.TryAdd(sectionName, []);
                if (!string.IsNullOrWhiteSpace(inlineValue))
                {
                    sections[sectionName].Add(inlineValue);
                }

                continue;
            }

            if (currentSection is not null
                && !string.Equals(currentSection, IgnoredSectionName, StringComparison.Ordinal)
                && SectionHeaderSupport.LooksLikeUnrecognizedMarkdownSectionHeader(line, SectionAliases, IgnoredSectionHeaders))
            {
                currentSection = IgnoredSectionName;
                continue;
            }

            if (currentSection is null)
            {
                if (!TextNoiseClassifier.ShouldIgnorePreambleLine(line))
                {
                    preamble.Add(line);
                }
            }
            else if (!string.Equals(currentSection, IgnoredSectionName, StringComparison.Ordinal))
            {
                sections[currentSection].Add(line);
            }
        }

        var (title, version, descriptionStartIndex) = TitleInference.ParseTitleAndVersion(preamble);
        if (!string.IsNullOrWhiteSpace(commandHeader))
        {
            title = commandHeader;
        }

        sections.TryGetValue("description", out var descriptionLines);
        sections.TryGetValue("usage", out var usageLines);
        sections.TryGetValue("arguments", out var argumentLines);
        sections.TryGetValue("options", out var optionLines);
        sections.TryGetValue("commands", out var commandLines);

        var usageSectionParts = UsageSectionSplitter.Split(usageLines ?? []);
        var trailingStructuredBlock = TrailingStructuredBlockInference.Infer(sections);
        var rawArgumentLines = ParserInputAssemblySupport.BuildRawArgumentLines(
            preamble,
            title,
            sections,
            usageSectionParts,
            trailingStructuredBlock);
        ItemParser.SplitArgumentSectionLines(rawArgumentLines, out var parsedArgumentLines, out var optionStyleArgumentLines);

        var parsedUsageLines = TrimNonEmpty(
            usageSectionParts.UsageLines.Count > 0
                ? usageSectionParts.UsageLines
                : PreambleInference.InferUsageLines(preamble));
        var rawOptionLines = ParserInputAssemblySupport.BuildRawOptionLines(
            lines,
            preamble,
            title,
            sections,
            parsedUsageLines,
            usageSectionParts,
            trailingStructuredBlock,
            optionStyleArgumentLines,
            rawArgumentLines,
            out rawArgumentLines);
        var parsedOptions = ItemParser.ParseItems(
            LegacyOptionTable.NormalizeOptionLines(rawOptionLines),
            ItemKind.Option);

        var commands = ItemParser.ParseItems(commandLines ?? [], ItemKind.Command);
        if (commands.Count == 0)
        {
            commands = ItemParser.InferCommands(preamble, sawInventoryHeader);
        }

        var applicationDescription = ApplicationDescriptionInference.Infer(preamble, descriptionStartIndex);
        var commandDescription = JoinLines(descriptionLines ?? []);
        return new Document(
            Title: title,
            Version: version,
            ApplicationDescription: applicationDescription,
            CommandDescription: commandDescription,
            UsageLines: parsedUsageLines,
            Arguments: ItemParser.ParseItems(parsedArgumentLines, ItemKind.Argument),
            Options: parsedOptions,
            Commands: commands);
    }

    private static string[] Normalize(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

    private static IReadOnlyList<string> TrimNonEmpty(IEnumerable<string> lines)
        => lines.Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();

    private static string? JoinLines(IEnumerable<string> lines)
    {
        var joined = string.Join("\n", lines.Select(line => line.Trim()).Where(line => line.Length > 0));
        return joined.Length == 0 ? null : joined;
    }
}

