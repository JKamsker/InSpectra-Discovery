using System.Text.RegularExpressions;

internal static partial class ToolHelpPreambleArgumentInference
{
    public static IReadOnlyList<string> InferArgumentLines(IReadOnlyList<string> preamble, string? title)
    {
        var candidateLines = preamble.Skip(string.IsNullOrWhiteSpace(title) ? 0 : 1);
        var results = new List<string>();
        var hasRows = false;
        var currentRowCaptured = false;

        foreach (var rawLine in candidateLines)
        {
            if (PositionalArgumentRowRegex().IsMatch(rawLine.TrimStart()))
            {
                results.Add(rawLine);
                hasRows = true;
                currentRowCaptured = true;
                continue;
            }

            if (currentRowCaptured && rawLine.Length > 0 && char.IsWhiteSpace(rawLine, 0))
            {
                results.Add(rawLine);
                continue;
            }

            currentRowCaptured = false;
        }

        return hasRows ? results : [];
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9_.-]*\s+(?:\(pos\.\s*\d+\)|pos\.\s*\d+)(?:\s{2,}\S.*)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PositionalArgumentRowRegex();
}
