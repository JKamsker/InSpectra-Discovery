using System.Text.Json.Nodes;

internal static class OpenCliOptionDescriptionSupport
{
    private static readonly char[] DescriptionKeywordSeparators =
    [
        ' ',
        '\t',
        '\r',
        '\n',
        ',',
        '.',
        ':',
        ';',
        '!',
        '?',
        '(',
        ')',
        '[',
        ']',
        '{',
        '}',
        '<',
        '>',
        '/',
        '\\',
        '|',
        '-',
        '"',
        '\'',
        '`',
    ];

    private static readonly HashSet<string> InformationalOptionDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Display this help screen.",
        "Display version information.",
        "Show version information.",
        "Show help information.",
        "Show help and usage information",
    };

    private static readonly HashSet<string> DescriptionStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a",
        "an",
        "and",
        "as",
        "be",
        "for",
        "given",
        "in",
        "is",
        "of",
        "on",
        "or",
        "the",
        "this",
        "to",
        "with",
    };

    public static void NormalizeOptionObject(JsonObject option)
    {
        var description = option["description"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        var trimmedDescription = TrimTrailingDescriptionNoise(description);
        if (TryGetInformationalDescriptionPrefix(trimmedDescription, out var normalizedDescription))
        {
            option["description"] = normalizedDescription;
            return;
        }

        option["description"] = trimmedDescription;
    }

    public static string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        return string.Join(" ", GetDescriptionLinesWithoutTrailingNoise(description));
    }

    public static bool IsInformationalOptionDescription(string description)
        => InformationalOptionDescriptions.Contains(description);

    public static bool HaveEquivalentInformationalTokenSets(
        IReadOnlySet<string> leftTokens,
        IReadOnlySet<string> rightTokens)
        => leftTokens.SetEquals(rightTokens);

    public static bool AreCompatibleDescriptions(
        string leftDescription,
        string rightDescription,
        bool leftInformational,
        bool rightInformational)
    {
        if (string.Equals(leftDescription, rightDescription, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(leftDescription) || string.IsNullOrWhiteSpace(rightDescription))
        {
            return true;
        }

        if (leftInformational ^ rightInformational)
        {
            return true;
        }

        return HaveNearEquivalentKeywords(leftDescription, rightDescription);
    }

    public static string TrimTrailingDescriptionNoise(string description)
        => string.Join("\n", GetDescriptionLinesWithoutTrailingNoise(description));

    private static bool TryGetInformationalDescriptionPrefix(string description, out string normalizedDescription)
    {
        foreach (var informationalDescription in InformationalOptionDescriptions)
        {
            if (!description.StartsWith(informationalDescription, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            normalizedDescription = informationalDescription;
            return true;
        }

        normalizedDescription = string.Empty;
        return false;
    }

    private static bool HaveNearEquivalentKeywords(string leftDescription, string rightDescription)
    {
        var leftKeywords = GetDescriptionKeywords(leftDescription);
        var rightKeywords = GetDescriptionKeywords(rightDescription);
        if (leftKeywords.Count < 2 || rightKeywords.Count < 2)
        {
            return false;
        }

        return leftKeywords.SetEquals(rightKeywords)
            || leftKeywords.IsSubsetOf(rightKeywords)
            || rightKeywords.IsSubsetOf(leftKeywords);
    }

    private static HashSet<string> GetDescriptionKeywords(string description)
        => description
            .Split(DescriptionKeywordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Any(char.IsLetterOrDigit))
            .Where(token => !DescriptionStopWords.Contains(token))
            .Select(token => token.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static bool LooksLikeTrailingDescriptionNoise(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        var trimmed = line.Trim();
        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 3
            && parts[0].Length > 0
            && parts[0].All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.' or '-')
            && string.Equals(parts[1], "pos.", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(parts[2], out _);
    }

    private static IReadOnlyList<string> GetDescriptionLinesWithoutTrailingNoise(string description)
    {
        var lines = description
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        while (lines.Count > 0 && LooksLikeTrailingDescriptionNoise(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines;
    }
}
