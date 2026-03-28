using System.Text.RegularExpressions;

internal static partial class ToolHelpOptionDescriptionArgumentInference
{
    public static IReadOnlyList<ToolHelpItem> Infer(IReadOnlyList<ToolHelpItem> options)
    {
        var arguments = new List<ToolHelpItem>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Description))
            {
                continue;
            }

            var lines = option.Description
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                var trimmed = lines[index].Trim();
                var match = PositionalArgumentRowRegex().Match(trimmed);
                if (!match.Success)
                {
                    continue;
                }

                var key = match.Groups["key"].Value.Trim();
                var description = match.Groups["description"].Success ? match.Groups["description"].Value.Trim() : null;
                var isRequired = false;
                if (!string.IsNullOrWhiteSpace(description)
                    && description.StartsWith("Required.", StringComparison.OrdinalIgnoreCase))
                {
                    isRequired = true;
                    description = description["Required.".Length..].TrimStart();
                }

                while (index + 1 < lines.Length && lines[index + 1].Length > 0 && char.IsWhiteSpace(lines[index + 1], 0))
                {
                    index++;
                    var continuation = lines[index].Trim();
                    description = string.IsNullOrWhiteSpace(description)
                        ? continuation
                        : $"{description}\n{continuation}";
                }

                if (seen.Add(key))
                {
                    arguments.Add(new ToolHelpItem(key, isRequired, description));
                }
            }
        }

        return arguments;
    }

    [GeneratedRegex(@"^(?<key>[A-Za-z][A-Za-z0-9_.-]*)\s+(?:\(pos\.\s*\d+\)|pos\.\s*\d+)(?:\s+(?<description>\S.*))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PositionalArgumentRowRegex();
}
