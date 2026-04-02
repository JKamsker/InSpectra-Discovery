namespace InSpectra.Discovery.Tool.Help.Parsing;


using System.Text.RegularExpressions;

internal static partial class UsageSectionSplitter
{
    public static UsageSectionParts Split(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return UsageSectionParts.Empty;
        }

        var usageLines = new List<string>();
        var argumentLines = new List<string>();
        var optionLines = new List<string>();
        var currentTarget = UsageSectionTarget.Usage;
        var currentIndentation = -1;
        var sawUsageSeparator = false;

        for (var index = 0; index < lines.Count; index++)
        {
            var rawLine = lines[index];
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                if (currentTarget == UsageSectionTarget.Usage)
                {
                    sawUsageSeparator |= usageLines.Count > 0;
                    usageLines.Add(rawLine);
                }
                else
                {
                    GetTargetLines(currentTarget, argumentLines, optionLines).Add(rawLine);
                }

                continue;
            }

            if (sawUsageSeparator
                && TryClassifyStructuredGroupLabel(lines, index, out currentTarget))
            {
                currentIndentation = GetIndentation(rawLine);
                continue;
            }

            if (currentTarget != UsageSectionTarget.Usage)
            {
                if (TryClassifyStructuredUsageLine(rawLine, out var nextTarget, out var indentation))
                {
                    currentTarget = nextTarget;
                    currentIndentation = indentation;
                    GetTargetLines(currentTarget, argumentLines, optionLines).Add(rawLine);
                    continue;
                }

                if (TryClassifyBareArgumentUsageLine(lines, index, out nextTarget, out indentation))
                {
                    currentTarget = nextTarget;
                    currentIndentation = indentation;
                    GetTargetLines(currentTarget, argumentLines, optionLines).Add(rawLine);
                    continue;
                }

                if (GetIndentation(rawLine) > currentIndentation)
                {
                    GetTargetLines(currentTarget, argumentLines, optionLines).Add(rawLine);
                    continue;
                }
            }

            if (sawUsageSeparator && TryClassifyStructuredUsageLine(rawLine, out currentTarget, out currentIndentation))
            {
                GetTargetLines(currentTarget, argumentLines, optionLines).Add(rawLine);
                continue;
            }

            if (sawUsageSeparator && TryClassifyBareArgumentUsageLine(lines, index, out currentTarget, out currentIndentation))
            {
                GetTargetLines(currentTarget, argumentLines, optionLines).Add(rawLine);
                continue;
            }

            currentTarget = UsageSectionTarget.Usage;
            usageLines.Add(rawLine);
        }

        return new UsageSectionParts(usageLines, argumentLines, optionLines);
    }

    private static List<string> GetTargetLines(
        UsageSectionTarget target,
        List<string> argumentLines,
        List<string> optionLines)
        => target switch
        {
            UsageSectionTarget.Arguments => argumentLines,
            UsageSectionTarget.Options => optionLines,
            _ => throw new InvalidOperationException($"Unexpected usage section target '{target}'."),
        };

    private static bool TryClassifyStructuredUsageLine(string rawLine, out UsageSectionTarget target, out int indentation)
    {
        target = UsageSectionTarget.Usage;
        indentation = GetIndentation(rawLine);

        var trimmed = rawLine.TrimStart();
        if (LegacyOptionRowSupport.LooksLikeLooseOptionRow(trimmed))
        {
            target = UsageSectionTarget.Options;
            return true;
        }

        if (PositionalArgumentRowRegex().IsMatch(trimmed))
        {
            target = UsageSectionTarget.Arguments;
            return true;
        }

        return false;
    }

    private static bool TryClassifyBareArgumentUsageLine(
        IReadOnlyList<string> lines,
        int index,
        out UsageSectionTarget target,
        out int indentation)
    {
        target = UsageSectionTarget.Usage;
        indentation = GetIndentation(lines[index]);
        var trimmed = lines[index].Trim();
        if (!LooksLikeBareArgumentKey(trimmed))
        {
            return false;
        }

        for (var nextIndex = index + 1; nextIndex < lines.Count; nextIndex++)
        {
            var nextLine = lines[nextIndex];
            if (string.IsNullOrWhiteSpace(nextLine))
            {
                continue;
            }

            if (GetIndentation(nextLine) <= indentation)
            {
                return false;
            }

            var description = nextLine.Trim();
            if (!LooksLikeArgumentDescription(description))
            {
                return false;
            }

            target = UsageSectionTarget.Arguments;
            return true;
        }

        return false;
    }

    private static bool TryClassifyStructuredGroupLabel(
        IReadOnlyList<string> lines,
        int index,
        out UsageSectionTarget target)
    {
        target = UsageSectionTarget.Usage;
        var trimmed = lines[index].Trim();
        if (!LooksLikeStructuredGroupLabel(trimmed))
        {
            return false;
        }

        for (var nextIndex = index + 1; nextIndex < lines.Count; nextIndex++)
        {
            var nextLine = lines[nextIndex];
            if (string.IsNullOrWhiteSpace(nextLine))
            {
                continue;
            }

            return TryClassifyStructuredUsageLine(nextLine, out target, out _);
        }

        return false;
    }

    private static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();

    private static bool LooksLikeStructuredGroupLabel(string line)
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

    private static bool LooksLikeBareArgumentKey(string line)
    {
        if (string.IsNullOrWhiteSpace(line)
            || line.EndsWith(":", StringComparison.Ordinal)
            || line.StartsWith("-", StringComparison.Ordinal)
            || line.StartsWith("/", StringComparison.Ordinal)
            || line.Contains('[', StringComparison.Ordinal)
            || line.Contains('<', StringComparison.Ordinal))
        {
            return false;
        }

        return line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(token => token.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'));
    }

    private static bool LooksLikeArgumentDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        return description.StartsWith("The ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("A ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("An ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Required.", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Optional.", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Arbitrary ", StringComparison.OrdinalIgnoreCase)
            || description.StartsWith("Signifies ", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^\S(?:.*?\S)?\s+(?:\(pos\.\s*\d+\)|pos\.\s*\d+)(?:\s+\S.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PositionalArgumentRowRegex();

    internal readonly record struct UsageSectionParts(
        IReadOnlyList<string> UsageLines,
        IReadOnlyList<string> ArgumentLines,
        IReadOnlyList<string> OptionLines)
    {
        public static UsageSectionParts Empty { get; } = new([], [], []);
    }

    private enum UsageSectionTarget
    {
        Usage,
        Arguments,
        Options,
    }
}
