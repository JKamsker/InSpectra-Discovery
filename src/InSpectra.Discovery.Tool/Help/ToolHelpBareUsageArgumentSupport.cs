namespace InSpectra.Discovery.Tool.Help;

using System.Text.RegularExpressions;

internal static partial class ToolHelpBareUsageArgumentSupport
{
    public static ToolHelpItem? TryExtract(string invocation, string line, bool hasChildCommands)
    {
        var remainder = StripUsageInvocationPrefix(invocation, line);
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
            || string.Equals(value, "options", StringComparison.OrdinalIgnoreCase)
            || ToolHelpUsageArgumentPatternSupport.IsDispatcherPlaceholder(value)
            || hasChildCommands)
        {
            return null;
        }

        var normalizedValue = NormalizeBareUsageArgumentValue(value);
        var isSequence = match.Groups["ellipsis"].Success || match.Groups["quantifier"].Value is "*" or "+";
        var isRequired = !match.Groups["optional"].Success && match.Groups["quantifier"].Value is not "*" and not "?";
        return new ToolHelpItem(
            ToolHelpUsageArgumentPatternSupport.NormalizeUsageArgumentKey(normalizedValue, isSequence),
            isRequired,
            null);
    }

    public static bool LooksLikeExampleLabel(string? line)
        => !string.IsNullOrWhiteSpace(line)
            && line.TrimEnd().EndsWith(":", StringComparison.Ordinal)
            && line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 3;

    public static bool LooksLikeWrappedUsageValueContinuation(string? previousLine, string currentLine)
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

    private static string StripUsageInvocationPrefix(string invocation, string line)
        => line.StartsWith(invocation, StringComparison.OrdinalIgnoreCase)
            ? line[invocation.Length..].Trim()
            : line.Trim();

    private static string NormalizeBareUsageArgumentValue(string value)
    {
        var trimmed = value.Trim('[', ']').Trim();
        return trimmed.Contains('/') || trimmed.Contains('\\') || FileLikeUsageTokenRegex().IsMatch(trimmed)
            ? "FILE"
            : trimmed;
    }

    [GeneratedRegex(@"\[(?=[^\]]*(?:-|/))[^\]]+\]", RegexOptions.Compiled)]
    private static partial Regex BareOptionClauseRegex();

    [GeneratedRegex(@"^(?<optional>\[)?(?<name>[A-Za-z0-9][A-Za-z0-9._/\-\\:]*?)(?<ellipsis>\.\.\.)?(?(optional)\])(?<quantifier>[+*?])?$", RegexOptions.Compiled)]
    private static partial Regex SingleBareUsageArgumentRegex();

    [GeneratedRegex(@"\.[A-Za-z0-9]{1,8}$", RegexOptions.Compiled)]
    private static partial Regex FileLikeUsageTokenRegex();
}

