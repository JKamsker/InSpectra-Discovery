namespace InSpectra.Discovery.Tool.Help;

using System.Text.RegularExpressions;

internal static class UsageArgumentPatternSupport
{
    public static bool IsDispatcherPlaceholder(string value)
        => string.Equals(value, "command", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "subcommand", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeUsageArgumentKey(string rawValue, bool isSequence)
    {
        var trimmed = rawValue.Trim();
        if (trimmed.EndsWith("...", StringComparison.Ordinal))
        {
            return trimmed;
        }

        return isSequence ? $"{trimmed}..." : trimmed;
    }

    public static char? GetUsageArgumentQuantifier(string line, Match match)
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
}

