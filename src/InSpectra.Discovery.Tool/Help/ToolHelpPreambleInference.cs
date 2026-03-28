internal static class ToolHelpPreambleInference
{
    public static IReadOnlyList<string> InferUsageLines(IReadOnlyList<string> preamble)
        => preamble
            .SelectMany(EnumerateUsageCandidates)
            .Where(line => line.Length > 0)
            .ToArray();

    public static IReadOnlyList<string> InferOptionCandidateLines(IReadOnlyList<string> preamble, string? title)
        => preamble.Skip(string.IsNullOrWhiteSpace(title) ? 0 : 1).ToArray();

    public static bool LooksLikeCommandSignature(string key)
    {
        var segments = key.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 1
            && segments.Skip(1).All(segment => segment.StartsWith("<", StringComparison.Ordinal)
                || segment.StartsWith("[", StringComparison.Ordinal));
    }

    private static IEnumerable<string> EnumerateUsageCandidates(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("Usage -", StringComparison.OrdinalIgnoreCase))
        {
            yield return trimmed["Usage".Length..].TrimStart(' ', ':', '-').Trim();
        }

        if (trimmed.StartsWith("```", StringComparison.Ordinal)
            && trimmed.EndsWith("```", StringComparison.Ordinal)
            && trimmed.Length > 6)
        {
            var fenced = trimmed[3..^3].Trim();
            if (LooksLikeUsageCandidate(fenced))
            {
                yield return fenced;
            }
        }
    }

    private static bool LooksLikeUsageCandidate(string line)
        => line.Contains('[', StringComparison.Ordinal)
            || line.Contains('<', StringComparison.Ordinal)
            || line.Contains("--", StringComparison.Ordinal)
            || line.Contains(" -", StringComparison.Ordinal);
}
