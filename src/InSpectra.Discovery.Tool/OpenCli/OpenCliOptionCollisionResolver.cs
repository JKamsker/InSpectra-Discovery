using System.Text.Json.Nodes;

internal static class OpenCliOptionCollisionResolver
{
    public static void DeduplicateSafeOptionCollisions(JsonArray options)
    {
        var deduplicated = new List<OpenCliOptionCollisionEntry>();
        var candidateIndexesByToken = new Dictionary<string, SortedSet<int>>(StringComparer.Ordinal);

        foreach (var option in options.OfType<JsonObject>())
        {
            var optionTokens = OpenCliOptionSupport.GetOptionTokens(option);
            var merged = false;
            var candidateIndexes = OpenCliOptionCollisionIndexSupport.GetCandidateIndexes(optionTokens, candidateIndexesByToken);
            foreach (var index in candidateIndexes)
            {
                if (!OpenCliOptionCollisionMergeSupport.TryMergeSafeOptionCollision(deduplicated[index], option, optionTokens, out var resolved))
                {
                    continue;
                }

                deduplicated[index] = resolved;
                OpenCliOptionCollisionIndexSupport.RegisterEntry(candidateIndexesByToken, resolved, index);
                merged = true;
                break;
            }

            if (merged)
            {
                continue;
            }

            var deduplicatedOption = OpenCliOptionCollisionIndexSupport.CreateEntry(option);
            var deduplicatedIndex = deduplicated.Count;
            deduplicated.Add(deduplicatedOption);
            OpenCliOptionCollisionIndexSupport.RegisterEntry(candidateIndexesByToken, deduplicatedOption, deduplicatedIndex);
        }

        options.Clear();
        foreach (var option in deduplicated)
        {
            options.Add(option.Option);
        }
    }
}
