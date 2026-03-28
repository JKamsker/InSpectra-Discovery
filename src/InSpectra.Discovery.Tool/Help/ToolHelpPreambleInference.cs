internal static class ToolHelpPreambleInference
{
    public static IReadOnlyList<string> InferUsageLines(IReadOnlyList<string> preamble)
        => preamble
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Usage -", StringComparison.OrdinalIgnoreCase))
            .Select(line => line["Usage".Length..].TrimStart(' ', ':', '-').Trim())
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
}
