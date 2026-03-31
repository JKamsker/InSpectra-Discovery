namespace InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.Signatures;

using InSpectra.Discovery.Tool.Help.Documents;

using System.Text.RegularExpressions;

internal static partial class BracketedUsageArgumentSupport
{
    public static IReadOnlyList<Item> Extract(
        string line,
        ISet<string> seen,
        bool hasChildCommands,
        out bool stopLine)
    {
        stopLine = false;
        var arguments = new List<Item>();
        foreach (Match match in UsageArgumentRegex().Matches(line))
        {
            var argument = TryExtract(line, match, seen, hasChildCommands, out stopLine);
            if (stopLine)
            {
                break;
            }

            if (argument is not null)
            {
                arguments.Add(argument);
            }
        }

        return arguments;
    }

    private static Item? TryExtract(
        string line,
        Match match,
        ISet<string> seen,
        bool hasChildCommands,
        out bool stopLine)
    {
        stopLine = false;
        var value = match.Groups["name"].Value.Trim();
        if (OptionSignatureSupport.LooksLikeOptionPlaceholder(value)
            || OptionSignatureSupport.AppearsInOptionClause(line, match))
        {
            return null;
        }

        if (UsageArgumentPatternSupport.IsDispatcherPlaceholder(value))
        {
            stopLine = hasChildCommands;
            return null;
        }

        if (string.Equals(value, "options", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var quantifier = UsageArgumentPatternSupport.GetUsageArgumentQuantifier(line, match);
        var isSequence = value.Contains("...", StringComparison.Ordinal) || quantifier is '*' or '+';
        var isRequired = !match.Value.StartsWith("[", StringComparison.Ordinal) && quantifier is not '*' and not '?';
        var normalizedKey = UsageArgumentPatternSupport.NormalizeUsageArgumentKey(value, isSequence);
        return seen.Add(normalizedKey)
            ? new Item(normalizedKey, isRequired, null)
            : null;
    }

    [GeneratedRegex(@"(?<all>\[?<(?<name>[^>]+)>\]?)", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();
}

