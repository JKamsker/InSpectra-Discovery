using System.Text.Json.Nodes;

internal sealed class StaticAnalysisOpenCliOptionBuilder
{
    public JsonArray? BuildOptions(StaticCommandDefinition? staticCommand, ToolHelpDocument? helpDocument)
    {
        if (helpDocument?.Options.Count is not > 0 && staticCommand?.Options.Count is not > 0)
        {
            return null;
        }

        var array = new JsonArray();
        var matcher = new StaticOptionDefinitionMatcher(staticCommand?.Options ?? []);

        foreach (var helpOption in helpDocument?.Options ?? [])
        {
            var names = StaticAnalysisOpenCliNodeSupport.ParseHelpOptionNames(helpOption.Key);
            var definition = matcher.TakeMatch(names.LongName, names.ShortName);
            array.Add(BuildOptionNode(
                names.LongName is not null ? $"--{names.LongName}" : $"-{names.ShortName}",
                definition,
                helpOption.Description ?? definition?.Description,
                helpOption.IsRequired || definition?.IsRequired == true,
                names.LongName,
                names.ShortName));
        }

        foreach (var option in matcher.GetRemainingDefinitions())
        {
            if (option.IsBoolLike && StaticAnalysisOpenCliNodeSupport.IsHiddenOption(option))
            {
                continue;
            }

            var primaryName = option.LongName is not null ? $"--{option.LongName}" : $"-{option.ShortName}";
            array.Add(BuildOptionNode(
                primaryName,
                option,
                option.Description,
                option.IsRequired,
                option.LongName,
                option.ShortName));
        }

        return array.Count > 0 ? array : null;
    }

    private JsonObject BuildOptionNode(
        string name,
        StaticOptionDefinition? definition,
        string? description,
        bool required,
        string? longName,
        char? shortName)
    {
        var optionNode = new JsonObject
        {
            ["name"] = name,
            ["recursive"] = false,
            ["hidden"] = definition is not null && StaticAnalysisOpenCliNodeSupport.IsHiddenOption(definition),
        };
        StaticAnalysisOpenCliNodeSupport.AddIfPresent(optionNode, "description", description);

        var aliases = BuildAliases(definition, longName, shortName);
        if (aliases is not null)
        {
            optionNode["aliases"] = aliases;
        }

        var argument = BuildOptionArgument(definition, longName, shortName, required);
        if (argument is not null)
        {
            optionNode["arguments"] = new JsonArray { argument };
        }

        return optionNode;
    }

    private JsonObject? BuildOptionArgument(
        StaticOptionDefinition? definition,
        string? longName,
        char? shortName,
        bool required)
    {
        if (definition is null)
        {
            if (!required)
            {
                return null;
            }

            return new JsonObject
            {
                ["name"] = StaticAnalysisOpenCliNodeSupport.NormalizeArgumentName(longName ?? shortName?.ToString() ?? "VALUE"),
                ["required"] = required,
                ["arity"] = StaticAnalysisOpenCliNodeSupport.BuildArity(false, 1),
            };
        }

        if (!definition.IsRequired && definition.IsBoolLike)
        {
            return null;
        }

        var argument = new JsonObject
        {
            ["name"] = StaticAnalysisOpenCliNodeSupport.NormalizeArgumentName(
                definition.MetaValue ?? definition.PropertyName ?? definition.LongName ?? shortName?.ToString() ?? "VALUE"),
            ["required"] = definition.IsRequired,
            ["arity"] = StaticAnalysisOpenCliNodeSupport.BuildArity(definition.IsSequence, definition.IsRequired ? 1 : 0),
        };

        StaticAnalysisOpenCliNodeSupport.ApplyInputMetadata(argument, definition.ClrType, definition.AcceptedValues);
        return argument;
    }

    private static JsonArray? BuildAliases(StaticOptionDefinition? definition, string? longName, char? shortName)
    {
        var aliases = new JsonArray();

        if (definition is not null)
        {
            if (longName is not null && definition.LongName is not null
                && !string.Equals(longName, definition.LongName, StringComparison.OrdinalIgnoreCase))
            {
                aliases.Add($"--{definition.LongName}");
            }

            if (shortName is not null && definition.ShortName is not null
                && shortName != definition.ShortName)
            {
                aliases.Add($"-{definition.ShortName}");
            }
            else if (shortName is null && definition.ShortName is not null)
            {
                aliases.Add($"-{definition.ShortName}");
            }
            else if (longName is null && definition.LongName is not null)
            {
                aliases.Add($"--{definition.LongName}");
            }
        }

        if (longName is not null && shortName is not null)
        {
            aliases.Add($"-{shortName}");
        }

        return aliases.Count > 0 ? aliases : null;
    }
}
