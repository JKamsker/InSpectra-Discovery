using System.Text.RegularExpressions;

internal static partial class ToolHelpUsageArgumentSupport
{
    public static IReadOnlyList<ToolHelpItem> ExtractUsageArguments(
        string commandName,
        string commandPath,
        IReadOnlyList<string> usageLines,
        bool hasChildCommands)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var arguments = new List<ToolHelpItem>();
        string? previousNonEmptyLine = null;

        foreach (var line in usageLines)
        {
            var lineArguments = new List<ToolHelpItem>();
            foreach (Match match in UsageArgumentRegex().Matches(line))
            {
                var value = match.Groups["name"].Value.Trim();
                if (ToolHelpOptionSignatureSupport.LooksLikeOptionPlaceholder(value)
                    || ToolHelpOptionSignatureSupport.AppearsInOptionClause(line, match))
                {
                    continue;
                }

                if (IsDispatcherPlaceholder(value))
                {
                    if (hasChildCommands)
                    {
                        break;
                    }

                    continue;
                }

                if (string.Equals(value, "options", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var quantifier = GetUsageArgumentQuantifier(line, match);
                var isSequence = value.Contains("...", StringComparison.Ordinal) || quantifier is '*' or '+';
                var isRequired = !match.Value.StartsWith("[", StringComparison.Ordinal) && quantifier is not '*' and not '?';
                var normalizedKey = NormalizeUsageArgumentKey(value, isSequence);
                if (!seen.Add(normalizedKey))
                {
                    continue;
                }

                var argument = new ToolHelpItem(normalizedKey, isRequired, null);
                lineArguments.Add(argument);
                arguments.Add(argument);
            }

            if (lineArguments.Count > 0)
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (LooksLikeWrappedUsageValueContinuation(previousNonEmptyLine, line))
            {
                previousNonEmptyLine = line;
                continue;
            }

            if (!LooksLikeExampleLabel(previousNonEmptyLine))
            {
                var bareArgument = ExtractBareUsageArgument(commandName, commandPath, line, hasChildCommands);
                if (bareArgument is not null && seen.Add(bareArgument.Key))
                {
                    arguments.Add(bareArgument);
                }
            }

            previousNonEmptyLine = line;
        }

        return arguments;
    }

    public static IReadOnlyList<ToolHelpItem> SelectArguments(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<ToolHelpItem> usageArguments)
    {
        if (explicitArguments.Count == 0)
        {
            return usageArguments;
        }

        if (usageArguments.Count == 0)
        {
            return explicitArguments.All(ToolHelpArgumentNodeBuilder.IsLowSignalExplicitArgument)
                ? []
                : explicitArguments;
        }

        if (explicitArguments.Count != usageArguments.Count)
        {
            return explicitArguments.All(ToolHelpArgumentNodeBuilder.IsLowSignalExplicitArgument)
                ? usageArguments
                : explicitArguments;
        }

        var merged = new List<ToolHelpItem>(explicitArguments.Count);
        var changed = false;
        for (var index = 0; index < explicitArguments.Count; index++)
        {
            var mergedArgument = MergeArguments(explicitArguments[index], usageArguments[index]);
            merged.Add(mergedArgument);
            changed |= !string.Equals(explicitArguments[index].Key, mergedArgument.Key, StringComparison.Ordinal)
                || explicitArguments[index].IsRequired != mergedArgument.IsRequired
                || !string.Equals(explicitArguments[index].Description, mergedArgument.Description, StringComparison.Ordinal);
        }

        return changed ? merged : explicitArguments;
    }

    public static bool LooksLikeCommandInventoryEchoArguments(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<ToolHelpItem> commands)
    {
        if (explicitArguments.Count == 0 || commands.Count == 0 || explicitArguments.Count != commands.Count)
        {
            return false;
        }

        return explicitArguments.Zip(commands).All(pair =>
            string.Equals(
                NormalizeCommandInventoryKey(pair.First.Key),
                NormalizeCommandInventoryKey(pair.Second.Key),
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                NormalizeInlineText(pair.First.Description),
                NormalizeInlineText(pair.Second.Description),
                StringComparison.OrdinalIgnoreCase));
    }

    public static bool LooksLikeAuxiliaryInventoryEchoArguments(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<string> usageLines)
    {
        if (explicitArguments.Count == 0)
        {
            return false;
        }

        var normalizedUsage = usageLines
            .Select(NormalizeInlineText)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (normalizedUsage.Length == 0 || normalizedUsage.Any(line => !line.Contains("command", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return explicitArguments.All(argument => NormalizeCommandInventoryKey(argument.Key) == "command");
    }

    private static ToolHelpItem MergeArguments(ToolHelpItem explicitArgument, ToolHelpItem usageArgument)
    {
        if (!ToolHelpArgumentNodeBuilder.TryParseArgumentSignature(explicitArgument.Key, out var explicitSignature)
            || !ToolHelpArgumentNodeBuilder.TryParseArgumentSignature(usageArgument.Key, out var usageSignature))
        {
            return explicitArgument;
        }

        return explicitSignature.Name == usageSignature.Name
            ? new ToolHelpItem(
                usageArgument.Key,
                explicitArgument.IsRequired || usageArgument.IsRequired,
                explicitArgument.Description ?? usageArgument.Description)
            : explicitArgument;
    }

    private static bool IsDispatcherPlaceholder(string value)
        => string.Equals(value, "command", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "subcommand", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeExampleLabel(string? line)
        => !string.IsNullOrWhiteSpace(line)
            && line.TrimEnd().EndsWith(":", StringComparison.Ordinal)
            && line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3;

    private static string NormalizeInlineText(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("`", string.Empty, StringComparison.Ordinal).Trim();

    private static string NormalizeCommandInventoryKey(string key)
        => NormalizeInlineText(key).Trim('[', ']', '<', '>', '(', ')').ToLowerInvariant();

    private static string NormalizeUsageArgumentKey(string rawValue, bool isSequence)
    {
        var trimmed = rawValue.Trim();
        if (trimmed.EndsWith("...", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return isSequence ? $"{trimmed}..." : trimmed;
    }

    private static char? GetUsageArgumentQuantifier(string line, Match match)
    {
        var directQuantifier = TryReadUsageQuantifier(line, match.Index + match.Length);
        if (directQuantifier is not null)
        {
            return directQuantifier;
        }

        var groupStart = line.LastIndexOf('(', match.Index);
        if (groupStart < 0)
        {
            return null;
        }

        var groupEnd = line.IndexOf(')', match.Index + match.Length);
        if (groupEnd < 0 || groupStart >= match.Index || groupEnd < match.Index + match.Length)
        {
            return null;
        }

        return TryReadUsageQuantifier(line, groupEnd + 1);
    }

    private static char? TryReadUsageQuantifier(string line, int startIndex)
    {
        var index = startIndex;
        while (index < line.Length && char.IsWhiteSpace(line[index]))
        {
            index++;
        }

        return index < line.Length && line[index] is '*' or '+' or '?'
            ? line[index]
            : null;
    }

    private static ToolHelpItem? ExtractBareUsageArgument(string commandName, string commandPath, string line, bool hasChildCommands)
    {
        var remainder = StripUsageInvocationPrefix(commandName, commandPath, line);
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return null;
        }

        remainder = BareOptionClauseRegex().Replace(remainder, " ").Trim();
        if (string.IsNullOrWhiteSpace(remainder))
        {
            return null;
        }

        var match = SingleBareUsageArgumentRegex().Match(remainder);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["name"].Value.Trim();
        if (value.Length == 0
            || value.All(char.IsDigit)
            || value.EndsWith(":", StringComparison.Ordinal)
            || ToolHelpOptionSignatureSupport.LooksLikeOptionPlaceholder(value)
            || IsDispatcherPlaceholder(value)
            || string.Equals(value, "options", StringComparison.OrdinalIgnoreCase)
            || hasChildCommands)
        {
            return null;
        }

        var normalizedValue = NormalizeBareUsageArgumentValue(value);
        var isSequence = match.Groups["ellipsis"].Success || match.Groups["quantifier"].Value is "*" or "+";
        var isRequired = !match.Groups["optional"].Success && match.Groups["quantifier"].Value is not "*" and not "?";
        return new ToolHelpItem(NormalizeUsageArgumentKey(normalizedValue, isSequence), isRequired, null);
    }

    private static string StripUsageInvocationPrefix(string commandName, string commandPath, string line)
    {
        var invocation = string.IsNullOrWhiteSpace(commandPath)
            ? commandName
            : $"{commandName} {commandPath}";
        return line.StartsWith(invocation, StringComparison.OrdinalIgnoreCase)
            ? line[invocation.Length..].Trim()
            : line.Trim();
    }

    private static string NormalizeBareUsageArgumentValue(string value)
    {
        var trimmed = value.Trim('[', ']').Trim();
        return trimmed.Contains('/') || trimmed.Contains('\\') || FileLikeUsageTokenRegex().IsMatch(trimmed)
            ? "FILE"
            : trimmed;
    }

    private static bool LooksLikeWrappedUsageValueContinuation(string? previousLine, string currentLine)
    {
        if (string.IsNullOrWhiteSpace(previousLine))
        {
            return false;
        }

        var previous = previousLine.TrimEnd();
        var current = currentLine.Trim();
        if (current.Length == 0 || !char.IsWhiteSpace(currentLine, 0))
        {
            return false;
        }

        return previous.EndsWith("|", StringComparison.Ordinal)
            || previous.EndsWith(",", StringComparison.Ordinal)
            || previous.EndsWith("(", StringComparison.Ordinal)
            || previous.EndsWith("[", StringComparison.Ordinal)
            || previous.EndsWith("<", StringComparison.Ordinal)
            || previous.EndsWith("{", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<all>\[?<(?<name>[^>]+)>\]?)", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();

    [GeneratedRegex(@"\[(?=[^\]]*(?:-|/))[^\]]+\]", RegexOptions.Compiled)]
    private static partial Regex BareOptionClauseRegex();

    [GeneratedRegex(@"^(?<optional>\[)?(?<name>[A-Za-z0-9][A-Za-z0-9._/\-\\:]*?)(?<ellipsis>\.\.\.)?(?(optional)\])(?<quantifier>[+*?])?$", RegexOptions.Compiled)]
    private static partial Regex SingleBareUsageArgumentRegex();

    [GeneratedRegex(@"\.[A-Za-z0-9]{1,8}$", RegexOptions.Compiled)]
    private static partial Regex FileLikeUsageTokenRegex();
}
