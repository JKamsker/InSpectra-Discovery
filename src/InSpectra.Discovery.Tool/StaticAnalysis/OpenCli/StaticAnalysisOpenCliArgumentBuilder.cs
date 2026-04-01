namespace InSpectra.Discovery.Tool.StaticAnalysis.OpenCli;

using InSpectra.Discovery.Tool.Help.Documents;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;

using System.Text.Json.Nodes;

internal sealed class StaticAnalysisOpenCliArgumentBuilder
{
    public JsonArray? BuildArguments(StaticCommandDefinition? staticCommand, Document? helpDocument)
    {
        if (helpDocument is not null)
        {
            return BuildHelpAnchoredArguments(staticCommand, helpDocument);
        }

        if (staticCommand?.Values.Count is > 0)
        {
            return BuildMetadataFirstArguments(staticCommand);
        }

        return null;
    }

    private JsonArray? BuildHelpAnchoredArguments(StaticCommandDefinition? staticCommand, Document helpDocument)
    {
        if (helpDocument.Arguments.Count is not > 0)
        {
            return null;
        }

        var array = new JsonArray();
        var staticArguments = staticCommand?.Values ?? [];
        var matchedStaticArguments = new bool[staticArguments.Count];
        var useIndexFallback = staticArguments.Count == helpDocument.Arguments.Count;

        for (var index = 0; index < helpDocument.Arguments.Count; index++)
        {
            var helpArgument = helpDocument.Arguments[index];
            var staticArgumentIndex = FindStaticArgumentIndex(staticArguments, matchedStaticArguments, helpArgument.Key);
            if (staticArgumentIndex < 0 && useIndexFallback && !matchedStaticArguments[index])
            {
                staticArgumentIndex = index;
            }

            StaticValueDefinition? staticArgument = null;
            if (staticArgumentIndex >= 0)
            {
                matchedStaticArguments[staticArgumentIndex] = true;
                staticArgument = staticArguments[staticArgumentIndex];
            }

            array.Add(BuildArgumentNode(
                helpArgument.Key,
                helpArgument.IsRequired,
                isSequence: false,
                helpArgument.Description,
                staticArgument?.ClrType,
                staticArgument?.AcceptedValues));
        }

        return array.Count > 0 ? array : null;
    }

    private JsonArray? BuildMetadataFirstArguments(StaticCommandDefinition staticCommand)
    {
        var array = new JsonArray();

        for (var index = 0; index < staticCommand.Values.Count; index++)
        {
            var definition = staticCommand.Values[index];
            array.Add(BuildArgumentNode(
                definition.Name ?? $"value{definition.Index}",
                definition.IsRequired,
                definition.IsSequence,
                definition.Description,
                definition.ClrType,
                definition.AcceptedValues));
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

    private static int FindStaticArgumentIndex(
        IReadOnlyList<StaticValueDefinition> staticArguments,
        IReadOnlyList<bool> matched,
        string helpArgumentName)
    {
        if (string.IsNullOrWhiteSpace(helpArgumentName))
        {
            return -1;
        }

        var normalized = StaticAnalysisOpenCliNodeSupport.NormalizeForLookup(helpArgumentName);
        for (var index = 0; index < staticArguments.Count; index++)
        {
            if (matched[index])
            {
                continue;
            }

            if (string.Equals(
                StaticAnalysisOpenCliNodeSupport.NormalizeForLookup(staticArguments[index].Name),
                normalized,
                StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}
