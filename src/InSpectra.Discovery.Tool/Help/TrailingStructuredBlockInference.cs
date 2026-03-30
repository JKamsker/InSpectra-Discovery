namespace InSpectra.Discovery.Tool.Help;

using System.Text.RegularExpressions;

internal static partial class TrailingStructuredBlockInference
{
    public static TrailingStructuredBlock Infer(IReadOnlyDictionary<string, List<string>> sections)
    {
        var candidateLines = sections
            .Where(pair => !string.Equals(pair.Key, "arguments", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pair.Key, "commands", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pair.Key, "options", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(pair.Key, "usage", StringComparison.OrdinalIgnoreCase))
            .SelectMany(pair => pair.Value)
            .ToArray();

        var bestOptionLines = Array.Empty<string>();
        var bestArgumentLines = Array.Empty<string>();
        var currentOptionLines = new List<string>();
        var currentArgumentLines = new List<string>();
        var rowCount = 0;
        var currentRowKind = StructuredRowKind.None;
        var previousWasBlank = true;

        void Commit()
        {
            if (rowCount >= 2)
            {
                bestOptionLines = currentOptionLines.ToArray();
                bestArgumentLines = currentArgumentLines.ToArray();
            }

            currentOptionLines.Clear();
            currentArgumentLines.Clear();
            rowCount = 0;
            currentRowKind = StructuredRowKind.None;
        }

        foreach (var rawLine in candidateLines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                if (rowCount > 0)
                {
                    GetTargetLines(currentRowKind, currentOptionLines, currentArgumentLines).Add(rawLine);
                }

                previousWasBlank = true;
                continue;
            }

            if (TryClassifyRow(rawLine, previousWasBlank || rowCount > 0, out var rowKind))
            {
                rowCount++;
                currentRowKind = rowKind;
                GetTargetLines(rowKind, currentOptionLines, currentArgumentLines).Add(rawLine);
                previousWasBlank = false;
                continue;
            }

            if (rowCount > 0 && currentRowKind != StructuredRowKind.None && GetIndentation(rawLine) > 0)
            {
                GetTargetLines(currentRowKind, currentOptionLines, currentArgumentLines).Add(rawLine);
                previousWasBlank = false;
                continue;
            }

            Commit();
            previousWasBlank = false;
        }

        Commit();
        return new TrailingStructuredBlock(bestOptionLines, bestArgumentLines);
    }

    private static List<string> GetTargetLines(
        StructuredRowKind rowKind,
        List<string> optionLines,
        List<string> argumentLines)
        => rowKind switch
        {
            StructuredRowKind.Argument => argumentLines,
            StructuredRowKind.Option => optionLines,
            _ => throw new InvalidOperationException($"Unexpected structured row kind '{rowKind}'."),
        };

    private static bool TryClassifyRow(string rawLine, bool mayStartBlock, out StructuredRowKind rowKind)
    {
        rowKind = StructuredRowKind.None;
        if (!mayStartBlock)
        {
            return false;
        }

        var trimmed = rawLine.Trim();
        if (OptionRowRegex().IsMatch(trimmed) || CommandPrototypeSupport.LooksLikeBareShortLongOptionRow(rawLine))
        {
            rowKind = StructuredRowKind.Option;
            return true;
        }

        if (PositionalArgumentRowRegex().IsMatch(trimmed))
        {
            rowKind = StructuredRowKind.Argument;
            return true;
        }

        return false;
    }

    private static int GetIndentation(string rawLine)
        => rawLine.TakeWhile(char.IsWhiteSpace).Count();

    [GeneratedRegex(@"^(?:--?[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*)(?:\s*[,|]\s*(?:--?[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*|/[A-Za-z0-9\?][A-Za-z0-9_\.\?\-]*))*?(?:\s{2,}\S.*)?$", RegexOptions.Compiled)]
    private static partial Regex OptionRowRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.-]*\s+(?:\(pos\.\s*\d+\)|pos\.\s*\d+)(?:\s+\S.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PositionalArgumentRowRegex();

    internal readonly record struct TrailingStructuredBlock(
        IReadOnlyList<string> OptionLines,
        IReadOnlyList<string> ArgumentLines);

    private enum StructuredRowKind
    {
        None,
        Option,
        Argument,
    }
}

