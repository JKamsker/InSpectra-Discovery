namespace InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.Signatures;

internal static class UsagePrototypeSupport
{
    public static IReadOnlyList<UsagePrototype> ExtractLeafCommandPrototypes(
        string rootCommandName,
        string commandPath,
        IReadOnlyList<string> usageLines)
    {
        var rootSegments = SplitSegments(rootCommandName);
        var commandSegments = SplitSegments(commandPath);
        var pathSegments = rootSegments.Concat(commandSegments).ToArray();
        if (pathSegments.Length == 0)
        {
            return [];
        }

        var results = new List<UsagePrototype>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var usageLine in usageLines)
        {
            if (!TrySplitPrototypeAndDescription(usageLine, out var prototype, out var description))
            {
                continue;
            }

            var tokens = TokenizeUsageLine(prototype);
            var pathStart = FindTokenSequence(tokens, pathSegments);
            if (pathStart < 0)
            {
                continue;
            }

            var nextTokenIndex = pathStart + pathSegments.Length;
            if (nextTokenIndex < tokens.Length && LooksLikeLiteralCommandToken(tokens[nextTokenIndex]))
            {
                continue;
            }

            var normalizedPrototype = prototype.Trim();
            if (normalizedPrototype.Length == 0 || !seen.Add(normalizedPrototype))
            {
                continue;
            }

            results.Add(new UsagePrototype(normalizedPrototype, NormalizeDescription(description)));
        }

        return results;
    }

    private static string[] SplitSegments(string commandKey)
        => string.IsNullOrWhiteSpace(commandKey)
            ? []
            : commandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string[] TokenizeUsageLine(string line)
        => StripLinePrefix(line).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim().Trim(',', ';'))
            .Where(token => token.Length > 0)
            .ToArray();

    private static string StripLinePrefix(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.StartsWith("OR:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[3..].TrimStart();
        }

        if (trimmed.StartsWith("OR ", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[2..].TrimStart();
        }

        return trimmed;
    }

    private static bool TrySplitPrototypeAndDescription(string usageLine, out string prototype, out string? description)
    {
        prototype = StripLinePrefix(usageLine);
        description = null;
        if (prototype.Length == 0)
        {
            return false;
        }

        var gapIndex = prototype.IndexOf("  ", StringComparison.Ordinal);
        if (gapIndex < 0)
        {
            return true;
        }

        var separatorLength = 2;
        while (gapIndex + separatorLength < prototype.Length
            && prototype[gapIndex + separatorLength] == ' ')
        {
            separatorLength++;
        }

        var prototypeCandidate = prototype[..gapIndex].TrimEnd();
        var descriptionCandidate = prototype[(gapIndex + separatorLength)..].Trim();
        if (prototypeCandidate.Length == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(descriptionCandidate))
        {
            prototype = prototypeCandidate;
            description = descriptionCandidate;
        }

        return true;
    }

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

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description)
            ? null
            : description.Trim();

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

internal sealed record UsagePrototype(
    string Prototype,
    string? Description);
