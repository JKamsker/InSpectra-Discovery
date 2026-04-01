namespace InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Parsing;
using InSpectra.Discovery.Tool.Help.Signatures;

internal static class UsageArgumentExtractionSupport
{
    public static IReadOnlyList<Item> Extract(
        string commandName,
        string commandPath,
        IReadOnlyList<string> usageLines,
        bool hasChildCommands)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<Item>();
        var invocation = string.IsNullOrWhiteSpace(commandPath)
            ? commandName
            : $"{commandName} {commandPath}";
        string? previousNonEmptyLine = null;

        for (var index = 0; index < usageLines.Count; index++)
        {
            var line = usageLines[index];
            if (ShouldSkipDescendantUsageLine(invocation, line, hasChildCommands))
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (LooksLikeStructuredUsageGroupLabel(usageLines, index))
            {
                previousNonEmptyLine = line;
                continue;
            }

            var lineArguments = new List<Item>();
            var stopLine = false;
            foreach (var argument in BracketedUsageArgumentSupport.Extract(line, seen, hasChildCommands, out stopLine))
            {
                lineArguments.Add(argument);
                arguments.Add(argument);
            }

            if (stopLine)
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (lineArguments.Count > 0)
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (BareUsageArgumentSupport.LooksLikeWrappedUsageValueContinuation(previousNonEmptyLine, line))
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (!BareUsageArgumentSupport.LooksLikeExampleLabel(previousNonEmptyLine))
            {
                var bareArgument = BareUsageArgumentSupport.TryExtract(invocation, line, hasChildCommands);
                if (bareArgument is not null && seen.Add(bareArgument.Key))
                {
                    arguments.Add(bareArgument);
                }
            }

            previousNonEmptyLine = line;
        }

        return arguments;
    }

    private static bool LooksLikeStructuredUsageGroupLabel(IReadOnlyList<string> usageLines, int index)
    {
        var trimmed = usageLines[index].Trim();
        if (!LooksLikeStandaloneGroupLabel(trimmed))
        {
            return false;
        }

        for (var nextIndex = index + 1; nextIndex < usageLines.Count; nextIndex++)
        {
            var nextLine = usageLines[nextIndex];
            if (string.IsNullOrWhiteSpace(nextLine))
            {
                continue;
            }

            var trimmedNextLine = nextLine.TrimStart();
            return LegacyOptionRowSupport.LooksLikeLooseOptionRow(trimmedNextLine)
                || ItemStartParserSupport.TryParsePositionalArgumentRow(trimmedNextLine, out _, out _, out _);
        }

        return false;
    }

    private static bool ShouldSkipDescendantUsageLine(string invocation, string usageLine, bool hasChildCommands)
    {
        if (!hasChildCommands)
        {
            return false;
        }

        var invocationTokens = invocation.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (invocationTokens.Length == 0)
        {
            return false;
        }

        var usageTokens = usageLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var invocationStart = FindTokenSequence(usageTokens, invocationTokens);
        if (invocationStart < 0)
        {
            return false;
        }

        var nextTokenIndex = invocationStart + invocationTokens.Length;
        if (nextTokenIndex >= usageTokens.Length)
        {
            return false;
        }

        return LooksLikeLiteralCommandToken(usageTokens[nextTokenIndex]);
    }

    private static int FindTokenSequence(IReadOnlyList<string> tokens, IReadOnlyList<string> sequence)
    {
        if (sequence.Count == 0 || tokens.Count < sequence.Count)
        {
            return -1;
        }

        for (var start = 0; start <= tokens.Count - sequence.Count; start++)
        {
            var matched = true;
            for (var index = 0; index < sequence.Count; index++)
            {
                if (!string.Equals(tokens[start + index], sequence[index], StringComparison.OrdinalIgnoreCase))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return start;
            }
        }

        return -1;
    }

    private static bool LooksLikeLiteralCommandToken(string token)
    {
        var trimmed = token.Trim();
        if (trimmed.Length == 0
            || trimmed.StartsWith("<", StringComparison.Ordinal)
            || trimmed.StartsWith("[", StringComparison.Ordinal)
            || trimmed.StartsWith("(", StringComparison.Ordinal)
            || trimmed.StartsWith("-", StringComparison.Ordinal)
            || trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = SignatureNormalizer.NormalizeCommandKey(trimmed);
        return !string.IsNullOrWhiteSpace(normalized)
            && string.Equals(normalized, trimmed.TrimEnd(':', ','), StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeStandaloneGroupLabel(string line)
    {
        if (string.IsNullOrWhiteSpace(line)
            || line.Contains(' ', StringComparison.Ordinal)
            || line.Length < 2)
        {
            return false;
        }

        var letters = line.Where(char.IsLetter).ToArray();
        return letters.Length > 0
            && letters.All(char.IsUpper)
            && line.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_');
    }
}
