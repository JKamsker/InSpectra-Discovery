namespace InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.Signatures;

internal static class UsageCommandInferenceSupport
{
    public static IReadOnlyList<string> InferChildCommands(
        string rootCommandName,
        IReadOnlyList<string> commandSegments,
        IReadOnlyList<string> usageLines)
    {
        var inferredCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootSegments = SplitSegments(rootCommandName);

        foreach (var usageLine in usageLines)
        {
            var tokens = TokenizeUsageLine(usageLine);
            if (tokens.Length == 0)
            {
                continue;
            }

            var rootStart = FindTokenSequence(tokens, rootSegments);
            if (rootStart < 0)
            {
                continue;
            }

            var candidateStart = rootStart + rootSegments.Length;
            if (!StartsWith(tokens, candidateStart, commandSegments))
            {
                continue;
            }

            var nextTokenIndex = candidateStart + commandSegments.Count;
            if (nextTokenIndex >= tokens.Length || !LooksLikeLiteralCommandToken(tokens[nextTokenIndex]))
            {
                continue;
            }

            var childKey = commandSegments.Count == 0
                ? SignatureNormalizer.NormalizeCommandKey(tokens[nextTokenIndex])
                : $"{string.Join(' ', commandSegments)} {SignatureNormalizer.NormalizeCommandKey(tokens[nextTokenIndex])}";
            if (!string.IsNullOrWhiteSpace(childKey))
            {
                inferredCommands.Add(childKey);
            }
        }

        return inferredCommands.ToArray();
    }

    private static string[] SplitSegments(string commandKey)
        => string.IsNullOrWhiteSpace(commandKey)
            ? []
            : commandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string[] TokenizeUsageLine(string line)
        => line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim().Trim(',', ';'))
            .Where(token => token.Length > 0)
            .ToArray();

    private static int FindTokenSequence(IReadOnlyList<string> tokens, IReadOnlyList<string> sequence)
    {
        if (sequence.Count == 0 || tokens.Count < sequence.Count)
        {
            return -1;
        }

        for (var start = 0; start <= tokens.Count - sequence.Count; start++)
        {
            if (StartsWith(tokens, start, sequence))
            {
                return start;
            }
        }

        return -1;
    }

    private static bool StartsWith(IReadOnlyList<string> tokens, int start, IReadOnlyList<string> sequence)
    {
        if (start < 0 || start + sequence.Count > tokens.Count)
        {
            return false;
        }

        for (var index = 0; index < sequence.Count; index++)
        {
            if (!string.Equals(tokens[start + index], sequence[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static bool LooksLikeLiteralCommandToken(string token)
    {
        var trimmed = token.Trim().TrimEnd(':');
        if (trimmed.Length == 0
            || trimmed.StartsWith("<", StringComparison.Ordinal)
            || trimmed.StartsWith("[", StringComparison.Ordinal)
            || trimmed.StartsWith("(", StringComparison.Ordinal)
            || trimmed.StartsWith("-", StringComparison.Ordinal)
            || trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return false;
        }

        var normalized = SignatureNormalizer.NormalizeCommandKey(trimmed);
        return !string.IsNullOrWhiteSpace(normalized)
            && string.Equals(normalized, trimmed, StringComparison.OrdinalIgnoreCase);
    }
}
