namespace InSpectra.Discovery.Tool.StaticAnalysis;

using System.Text.Json.Nodes;

internal sealed class StaticAnalysisOpenCliArgumentBuilder
{
    public JsonArray? BuildArguments(StaticCommandDefinition? staticCommand, Document? helpDocument)
    {
        if (staticCommand?.Values.Count is > 0)
        {
            return BuildMetadataFirstArguments(staticCommand, helpDocument);
        }

        if (helpDocument?.Arguments.Count is not > 0)
        {
            return null;
        }

        var helpOnlyArgs = new JsonArray();
        foreach (var argument in helpDocument.Arguments)
        {
            helpOnlyArgs.Add(BuildArgumentNode(
                argument.Key,
                argument.IsRequired,
                isSequence: false,
                argument.Description,
                clrType: null,
                acceptedValues: null));
        }

        return helpOnlyArgs.Count > 0 ? helpOnlyArgs : null;
    }

    private JsonArray? BuildMetadataFirstArguments(StaticCommandDefinition staticCommand, Document? helpDocument)
    {
        var array = new JsonArray();
        var helpArguments = helpDocument?.Arguments.ToList() ?? [];
        var matchedHelpArguments = new bool[helpArguments.Count];
        var useIndexFallback = helpArguments.Count == staticCommand.Values.Count;

        for (var index = 0; index < staticCommand.Values.Count; index++)
        {
            var definition = staticCommand.Values[index];
            var helpArgumentIndex = FindHelpArgumentIndex(helpArguments, matchedHelpArguments, definition.Name);
            if (helpArgumentIndex < 0 && useIndexFallback && !matchedHelpArguments[index])
            {
                helpArgumentIndex = index;
            }

            Item? helpArgument = null;
            if (helpArgumentIndex >= 0)
            {
                matchedHelpArguments[helpArgumentIndex] = true;
                helpArgument = helpArguments[helpArgumentIndex];
            }

            array.Add(BuildArgumentNode(
                definition.Name ?? $"value{definition.Index}",
                definition.IsRequired,
                definition.IsSequence,
                helpArgument?.Description ?? definition.Description,
                definition.ClrType,
                definition.AcceptedValues));
        }

        for (var index = 0; index < helpArguments.Count; index++)
        {
            if (matchedHelpArguments[index])
            {
                continue;
            }

            var helpArgument = helpArguments[index];
            array.Add(BuildArgumentNode(
                helpArgument.Key,
                helpArgument.IsRequired,
                isSequence: false,
                helpArgument.Description,
                clrType: null,
                acceptedValues: null));
        }

        return array.Count > 0 ? array : null;
    }

    private JsonObject BuildArgumentNode(
        string name,
        bool required,
        bool isSequence,
        string? description,
        string? clrType,
        IReadOnlyList<string>? acceptedValues)
    {
        var argument = new JsonObject
        {
            ["name"] = name,
            ["required"] = required,
            ["hidden"] = false,
            ["arity"] = StaticAnalysisOpenCliNodeSupport.BuildArity(isSequence, required ? 1 : 0),
        };

        StaticAnalysisOpenCliNodeSupport.AddIfPresent(argument, "description", description);
        StaticAnalysisOpenCliNodeSupport.ApplyInputMetadata(argument, clrType, acceptedValues);
        return argument;
    }

    private static int FindHelpArgumentIndex(
        IReadOnlyList<Item> helpArguments,
        IReadOnlyList<bool> matched,
        string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return -1;
        }

        var normalized = StaticAnalysisOpenCliNodeSupport.NormalizeForLookup(name);
        for (var index = 0; index < helpArguments.Count; index++)
        {
            if (matched[index])
            {
                continue;
            }

            if (string.Equals(
                StaticAnalysisOpenCliNodeSupport.NormalizeForLookup(helpArguments[index].Key),
                normalized,
                StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}

