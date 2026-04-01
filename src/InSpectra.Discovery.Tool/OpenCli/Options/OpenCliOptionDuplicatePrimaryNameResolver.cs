namespace InSpectra.Discovery.Tool.OpenCli.Options;

using System.Text;
using System.Text.Json.Nodes;

internal static class OpenCliOptionDuplicatePrimaryNameResolver
{
    public static void Resolve(JsonArray options)
    {
        var unavailableTokens = options
            .OfType<JsonObject>()
            .SelectMany(OpenCliOptionSupport.GetOptionTokens)
            .Where(static token => !string.IsNullOrWhiteSpace(token))
            .ToHashSet(StringComparer.Ordinal);
        var seenPrimaryNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var option in options.OfType<JsonObject>())
        {
            var primaryName = option["name"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(primaryName) || seenPrimaryNames.Add(primaryName))
            {
                continue;
            }

            if (TryPromoteDerivedLongName(option, primaryName, unavailableTokens, out var resolvedPrimaryName)
                || TryPromoteUniqueAlias(option, unavailableTokens, out resolvedPrimaryName))
            {
                seenPrimaryNames.Add(resolvedPrimaryName);
                unavailableTokens.Add(resolvedPrimaryName);
            }
        }
    }

    private static bool TryPromoteDerivedLongName(
        JsonObject option,
        string duplicatePrimaryName,
        IReadOnlySet<string> unavailableTokens,
        out string resolvedPrimaryName)
    {
        resolvedPrimaryName = duplicatePrimaryName;
        if (!duplicatePrimaryName.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        var duplicateSemanticName = duplicatePrimaryName[2..];
        var derivedPrimaryName = BuildDerivedLongName(option);
        if (string.IsNullOrWhiteSpace(derivedPrimaryName)
            || string.Equals(derivedPrimaryName, duplicatePrimaryName, StringComparison.Ordinal)
            || !derivedPrimaryName.StartsWith($"--{duplicateSemanticName}-", StringComparison.Ordinal)
            || unavailableTokens.Contains(derivedPrimaryName))
        {
            return false;
        }

        option["name"] = derivedPrimaryName;
        resolvedPrimaryName = derivedPrimaryName;
        return true;
    }

    private static bool TryPromoteUniqueAlias(
        JsonObject option,
        IReadOnlySet<string> unavailableTokens,
        out string resolvedPrimaryName)
    {
        resolvedPrimaryName = option["name"]?.GetValue<string>()?.Trim() ?? string.Empty;
        if (option["aliases"] is not JsonArray aliases)
        {
            return false;
        }

        var candidateAlias = aliases
            .OfType<JsonValue>()
            .Select(alias => alias.GetValue<string>()?.Trim())
            .Where(alias => !string.IsNullOrWhiteSpace(alias) && !unavailableTokens.Contains(alias))
            .OrderByDescending(static alias => alias!.StartsWith("--", StringComparison.Ordinal))
            .ThenBy(static alias => alias!.Length)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(candidateAlias))
        {
            return false;
        }

        option["name"] = candidateAlias;

        var remainingAliases = new JsonArray();
        foreach (var aliasNode in aliases.OfType<JsonValue>())
        {
            var alias = aliasNode.GetValue<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(alias)
                || string.Equals(alias, candidateAlias, StringComparison.Ordinal))
            {
                continue;
            }

            remainingAliases.Add(alias);
        }

        if (remainingAliases.Count == 0)
        {
            option.Remove("aliases");
        }
        else
        {
            option["aliases"] = remainingAliases;
        }

        resolvedPrimaryName = candidateAlias;
        return true;
    }

    private static string? BuildDerivedLongName(JsonObject option)
    {
        if (option["arguments"] is not JsonArray arguments || arguments.Count == 0)
        {
            return null;
        }

        var argumentName = arguments[0]?["name"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(argumentName))
        {
            return null;
        }

        var builder = new StringBuilder();
        var previousWasSeparator = false;
        foreach (var character in argumentName)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        var normalized = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : $"--{normalized}";
    }
}
