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

        foreach (var rawLine in lines)
        {
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

            if (currentTarget != UsageSectionTarget.Usage)
            {
                if (TryClassifyStructuredUsageLine(rawLine, out var nextTarget, out var indentation))
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
        if (OptionRowRegex().IsMatch(trimmed))
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

    private static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();

    [GeneratedRegex(@"^(?:--?[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*)(?:\s*[,|]\s*(?:--?[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*))*\s{2,}\S", RegexOptions.Compiled)]
    private static partial Regex OptionRowRegex();

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

