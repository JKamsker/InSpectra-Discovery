namespace InSpectra.Discovery.Tool.Help;

using System.Text.RegularExpressions;

internal static partial class ToolHelpApplicationDescriptionInference
{
    public static string? Infer(IReadOnlyList<string> preamble, int descriptionStartIndex)
    {
        var descriptionLines = new List<string>();
        foreach (var rawLine in preamble.Skip(descriptionStartIndex))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (descriptionLines.Count > 0)
                {
                    break;
                }

                continue;
            }

            if (ShouldIgnoreLine(line))
            {
                continue;
            }

            if (ShouldStop(line))
            {
                break;
            }

            descriptionLines.Add(line);
        }

        return descriptionLines.Count == 0 ? null : string.Join("\n", descriptionLines);
    }

    private static bool ShouldIgnoreLine(string line)
        => line.StartsWith("Copyright", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("Active runtime version", StringComparison.OrdinalIgnoreCase)
            || line.StartsWith("The .NET Foundation", StringComparison.OrdinalIgnoreCase)
            || line.EndsWith(".com", StringComparison.OrdinalIgnoreCase);

    private static bool ShouldStop(string line)
    {
        if (line.StartsWith("-", StringComparison.Ordinal)
            || line.StartsWith("/", StringComparison.Ordinal)
            || ToolHelpCommandPrototypeSupport.LooksLikeBareShortLongOptionRow(line)
            || ToolHelpCommandPrototypeSupport.TryParseBareShortLongAliasRow(line, out _, out _, out _))
        {
            return true;
        }

        return PositionalArgumentRowRegex().IsMatch(line)
            || TitleVersionLineRegex().IsMatch(line)
            || UsagePrototypeRegex().IsMatch(line)
            || InventoryEntryRegex().IsMatch(line)
            || MarkdownHeadingRegex().IsMatch(line);
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.-]*\s+(?:\(pos\.\s*\d+\)|pos\.\s*\d+)(?:\s+\S.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PositionalArgumentRowRegex();

    [GeneratedRegex(@"^.+?\s+v?\d[\w\.\-\+]*$", RegexOptions.Compiled)]
    private static partial Regex TitleVersionLineRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.-]*(?:\s+[^\s].*)?(?:\[[^\]]+\]|<[^>]+>| \| )", RegexOptions.Compiled)]
    private static partial Regex UsagePrototypeRegex();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.-]*(?:\s+[A-Za-z][A-Za-z0-9_.-]*)*\s{2,}\S.*$", RegexOptions.Compiled)]
    private static partial Regex InventoryEntryRegex();

    [GeneratedRegex(@"^#+\s+[A-Za-z]", RegexOptions.Compiled)]
    private static partial Regex MarkdownHeadingRegex();
}

