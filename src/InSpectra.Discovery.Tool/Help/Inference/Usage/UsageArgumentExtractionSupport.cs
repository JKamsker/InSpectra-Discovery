namespace InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.Documents;
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

        foreach (var line in usageLines)
        {
            if (ShouldSkipDescendantUsageLine(invocation, line, hasChildCommands))
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
}
