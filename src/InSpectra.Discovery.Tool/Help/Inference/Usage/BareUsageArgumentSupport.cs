namespace InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.Signatures;

using InSpectra.Discovery.Tool.Help.Documents;

using System.Text.RegularExpressions;

internal static partial class BareUsageArgumentSupport
{
    public static Item? TryExtract(string invocation, string line, bool hasChildCommands)
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
            || OptionSignatureSupport.LooksLikeOptionPlaceholder(value)
            || UsageArgumentPatternSupport.IsOptionsPlaceholder(value)
            || UsageArgumentPatternSupport.IsDispatcherPlaceholder(value)
            || hasChildCommands)
        {
            return null;
        }

        var normalizedValue = NormalizeBareUsageArgumentValue(value);
        var isSequence = match.Groups["ellipsis"].Success || match.Groups["quantifier"].Value is "*" or "+";
        var isRequired = !match.Groups["optional"].Success && match.Groups["quantifier"].Value is not "*" and not "?";
        return new Item(
            UsageArgumentPatternSupport.NormalizeUsageArgumentKey(normalizedValue, isSequence),
            isRequired,
            null);
    }

    public static bool LooksLikeExampleLabel(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        if (!trimmed.EndsWith(":", StringComparison.Ordinal))
        {
            return false;
        }

        var content = trimmed[..^1].TrimEnd();
        return content.Length > 0
            && !content.StartsWith("-", StringComparison.Ordinal)
            && !content.Contains("--", StringComparison.Ordinal)
            && content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 8;
    }

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
