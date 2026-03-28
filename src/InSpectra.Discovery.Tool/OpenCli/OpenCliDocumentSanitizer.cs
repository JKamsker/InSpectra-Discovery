using System.Text.Json.Nodes;

internal static class OpenCliDocumentSanitizer
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
    private static readonly HashSet<string> EmptyOptionalArrayProperties = new(StringComparer.Ordinal)
    {
        "acceptedValues",
        "aliases",
        "arguments",
        "examples",
        "metadata",
        "options",
    };
    private static readonly HashSet<string> InformationalOptionDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Display this help screen.",
        "Display version information.",
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

    public static JsonObject Sanitize(JsonObject document)
    {
        SanitizeNode(document, arrayContext: null);
        return document;
    }

    public static JsonObject EnsureArtifactSource(JsonObject document, string artifactSource)
    {
        if (string.IsNullOrWhiteSpace(artifactSource))
        {
            return document;
        }

        var inspectra = document["x-inspectra"] as JsonObject;
        if (inspectra is null)
        {
            inspectra = new JsonObject();
            document["x-inspectra"] = inspectra;
        }

        if (string.IsNullOrWhiteSpace(inspectra["artifactSource"]?.GetValue<string>()))
        {
            inspectra["artifactSource"] = artifactSource;
        }

        return document;
    }

    private static void SanitizeNode(JsonNode node, string? arrayContext)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToArray())
            {
                if (string.Equals(arrayContext, "options", StringComparison.Ordinal)
                    && string.Equals(property.Key, "required", StringComparison.Ordinal))
                {
                    obj.Remove(property.Key);
                    continue;
                }

                if (property.Value is null)
                {
                    obj.Remove(property.Key);
                    continue;
                }

                var childArrayContext = property.Value is JsonArray ? property.Key : arrayContext;
                SanitizeNode(property.Value, childArrayContext);

                if (ShouldRemoveProperty(property.Key, property.Value))
                {
                    obj.Remove(property.Key);
                }
            }

            if (obj["options"] is JsonArray options)
            {
                DeduplicateSafeOptionCollisions(options);
                if (ShouldRemoveProperty("options", options))
                {
                    obj.Remove("options");
                }
            }

            return;
        }

        if (node is not JsonArray array)
        {
            return;
        }

        for (var index = array.Count - 1; index >= 0; index--)
        {
            if (array[index] is null)
            {
                array.RemoveAt(index);
                continue;
            }

            SanitizeNode(array[index]!, arrayContext);
        }
    }

    private static bool ShouldRemoveProperty(string propertyName, JsonNode value)
    {
        if (value is JsonArray array)
        {
            return array.Count == 0 && EmptyOptionalArrayProperties.Contains(propertyName);
        }

        if (value is JsonObject obj)
        {
            return obj.Count == 0 && string.Equals(propertyName, "x-inspectra", StringComparison.Ordinal);
        }

        if (value is not JsonValue jsonValue
            || !string.Equals(propertyName, "description", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            return string.IsNullOrWhiteSpace(jsonValue.GetValue<string>());
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void DeduplicateSafeOptionCollisions(JsonArray options)
    {
        var deduplicated = new List<JsonObject>();
        foreach (var option in options.OfType<JsonObject>())
        {
            var merged = false;
            for (var index = 0; index < deduplicated.Count; index++)
            {
                if (!TryMergeSafeOptionCollision(deduplicated[index], option, out var resolved))
                {
                    continue;
                }

                deduplicated[index] = resolved;
                merged = true;
                break;
            }

            if (!merged)
            {
                deduplicated.Add((JsonObject)option.DeepClone());
            }
        }

        options.Clear();
        foreach (var option in deduplicated)
        {
            options.Add(option);
        }
    }

    private static bool TryMergeSafeOptionCollision(JsonObject left, JsonObject right, out JsonObject resolved)
    {
        resolved = left;
        var leftTokens = GetOptionTokens(left);
        var rightTokens = GetOptionTokens(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0 || !leftTokens.Overlaps(rightTokens))
        {
            return false;
        }

        if (TryResolveStandaloneAliasCollision(left, right, out resolved))
        {
            return true;
        }

        var leftName = left["name"]?.GetValue<string>();
        var rightName = right["name"]?.GetValue<string>();
        if (!string.Equals(leftName, rightName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var leftDescription = NormalizeDescription(left["description"]?.GetValue<string>());
        var rightDescription = NormalizeDescription(right["description"]?.GetValue<string>());
        var leftInformational = IsInformationalOptionDescription(leftDescription);
        var rightInformational = IsInformationalOptionDescription(rightDescription);
        if (!AreCompatibleDescriptions(leftDescription, rightDescription, leftInformational, rightInformational))
        {
            return false;
        }

        var preferred = ChoosePreferredOption(left, right, leftInformational, rightInformational);
        var other = ReferenceEquals(preferred, left) ? right : left;
        resolved = MergeOptions(preferred, other);
        return true;
    }

    private static bool TryResolveStandaloneAliasCollision(JsonObject left, JsonObject right, out JsonObject resolved)
    {
        resolved = left;
        if (TryResolveStandaloneAliasCollision(left, right))
        {
            resolved = MergeOptions(right, left);
            return true;
        }

        if (TryResolveStandaloneAliasCollision(right, left))
        {
            resolved = MergeOptions(left, right);
            return true;
        }

        return false;
    }

    private static bool TryResolveStandaloneAliasCollision(JsonObject standaloneCandidate, JsonObject richerCandidate)
    {
        var standaloneName = standaloneCandidate["name"]?.GetValue<string>();
        if (!IsStandaloneAliasOption(standaloneCandidate) || string.IsNullOrWhiteSpace(standaloneName))
        {
            return false;
        }

        return GetOptionTokens(richerCandidate).Contains(standaloneName);
    }

    private static JsonObject ChoosePreferredOption(
        JsonObject left,
        JsonObject right,
        bool leftInformational,
        bool rightInformational)
    {
        if (leftInformational != rightInformational)
        {
            return leftInformational ? right : left;
        }

        return ScoreOption(left) >= ScoreOption(right) ? left : right;
    }

    private static JsonObject MergeOptions(JsonObject preferred, JsonObject other)
    {
        var merged = (JsonObject)preferred.DeepClone();
        if (merged["aliases"] is not JsonArray aliases)
        {
            aliases = new JsonArray();
            merged["aliases"] = aliases;
        }

        var existingAliases = aliases
            .OfType<JsonValue>()
            .Select(value => value.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var primaryName = merged["name"]?.GetValue<string>();
        foreach (var token in GetOptionTokens(other))
        {
            if (string.IsNullOrWhiteSpace(token)
                || string.Equals(token, primaryName, StringComparison.OrdinalIgnoreCase)
                || existingAliases.Contains(token))
            {
                continue;
            }

            aliases.Add(token);
            existingAliases.Add(token);
        }

        if (merged["arguments"] is null && other["arguments"] is JsonArray arguments)
        {
            merged["arguments"] = arguments.DeepClone();
        }

        if (string.IsNullOrWhiteSpace(merged["description"]?.GetValue<string>())
            && !string.IsNullOrWhiteSpace(other["description"]?.GetValue<string>()))
        {
            merged["description"] = other["description"]!.DeepClone();
        }

        if (!string.IsNullOrWhiteSpace(merged["description"]?.GetValue<string>()))
        {
            merged["description"] = TrimTrailingDescriptionNoise(merged["description"]!.GetValue<string>());
        }

        if (aliases.Count == 0)
        {
            merged.Remove("aliases");
        }

        return merged;
    }

    private static int ScoreOption(JsonObject option)
    {
        var score = 0;
        var name = option["name"]?.GetValue<string>() ?? string.Empty;
        if (name.StartsWith("--", StringComparison.Ordinal) || name.StartsWith("/", StringComparison.Ordinal))
        {
            score += 2;
        }

        if (option["aliases"] is JsonArray aliases)
        {
            score += aliases.Count;
        }

        if (option["arguments"] is JsonArray arguments && arguments.Count > 0)
        {
            score += 2;
        }

        var description = NormalizeDescription(option["description"]?.GetValue<string>());
        if (!string.IsNullOrWhiteSpace(description) && !IsInformationalOptionDescription(description))
        {
            score += 2;
        }

        return score;
    }

    private static HashSet<string> GetOptionTokens(JsonObject option)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var name = option["name"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(name))
        {
            tokens.Add(name);
        }

        if (option["aliases"] is not JsonArray aliases)
        {
            return tokens;
        }

        foreach (var alias in aliases.OfType<JsonValue>())
        {
            var token = alias.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(token))
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }

    private static bool IsStandaloneAliasOption(JsonObject option)
    {
        var name = option["name"]?.GetValue<string>();
        return !string.IsNullOrWhiteSpace(name)
            && name.StartsWith("-", StringComparison.Ordinal)
            && !name.StartsWith("--", StringComparison.Ordinal)
            && option["aliases"] is not JsonArray;
    }

    private static string NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return string.Empty;
        }

        return string.Join(" ", GetDescriptionLinesWithoutTrailingNoise(description));
    }

    private static bool IsInformationalOptionDescription(string description)
        => InformationalOptionDescriptions.Contains(description);

    private static bool AreCompatibleDescriptions(
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

    private static string TrimTrailingDescriptionNoise(string description)
        => string.Join("\n", GetDescriptionLinesWithoutTrailingNoise(description));

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
