using System.Text.RegularExpressions;

internal static partial class ToolHelpBracketedUsageArgumentSupport
{
    public static IReadOnlyList<ToolHelpItem> Extract(
        string line,
        ISet<string> seen,
        bool hasChildCommands,
        out bool stopLine)
    {
        stopLine = false;
        var arguments = new List<ToolHelpItem>();
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

    private static ToolHelpItem? TryExtract(
        string line,
        Match match,
        ISet<string> seen,
        bool hasChildCommands,
        out bool stopLine)
    {
        stopLine = false;
        var value = match.Groups["name"].Value.Trim();
        if (ToolHelpOptionSignatureSupport.LooksLikeOptionPlaceholder(value)
            || ToolHelpOptionSignatureSupport.AppearsInOptionClause(line, match))
        {
            return null;
        }

        if (ToolHelpUsageArgumentPatternSupport.IsDispatcherPlaceholder(value))
        {
            stopLine = hasChildCommands;
            return null;
        }

        if (string.Equals(value, "options", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var quantifier = ToolHelpUsageArgumentPatternSupport.GetUsageArgumentQuantifier(line, match);
        var isSequence = value.Contains("...", StringComparison.Ordinal) || quantifier is '*' or '+';
        var isRequired = !match.Value.StartsWith("[", StringComparison.Ordinal) && quantifier is not '*' and not '?';
        var normalizedKey = ToolHelpUsageArgumentPatternSupport.NormalizeUsageArgumentKey(value, isSequence);
        return seen.Add(normalizedKey)
            ? new ToolHelpItem(normalizedKey, isRequired, null)
            : null;
    }

    [GeneratedRegex(@"(?<all>\[?<(?<name>[^>]+)>\]?)", RegexOptions.Compiled)]
    private static partial Regex UsageArgumentRegex();
}
