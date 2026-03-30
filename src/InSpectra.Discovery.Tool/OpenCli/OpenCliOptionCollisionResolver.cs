using System.Text.Json.Nodes;

internal static class OpenCliOptionCollisionResolver
{
    public static void DeduplicateSafeOptionCollisions(JsonArray options)
    {
        var deduplicated = new List<JsonObject>();
        var candidateIndexesByToken = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);

        foreach (var option in options.OfType<JsonObject>())
        {
            var merged = false;
            var candidateIndexes = GetCandidateIndexes(option, candidateIndexesByToken);
            foreach (var index in candidateIndexes)
            {
                if (!TryMergeSafeOptionCollision(deduplicated[index], option, out var resolved))
                {
                    continue;
                }

                deduplicated[index] = resolved;
                RegisterOptionTokens(candidateIndexesByToken, resolved, index);
                merged = true;
                break;
            }

            if (merged)
            {
                continue;
            }

            var deduplicatedOption = (JsonObject)option.DeepClone();
            var deduplicatedIndex = deduplicated.Count;
            deduplicated.Add(deduplicatedOption);
            RegisterOptionTokens(candidateIndexesByToken, deduplicatedOption, deduplicatedIndex);
        }

        options.Clear();
        foreach (var option in deduplicated)
        {
            options.Add(option);
        }
    }

    private static IReadOnlyList<int> GetCandidateIndexes(
        JsonObject option,
        IReadOnlyDictionary<string, SortedSet<int>> candidateIndexesByToken)
    {
        var candidateIndexes = new SortedSet<int>();
        foreach (var token in OpenCliOptionSupport.GetOptionTokens(option))
        {
            if (candidateIndexesByToken.TryGetValue(token, out var indexes))
            {
                candidateIndexes.UnionWith(indexes);
            }
        }

        return candidateIndexes.ToList();
    }

    private static void RegisterOptionTokens(
        IDictionary<string, SortedSet<int>> candidateIndexesByToken,
        JsonObject option,
        int index)
    {
        foreach (var token in OpenCliOptionSupport.GetOptionTokens(option))
        {
            if (!candidateIndexesByToken.TryGetValue(token, out var indexes))
            {
                indexes = [];
                candidateIndexesByToken[token] = indexes;
            }

            indexes.Add(index);
        }
    }

    private static bool TryMergeSafeOptionCollision(JsonObject left, JsonObject right, out JsonObject resolved)
    {
        resolved = left;
        var leftTokens = OpenCliOptionSupport.GetOptionTokens(left);
        var rightTokens = OpenCliOptionSupport.GetOptionTokens(right);
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
        if (!string.Equals(leftName, rightName, StringComparison.Ordinal))
        {
            return false;
        }

        var leftDescription = OpenCliOptionDescriptionSupport.NormalizeDescription(left["description"]?.GetValue<string>());
        var rightDescription = OpenCliOptionDescriptionSupport.NormalizeDescription(right["description"]?.GetValue<string>());
        var leftInformational = OpenCliOptionDescriptionSupport.IsInformationalOptionDescription(leftDescription);
        var rightInformational = OpenCliOptionDescriptionSupport.IsInformationalOptionDescription(rightDescription);
        if (TryResolveInformationalSelfArgumentDuplicate(left, right, out resolved))
        {
            return true;
        }

        if (leftInformational
            && rightInformational
            && !OpenCliOptionDescriptionSupport.HaveEquivalentInformationalTokenSets(leftTokens, rightTokens))
        {
            return false;
        }

        if ((leftInformational ^ rightInformational)
            && (OpenCliOptionSupport.HasArguments(left) || OpenCliOptionSupport.HasArguments(right)))
        {
            return false;
        }

        if (!OpenCliOptionDescriptionSupport.AreCompatibleDescriptions(
                leftDescription,
                rightDescription,
                leftInformational,
                rightInformational))
        {
            return false;
        }

        var preferred = ChoosePreferredOption(left, right, leftInformational, rightInformational);
        var other = ReferenceEquals(preferred, left) ? right : left;
        resolved = OpenCliOptionSupport.MergeOptions(preferred, other);
        return true;
    }

    private static bool TryResolveStandaloneAliasCollision(JsonObject left, JsonObject right, out JsonObject resolved)
    {
        resolved = left;
        if (TryResolveStandaloneAliasCollision(left, right))
        {
            resolved = OpenCliOptionSupport.MergeOptions(right, left);
            return true;
        }

        if (TryResolveStandaloneAliasCollision(right, left))
        {
            resolved = OpenCliOptionSupport.MergeOptions(left, right);
            return true;
        }

        return false;
    }

    private static bool TryResolveStandaloneAliasCollision(JsonObject standaloneCandidate, JsonObject richerCandidate)
    {
        var standaloneName = standaloneCandidate["name"]?.GetValue<string>();
        if (!OpenCliOptionSupport.IsStandaloneAliasOption(standaloneCandidate)
            || string.IsNullOrWhiteSpace(standaloneName))
        {
            return false;
        }

        return OpenCliOptionSupport.GetOptionTokens(richerCandidate).Contains(standaloneName);
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

        var description = OpenCliOptionDescriptionSupport.NormalizeDescription(option["description"]?.GetValue<string>());
        if (!string.IsNullOrWhiteSpace(description)
            && !OpenCliOptionDescriptionSupport.IsInformationalOptionDescription(description))
        {
            score += 2;
            score += Math.Min(6, description.Length / 24);
        }

        return score;
    }

    private static bool TryResolveInformationalSelfArgumentDuplicate(
        JsonObject left,
        JsonObject right,
        out JsonObject resolved)
    {
        resolved = left;
        if (TryResolveInformationalSelfArgumentDuplicateCore(left, right, out var informationalPreferred))
        {
            resolved = informationalPreferred;
            return true;
        }

        if (TryResolveInformationalSelfArgumentDuplicateCore(right, left, out informationalPreferred))
        {
            resolved = informationalPreferred;
            return true;
        }

        return false;
    }

    private static bool TryResolveInformationalSelfArgumentDuplicateCore(
        JsonObject informationalCandidate,
        JsonObject selfArgumentCandidate,
        out JsonObject resolved)
    {
        resolved = informationalCandidate;
        var description = OpenCliOptionDescriptionSupport.NormalizeDescription(informationalCandidate["description"]?.GetValue<string>());
        if (!OpenCliOptionDescriptionSupport.IsInformationalOptionDescription(description)
            || OpenCliOptionSupport.HasArguments(informationalCandidate)
            || !LooksLikeSyntheticSelfArgumentOption(selfArgumentCandidate))
        {
            return false;
        }

        resolved = OpenCliOptionSupport.MergeOptions(informationalCandidate, selfArgumentCandidate);
        resolved.Remove("arguments");
        return true;
    }

    private static bool LooksLikeSyntheticSelfArgumentOption(JsonObject option)
    {
        if (option["arguments"] is not JsonArray arguments || arguments.Count != 1)
        {
            return false;
        }

        var normalizedDescription = OpenCliOptionDescriptionSupport.NormalizeDescription(option["description"]?.GetValue<string>());
        if (!string.IsNullOrWhiteSpace(normalizedDescription)
            && !OpenCliOptionDescriptionSupport.IsInformationalOptionDescription(normalizedDescription))
        {
            return false;
        }

        var argument = arguments[0] as JsonObject;
        var argumentName = argument?["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(argumentName))
        {
            return false;
        }

        if (argument?["required"]?.GetValue<bool>() == true)
        {
            return false;
        }

        return string.Equals(
            argumentName,
            OpenCliOptionSupport.DeriveSyntheticArgumentName(option["name"]?.GetValue<string>()),
            StringComparison.Ordinal);
    }
}
