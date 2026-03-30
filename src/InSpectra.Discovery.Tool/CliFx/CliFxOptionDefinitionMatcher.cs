internal sealed class CliFxOptionDefinitionMatcher
{
    private readonly IReadOnlyList<CliFxOptionDefinition> _definitions;
    private readonly bool[] _matched;
    private readonly IReadOnlyDictionary<string, List<int>> _indexesByLongName;
    private readonly IReadOnlyDictionary<char, List<int>> _indexesByShortName;

    public CliFxOptionDefinitionMatcher(IReadOnlyList<CliFxOptionDefinition> definitions)
    {
        _definitions = definitions;
        _matched = new bool[definitions.Count];
        _indexesByLongName = BuildLongNameLookup(definitions);
        _indexesByShortName = BuildShortNameLookup(definitions);
    }

    public CliFxOptionDefinition? TakeMatch(string? longName, char? shortName)
    {
        if (TryTakeMatch(_indexesByLongName, longName, out var byLongName))
        {
            return byLongName;
        }

        return TryTakeMatch(_indexesByShortName, shortName, out var byShortName)
            ? byShortName
            : null;
    }

    public IReadOnlyList<CliFxOptionDefinition> GetRemainingDefinitions()
        => _definitions.Where((_, index) => !_matched[index]).ToArray();

    private bool TryTakeMatch<TLookupKey>(
        IReadOnlyDictionary<TLookupKey, List<int>> indexesByKey,
        TLookupKey? key,
        out CliFxOptionDefinition? definition)
        where TLookupKey : struct
    {
        definition = null;
        if (!key.HasValue || !indexesByKey.TryGetValue(key.Value, out var indexes))
        {
            return false;
        }

        foreach (var index in indexes)
        {
            if (_matched[index])
            {
                continue;
            }

            _matched[index] = true;
            definition = _definitions[index];
            return true;
        }

        return false;
    }

    private bool TryTakeMatch(
        IReadOnlyDictionary<string, List<int>> indexesByKey,
        string? key,
        out CliFxOptionDefinition? definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(key) || !indexesByKey.TryGetValue(key, out var indexes))
        {
            return false;
        }

        foreach (var index in indexes)
        {
            if (_matched[index])
            {
                continue;
            }

            _matched[index] = true;
            definition = _definitions[index];
            return true;
        }

        return false;
    }

    private static IReadOnlyDictionary<string, List<int>> BuildLongNameLookup(IReadOnlyList<CliFxOptionDefinition> definitions)
    {
        var lookup = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < definitions.Count; index++)
        {
            var name = definitions[index].Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!lookup.TryGetValue(name, out var indexes))
            {
                indexes = [];
                lookup[name] = indexes;
            }

            indexes.Add(index);
        }

        return lookup;
    }

    private static IReadOnlyDictionary<char, List<int>> BuildShortNameLookup(IReadOnlyList<CliFxOptionDefinition> definitions)
    {
        var lookup = new Dictionary<char, List<int>>();
        for (var index = 0; index < definitions.Count; index++)
        {
            var shortName = definitions[index].ShortName;
            if (shortName is null)
            {
                continue;
            }

            if (!lookup.TryGetValue(shortName.Value, out var indexes))
            {
                indexes = [];
                lookup[shortName.Value] = indexes;
            }

            indexes.Add(index);
        }

        return lookup;
    }
}
