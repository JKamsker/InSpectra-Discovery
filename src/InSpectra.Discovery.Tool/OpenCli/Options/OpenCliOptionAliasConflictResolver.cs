namespace InSpectra.Discovery.Tool.OpenCli.Options;

using System.Text.Json.Nodes;

internal static class OpenCliOptionAliasConflictResolver
{
    public static void RemoveConflictingAliases(JsonArray options)
    {
        var primaryNames = GetPrimaryNames(options);
        var seenTokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (var option in options.OfType<JsonObject>())
        {
            var primaryName = GetPrimaryName(option);
            if (!string.IsNullOrWhiteSpace(primaryName))
            {
                seenTokens.Add(primaryName);
            }

            if (option["aliases"] is not JsonArray aliases)
            {
                continue;
            }

            var keptAliases = new JsonArray();
            var localSeenAliases = new HashSet<string>(StringComparer.Ordinal);

            foreach (var aliasNode in aliases.OfType<JsonValue>())
            {
                var alias = aliasNode.GetValue<string>()?.Trim();
                if (string.IsNullOrWhiteSpace(alias)
                    || string.Equals(alias, primaryName, StringComparison.Ordinal)
                    || !localSeenAliases.Add(alias)
                    || IsReservedByAnotherPrimaryName(alias, primaryName, primaryNames)
                    || seenTokens.Contains(alias))
                {
                    continue;
                }

                keptAliases.Add(alias);
                seenTokens.Add(alias);
            }

            if (keptAliases.Count == 0)
            {
                option.Remove("aliases");
                continue;
            }

            option["aliases"] = keptAliases;
        }
    }

    private static HashSet<string> GetPrimaryNames(JsonArray options)
        => options
            .OfType<JsonObject>()
            .Select(GetPrimaryName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static string? GetPrimaryName(JsonObject option)
        => option["name"]?.GetValue<string>()?.Trim();

    private static bool IsReservedByAnotherPrimaryName(
        string alias,
        string? primaryName,
        IReadOnlySet<string> primaryNames)
        => primaryNames.Contains(alias)
            && !string.Equals(alias, primaryName, StringComparison.Ordinal);
}
