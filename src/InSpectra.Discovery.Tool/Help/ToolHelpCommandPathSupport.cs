internal static class ToolHelpCommandPathSupport
{
    public static string ResolveChildKey(string rootCommandName, string parentKey, string childKey)
    {
        var parentSegments = SplitSegments(parentKey);
        foreach (var candidate in EnumerateCandidates(childKey)
            .OrderByDescending(candidate => candidate.Sum(segment => segment.Length)))
        {
            var relative = TrimPrefix(candidate, SplitSegments(rootCommandName));
            if (StartsWith(relative, parentSegments))
            {
                relative = relative[parentSegments.Length..];
            }

            if (relative.Length == 0)
            {
                continue;
            }

            return parentSegments.Length == 0
                ? string.Join(' ', relative)
                : $"{parentKey} {string.Join(' ', relative)}";
        }

        return parentKey;
    }

    public static string[] ResolveStoredCaptureSegments(string rootCommandName, string commandKey, ToolHelpDocument document)
    {
        if (string.IsNullOrWhiteSpace(commandKey))
        {
            return [];
        }

        var rootSegments = SplitSegments(rootCommandName);
        var candidates = EnumerateCandidates(commandKey)
            .Select(candidate => TrimPrefix(candidate, rootSegments))
            .Distinct(CommandPathComparer.Instance)
            .OrderByDescending(candidate => candidate.Length)
            .ToArray();
        foreach (var candidate in candidates)
        {
            if (ToolHelpDocumentInspector.IsCompatible(candidate, document))
            {
                return candidate;
            }
        }

        return candidates.FirstOrDefault() ?? [];
    }

    public static string[] SplitSegments(string? commandKey)
        => string.IsNullOrWhiteSpace(commandKey)
            ? []
            : commandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static IEnumerable<string[]> EnumerateCandidates(string commandKey)
    {
        var groups = new List<string[]>();
        var tokens = SplitSegments(commandKey);
        for (var index = 0; index < tokens.Length; index++)
        {
            var aliases = new List<string>();
            var token = NormalizeToken(tokens[index]);
            if (token.Length == 0)
            {
                continue;
            }

            aliases.Add(token);
            while (tokens[index].EndsWith(",", StringComparison.Ordinal) && index + 1 < tokens.Length)
            {
                index++;
                var alias = NormalizeToken(tokens[index]);
                if (alias.Length > 0)
                {
                    aliases.Add(alias);
                }
            }

            groups.Add(aliases
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
        }

        return Expand(groups, 0, []);
    }

    private static IEnumerable<string[]> Expand(IReadOnlyList<string[]> groups, int index, IReadOnlyList<string> current)
    {
        if (index >= groups.Count)
        {
            yield return current.ToArray();
            yield break;
        }

        foreach (var alias in groups[index])
        {
            var next = current.Concat([alias]).ToArray();
            foreach (var expanded in Expand(groups, index + 1, next))
            {
                yield return expanded;
            }
        }
    }

    private static string NormalizeToken(string token)
        => token.Trim().Trim(',', ':');

    private static bool StartsWith(IReadOnlyList<string> value, IReadOnlyList<string> prefix)
    {
        if (prefix.Count == 0 || value.Count < prefix.Count)
        {
            return prefix.Count == 0;
        }

        for (var index = 0; index < prefix.Count; index++)
        {
            if (!string.Equals(value[index], prefix[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] TrimPrefix(IReadOnlyList<string> value, IReadOnlyList<string> prefix)
        => StartsWith(value, prefix)
            ? value.Skip(prefix.Count).ToArray()
            : value.ToArray();

    private sealed class CommandPathComparer : IEqualityComparer<string[]>
    {
        public static CommandPathComparer Instance { get; } = new();

        public bool Equals(string[]? x, string[]? y)
            => x is not null && y is not null && x.SequenceEqual(y, StringComparer.OrdinalIgnoreCase);

        public int GetHashCode(string[] obj)
        {
            var hash = new HashCode();
            foreach (var segment in obj)
            {
                hash.Add(segment, StringComparer.OrdinalIgnoreCase);
            }

            return hash.ToHashCode();
        }
    }
}
