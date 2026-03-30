namespace InSpectra.Discovery.Tool.Help;

internal sealed class ToolHelpTextParser
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
        var firstMeaningfulLines = lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(2)
            .ToArray();
        if (ToolHelpTextNoiseClassifier.LooksLikeRejectedHelpInvocation(
            firstMeaningfulLines.FirstOrDefault(),
            firstMeaningfulLines.Skip(1).FirstOrDefault()))
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
            if (ToolHelpTextNoiseClassifier.ShouldRejectHelpCapture(preamble, sections, commandHeader, line))
            {
                return new ToolHelpDocument(null, null, null, null, [], [], [], []);
            }

            sawInventoryHeader |= ToolHelpTextNoiseClassifier.LooksLikeInventoryHeaderLine(line.Trim());
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
                    if (!ToolHelpTextNoiseClassifier.HasContentSections(sections))
                    {
                        commandHeader = ToolHelpSignatureNormalizer.NormalizeCommandKey(inlineValue);
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
                && ToolHelpSectionHeaderSupport.LooksLikeUnrecognizedMarkdownSectionHeader(line, SectionAliases, IgnoredSectionHeaders))
            {
                currentSection = IgnoredSectionName;
                continue;
            }

            if (currentSection is null)
            {
                if (!ToolHelpTextNoiseClassifier.ShouldIgnorePreambleLine(line))
                {
                    preamble.Add(line);
                }
            }
            else if (!string.Equals(currentSection, IgnoredSectionName, StringComparison.Ordinal))
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
        var trailingStructuredBlock = ToolHelpTrailingStructuredBlockInference.Infer(sections);
        var rawArgumentLines = ToolHelpParserInputAssemblySupport.BuildRawArgumentLines(
            preamble,
            title,
            sections,
            usageSectionParts,
            trailingStructuredBlock);
        ToolHelpItemParser.SplitArgumentSectionLines(rawArgumentLines, out var parsedArgumentLines, out var optionStyleArgumentLines);

        var parsedUsageLines = TrimNonEmpty(
            usageSectionParts.UsageLines.Count > 0
                ? usageSectionParts.UsageLines
                : ToolHelpPreambleInference.InferUsageLines(preamble));
        var rawOptionLines = ToolHelpParserInputAssemblySupport.BuildRawOptionLines(
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
        var parsedOptions = ToolHelpItemParser.ParseItems(
            ToolHelpLegacyOptionTable.NormalizeOptionLines(rawOptionLines),
            ToolHelpItemKind.Option);

        var commands = ToolHelpItemParser.ParseItems(commandLines ?? [], ToolHelpItemKind.Command);
        if (commands.Count == 0)
        {
            commands = ToolHelpItemParser.InferCommands(preamble, sawInventoryHeader);
        }

        var applicationDescription = ToolHelpApplicationDescriptionInference.Infer(preamble, descriptionStartIndex);
        var commandDescription = JoinLines(descriptionLines ?? []);
        return new ToolHelpDocument(
            Title: title,
            Version: version,
            ApplicationDescription: applicationDescription,
            CommandDescription: commandDescription,
            UsageLines: parsedUsageLines,
            Arguments: ToolHelpItemParser.ParseItems(parsedArgumentLines, ToolHelpItemKind.Argument),
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

