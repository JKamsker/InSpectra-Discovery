namespace InSpectra.Discovery.Tool.Help.Inference.Usage;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Signatures;

internal static class UsageCommandInferenceSupport
{
    public static bool LooksLikeCommandHub(
        string rootCommandName,
        IReadOnlyList<string> usageLines)
    {
        if (usageLines.Count == 0)
        {
            return false;
        }

        var rootSegments = SplitSegments(rootCommandName);
        foreach (var usageLine in usageLines)
        {
            var prototype = StripLinePrefix(usageLine);
            if (ContainsCommandPlaceholder(prototype))
            {
                return true;
            }

            var tokens = TokenizeUsageLine(prototype);
            if (tokens.Length == 0)
            {
                continue;
            }

            if (rootSegments.Length == 0)
            {
                continue;
            }

            var rootStart = FindTokenSequence(tokens, rootSegments);
            if (rootStart < 0)
            {
                continue;
            }

            var nextTokenIndex = rootStart + rootSegments.Length;
            if (nextTokenIndex >= tokens.Length)
            {
                continue;
            }

            if (LooksLikeCommandPlaceholder(tokens[nextTokenIndex]))
            {
                return true;
            }
        }

        return InferCommands(usageLines).Count > 0;
    }

    public static IReadOnlyList<Item> InferCommands(
        IReadOnlyList<string> usageLines)
    {
        var rootSegments = InferCommonRootSegments(usageLines);
        if (rootSegments.Length == 0)
        {
            return [];
        }

        var inferredCommands = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        foreach (var usageLine in usageLines)
        {
            if (!LooksLikeCommandPrototypeLine(usageLine))
            {
                continue;
            }

            if (!TrySplitPrototypeAndDescription(usageLine, out var prototype, out var description))
            {
                continue;
            }

            var tokens = TokenizeUsageLine(prototype);
            if (tokens.Length == 0)
            {
                continue;
            }

            var rootStart = FindTokenSequence(tokens, rootSegments);
            if (rootStart < 0)
            {
                continue;
            }

            var commandTokens = new List<string>();
            for (var index = rootStart + rootSegments.Length; index < tokens.Length; index++)
            {
                if (!LooksLikeLiteralCommandToken(tokens[index]))
                {
                    break;
                }

                commandTokens.Add(tokens[index]);
            }

            if (commandTokens.Count == 0)
            {
                continue;
            }

            var key = SignatureNormalizer.NormalizeCommandKey(string.Join(' ', commandTokens));
            if (string.IsNullOrWhiteSpace(key)
                || SignatureNormalizer.IsBuiltinAuxiliaryCommand(key))
            {
                continue;
            }

            var item = new Item(key, false, NormalizeDescription(description));
            if (!inferredCommands.TryGetValue(key, out var existing)
                || ShouldPrefer(item, existing))
            {
                inferredCommands[key] = item;
            }
        }

        return inferredCommands.Values.ToArray();
    }

    public static IReadOnlyList<string> InferChildCommands(
        string rootCommandName,
        IReadOnlyList<string> commandSegments,
        IReadOnlyList<string> usageLines)
    {
        var inferredCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootSegments = SplitSegments(rootCommandName);

        foreach (var usageLine in usageLines)
        {
            if (!LooksLikeCommandPrototypeLine(usageLine))
            {
                continue;
            }

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

    private static string[] InferCommonRootSegments(IReadOnlyList<string> usageLines)
    {
        var literalSequences = usageLines
            .Where(LooksLikeRootPrototypeLine)
            .Select(TryExtractLeadingLiteralTokens)
            .Where(tokens => tokens.Length > 0)
            .GroupBy(tokens => tokens[0], StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .FirstOrDefault(group => group.Count() >= 2)?
            .ToArray()
            ?? []
            ;
        if (literalSequences.Length < 2)
        {
            return [];
        }

        return ComputeCommonPrefix(literalSequences);
    }

    private static string[] ComputeCommonPrefix(string[][] literalSequences)
    {
        var commonLength = literalSequences.Min(tokens => tokens.Length);
        for (var index = 0; index < commonLength; index++)
        {
            var expected = literalSequences[0][index];
            if (literalSequences.Any(tokens => !string.Equals(tokens[index], expected, StringComparison.OrdinalIgnoreCase)))
            {
                return index == 0 ? [] : literalSequences[0][..index];
            }
        }

        return literalSequences[0][..commonLength];
    }

    private static string[] TokenizeUsageLine(string line)
        => StripLinePrefix(line).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim().Trim(',', ';'))
            .Where(token => token.Length > 0)
            .ToArray();

    private static bool LooksLikeCommandPrototypeLine(string usageLine)
    {
        if (!TrySplitPrototypeAndDescription(usageLine, out var prototype, out var description))
        {
            return false;
        }

        if (prototype.Length == 0)
        {
            return false;
        }

        if (ContainsUsageSyntaxMarker(prototype))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(description) || LooksLikeDecorativeDescription(description))
        {
            return false;
        }

        var tokens = TokenizeUsageLine(prototype);
        return tokens.Length is >= 2 and <= 4
            && tokens.All(LooksLikeLiteralCommandToken);
    }

    private static bool LooksLikeRootPrototypeLine(string usageLine)
    {
        if (!TrySplitPrototypeAndDescription(usageLine, out var prototype, out var description))
        {
            return false;
        }

        if (prototype.Length == 0)
        {
            return false;
        }

        if (ContainsUsageSyntaxMarker(prototype))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(description) || LooksLikeDecorativeDescription(description))
        {
            return false;
        }

        var tokens = TokenizeUsageLine(prototype);
        return tokens.Length >= 1
            && tokens.All(LooksLikeLiteralCommandToken);
    }

    private static bool ContainsUsageSyntaxMarker(string prototype)
        => prototype.Contains('[', StringComparison.Ordinal)
            || prototype.Contains('<', StringComparison.Ordinal)
            || prototype.Contains("--", StringComparison.Ordinal)
            || prototype.Contains("...", StringComparison.Ordinal)
            || prototype.Contains('|', StringComparison.Ordinal);

    private static bool ContainsCommandPlaceholder(string prototype)
    {
        var normalized = prototype.Trim();
        return normalized.Contains("[command]", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("<command>", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("[commands]", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("<commands>", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("[subcommand]", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("<subcommand>", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("[subcommands]", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("<subcommands>", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(" [cmd]", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(" <cmd>", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(" [verb]", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(" <verb>", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeCommandPlaceholder(string token)
    {
        var normalized = token.Trim().Trim(',', ';', ':');
        return string.Equals(normalized, "[command]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "<command>", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "[commands]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "<commands>", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "[subcommand]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "<subcommand>", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "[subcommands]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "<subcommands>", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "[cmd]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "<cmd>", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "[verb]", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "<verb>", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeDecorativeDescription(string description)
        => description.All(ch => !char.IsLetterOrDigit(ch));

    private static string[] TryExtractLeadingLiteralTokens(string usageLine)
    {
        if (!TrySplitPrototypeAndDescription(usageLine, out var prototype, out _))
        {
            return [];
        }

        return TokenizeUsageLine(prototype)
            .TakeWhile(LooksLikeLiteralCommandToken)
            .ToArray();
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

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static bool ShouldPrefer(Item candidate, Item existing)
    {
        var candidateDescriptionLength = candidate.Description?.Length ?? 0;
        var existingDescriptionLength = existing.Description?.Length ?? 0;
        return candidateDescriptionLength > existingDescriptionLength;
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
