using System.Text.Json.Nodes;

internal static class OpenCliOptionCollisionIndexSupport
{
    public static OpenCliOptionCollisionEntry CreateEntry(JsonObject option)
        => new((JsonObject)option.DeepClone(), OpenCliOptionSupport.GetOptionTokens(option));

    public static IReadOnlyList<int> GetCandidateIndexes(
        IReadOnlySet<string> tokens,
        IReadOnlyDictionary<string, SortedSet<int>> candidateIndexesByToken)
    {
        var candidateIndexes = new SortedSet<int>();
        foreach (var token in tokens)
        {
            if (candidateIndexesByToken.TryGetValue(token, out var indexes))
            {
                candidateIndexes.UnionWith(indexes);
            }
        }

        return candidateIndexes.ToList();
    }

    public static void RegisterEntry(
        IDictionary<string, SortedSet<int>> candidateIndexesByToken,
        OpenCliOptionCollisionEntry entry,
        int index)
    {
        foreach (var token in entry.Tokens)
        {
            if (!candidateIndexesByToken.TryGetValue(token, out var indexes))
            {
                indexes = [];
                candidateIndexesByToken[token] = indexes;
            }

            indexes.Add(index);
        }
    }
}
