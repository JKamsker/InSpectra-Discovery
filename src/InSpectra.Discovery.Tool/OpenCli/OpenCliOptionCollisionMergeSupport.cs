namespace InSpectra.Discovery.Tool.OpenCli;

using System.Text.Json.Nodes;

internal static class OpenCliOptionCollisionMergeSupport
{
    public static bool TryMergeSafeOptionCollision(
        OpenCliOptionCollisionEntry leftEntry,
        JsonObject rightOption,
        IReadOnlySet<string> rightTokens,
        out OpenCliOptionCollisionEntry resolvedEntry)
    {
        resolvedEntry = leftEntry;
        if (leftEntry.Tokens.Count == 0 || rightTokens.Count == 0 || !leftEntry.Tokens.Overlaps(rightTokens))
        {
            return false;
        }

        if (TryResolveStandaloneAliasCollision(leftEntry.Option, rightOption, rightTokens, out resolvedEntry))
        {
            return true;
        }

        var leftName = leftEntry.Option["name"]?.GetValue<string>();
        var rightName = rightOption["name"]?.GetValue<string>();
        if (!string.Equals(leftName, rightName, StringComparison.Ordinal))
        {
            return false;
        }

        var leftDescription = OpenCliOptionDescriptionSupport.NormalizeDescription(leftEntry.Option["description"]?.GetValue<string>());
        var rightDescription = OpenCliOptionDescriptionSupport.NormalizeDescription(rightOption["description"]?.GetValue<string>());
        var leftInformational = OpenCliOptionDescriptionSupport.IsInformationalOptionDescription(leftDescription);
        var rightInformational = OpenCliOptionDescriptionSupport.IsInformationalOptionDescription(rightDescription);
        if (TryResolveInformationalSelfArgumentDuplicate(leftEntry.Option, rightOption, leftInformational, rightInformational, out resolvedEntry))
        {
            return true;
        }

        if (leftInformational
            && rightInformational
            && !OpenCliOptionDescriptionSupport.HaveEquivalentInformationalTokenSets(leftEntry.Tokens, rightTokens))
        {
            return false;
        }

        if ((leftInformational ^ rightInformational)
            && (OpenCliOptionSupport.HasArguments(leftEntry.Option) || OpenCliOptionSupport.HasArguments(rightOption)))
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

        var preferred = ChoosePreferredOption(leftEntry.Option, rightOption, leftInformational, rightInformational);
        var other = ReferenceEquals(preferred, leftEntry.Option) ? rightOption : leftEntry.Option;
        var merged = OpenCliOptionSupport.MergeOptions(preferred, other);
        resolvedEntry = new OpenCliOptionCollisionEntry(merged, OpenCliOptionSupport.GetOptionTokens(merged));
        return true;
    }

    private static bool TryResolveStandaloneAliasCollision(
        JsonObject leftOption,
        JsonObject rightOption,
        IReadOnlySet<string> rightTokens,
        out OpenCliOptionCollisionEntry resolvedEntry)
    {
        resolvedEntry = new OpenCliOptionCollisionEntry(leftOption, OpenCliOptionSupport.GetOptionTokens(leftOption));
        if (TryResolveStandaloneAliasCollision(leftOption, rightTokens))
        {
            var merged = OpenCliOptionSupport.MergeOptions(rightOption, leftOption);
            resolvedEntry = new OpenCliOptionCollisionEntry(merged, OpenCliOptionSupport.GetOptionTokens(merged));
            return true;
        }

        var leftTokens = OpenCliOptionSupport.GetOptionTokens(leftOption);
        if (TryResolveStandaloneAliasCollision(rightOption, leftTokens))
        {
            var merged = OpenCliOptionSupport.MergeOptions(leftOption, rightOption);
            resolvedEntry = new OpenCliOptionCollisionEntry(merged, OpenCliOptionSupport.GetOptionTokens(merged));
            return true;
        }

        return false;
    }

    private static bool TryResolveStandaloneAliasCollision(JsonObject standaloneCandidate, IReadOnlySet<string> richerTokens)
    {
        var standaloneName = standaloneCandidate["name"]?.GetValue<string>();
        if (!OpenCliOptionSupport.IsStandaloneAliasOption(standaloneCandidate)
            || string.IsNullOrWhiteSpace(standaloneName))
        {
            return false;
        }

        return richerTokens.Contains(standaloneName);
    }

    private static bool TryResolveInformationalSelfArgumentDuplicate(
        JsonObject leftOption,
        JsonObject rightOption,
        bool leftInformational,
        bool rightInformational,
        out OpenCliOptionCollisionEntry resolvedEntry)
    {
        resolvedEntry = new OpenCliOptionCollisionEntry(leftOption, OpenCliOptionSupport.GetOptionTokens(leftOption));
        if (TryResolveInformationalSelfArgumentDuplicateCore(leftOption, rightOption, leftInformational, out var merged))
        {
            resolvedEntry = new OpenCliOptionCollisionEntry(merged, OpenCliOptionSupport.GetOptionTokens(merged));
            return true;
        }

        if (TryResolveInformationalSelfArgumentDuplicateCore(rightOption, leftOption, rightInformational, out merged))
        {
            resolvedEntry = new OpenCliOptionCollisionEntry(merged, OpenCliOptionSupport.GetOptionTokens(merged));
            return true;
        }

        return false;
    }

    private static bool TryResolveInformationalSelfArgumentDuplicateCore(
        JsonObject informationalCandidate,
        JsonObject selfArgumentCandidate,
        bool isInformationalCandidate,
        out JsonObject resolved)
    {
        resolved = informationalCandidate;
        if (!isInformationalCandidate
            || OpenCliOptionSupport.HasArguments(informationalCandidate)
            || !LooksLikeSyntheticSelfArgumentOption(selfArgumentCandidate))
        {
            return false;
        }

        resolved = OpenCliOptionSupport.MergeOptions(informationalCandidate, selfArgumentCandidate);
        resolved.Remove("arguments");
        return true;
    }

    private static JsonObject ChoosePreferredOption(
        JsonObject leftOption,
        JsonObject rightOption,
        bool leftInformational,
        bool rightInformational)
    {
        if (leftInformational != rightInformational)
        {
            return leftInformational ? rightOption : leftOption;
        }

        return ScoreOption(leftOption) >= ScoreOption(rightOption) ? leftOption : rightOption;
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

