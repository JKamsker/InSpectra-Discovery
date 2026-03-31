namespace InSpectra.Discovery.Tool.Analysis.CliFx.OpenCli;

using InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;

using System.Text.Json.Nodes;

internal sealed class CliFxOpenCliArgumentBuilder
{
    public JsonArray? BuildArguments(CliFxCommandDefinition? command, CliFxHelpDocument? helpDocument)
    {
        if (command?.Parameters.Count is > 0)
        {
            return BuildMetadataFirstArguments(command, helpDocument);
        }

        if (helpDocument?.Parameters.Count is not > 0)
        {
            return null;
        }

        var helpArguments = new JsonArray();
        foreach (var parameter in helpDocument.Parameters)
        {
            helpArguments.Add(BuildArgumentNode(
                parameter.Key,
                parameter.IsRequired,
                isSequence: false,
                parameter.Description,
                clrType: null,
                acceptedValues: null));
        }

        return helpArguments.Count > 0 ? helpArguments : null;
    }

    private JsonArray? BuildMetadataFirstArguments(CliFxCommandDefinition command, CliFxHelpDocument? helpDocument)
    {
        var array = new JsonArray();
        var helpParameters = helpDocument?.Parameters.ToList() ?? [];
        var matchedHelpParameters = new bool[helpParameters.Count];
        var useIndexFallback = helpParameters.Count == command.Parameters.Count;
        for (var index = 0; index < command.Parameters.Count; index++)
        {
            var definition = command.Parameters[index];
            var helpParameterIndex = FindHelpParameterIndex(helpParameters, matchedHelpParameters, definition.Name);
            if (helpParameterIndex < 0 && useIndexFallback && !matchedHelpParameters[index])
            {
                helpParameterIndex = index;
            }

            CliFxHelpItem? parameter = null;
            if (helpParameterIndex >= 0)
            {
                matchedHelpParameters[helpParameterIndex] = true;
                parameter = helpParameters[helpParameterIndex];
            }

            array.Add(BuildArgumentNode(
                definition.Name,
                definition.IsRequired,
                definition.IsSequence,
                parameter?.Description ?? definition.Description,
                definition.ClrType,
                definition.AcceptedValues));
        }

        for (var index = 0; index < helpParameters.Count; index++)
        {
            if (matchedHelpParameters[index])
            {
                continue;
            }

            var parameter = helpParameters[index];
            array.Add(BuildArgumentNode(
                parameter.Key,
                parameter.IsRequired,
                isSequence: false,
                parameter.Description,
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
            ["arity"] = CliFxOpenCliNodeSupport.BuildArity(isSequence, required ? 1 : 0),
        };

        CliFxOpenCliNodeSupport.AddIfPresent(argument, "description", description);
        CliFxOpenCliNodeSupport.ApplyInputMetadata(argument, clrType, acceptedValues, null);
        return argument;
    }

    private static int FindHelpParameterIndex(
        IReadOnlyList<CliFxHelpItem> helpParameters,
        IReadOnlyList<bool> matchedHelpParameters,
        string parameterName)
    {
        var normalizedParameterName = CliFxOpenCliNodeSupport.NormalizeParameterLookupKey(parameterName);
        for (var index = 0; index < helpParameters.Count; index++)
        {
            if (matchedHelpParameters[index])
            {
                continue;
            }

            if (string.Equals(
                CliFxOpenCliNodeSupport.NormalizeParameterLookupKey(helpParameters[index].Key),
                normalizedParameterName,
                StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}

