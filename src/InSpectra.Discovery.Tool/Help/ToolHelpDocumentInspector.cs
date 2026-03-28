internal static class ToolHelpDocumentInspector
{
    public static int Score(ToolHelpDocument document)
        => document.UsageLines.Count * 10
            + document.Options.Count * 5
            + document.Commands.Count * 5
            + document.Arguments.Count * 3
            + (!string.IsNullOrWhiteSpace(document.CommandDescription) ? 2 : 0);

    public static bool IsCompatible(IReadOnlyList<string> commandSegments, ToolHelpDocument document)
    {
        if (commandSegments.Count == 0)
        {
            return HasStructuredContent(document);
        }

        if (document.UsageLines.Any(line => ContainsPath(line, commandSegments))
            || ContainsPath(document.Title, commandSegments))
        {
            return true;
        }

        if (document.Commands.Count > 0 || LooksLikeDispatcherUsage(document.UsageLines))
        {
            return false;
        }

        return document.Options.Count > 0
            || document.Arguments.Count > 0
            || !string.IsNullOrWhiteSpace(document.CommandDescription);
    }

    public static bool IsCompatible(string[] commandSegments, ToolHelpDocument document)
        => IsCompatible((IReadOnlyList<string>)commandSegments, document);

    private static bool HasStructuredContent(ToolHelpDocument document)
        => document.UsageLines.Count > 0
            || document.Options.Count > 0
            || document.Commands.Count > 0
            || document.Arguments.Count > 0
            || !string.IsNullOrWhiteSpace(document.CommandDescription);

    private static bool LooksLikeDispatcherUsage(IReadOnlyList<string> usageLines)
        => usageLines.Any(line =>
            line.Contains("[command]", StringComparison.OrdinalIgnoreCase)
            || line.Contains("<command>", StringComparison.OrdinalIgnoreCase)
            || line.Contains("[subcommand]", StringComparison.OrdinalIgnoreCase)
            || line.Contains("<subcommand>", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsPath(string? line, IReadOnlyList<string> commandSegments)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < commandSegments.Count)
        {
            return false;
        }

        for (var start = 0; start <= tokens.Length - commandSegments.Count; start++)
        {
            var matched = true;
            for (var index = 0; index < commandSegments.Count; index++)
            {
                if (!string.Equals(tokens[start + index], commandSegments[index], StringComparison.OrdinalIgnoreCase))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class ToolHelpInvocationComparer : IEqualityComparer<string[]>
{
    public bool Equals(string[]? x, string[]? y)
        => x is not null && y is not null && x.SequenceEqual(y, StringComparer.OrdinalIgnoreCase);

    public int GetHashCode(string[] obj)
    {
        var hash = new HashCode();
        foreach (var item in obj)
        {
            hash.Add(item, StringComparer.OrdinalIgnoreCase);
        }

        return hash.ToHashCode();
    }
}
