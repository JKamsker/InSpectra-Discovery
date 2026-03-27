using System.Text.Json.Nodes;
internal sealed class CliFxOpenCliBuilder
{
    private readonly CliFxCommandTreeBuilder _commandTreeBuilder = new();
    public JsonObject Build(
        string commandName,
        string packageVersion,
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands,
        IReadOnlyDictionary<string, CliFxHelpDocument> helpDocuments)
    {
        helpDocuments.TryGetValue(string.Empty, out var rootHelp);
        staticCommands.TryGetValue(string.Empty, out var defaultCommand);
        var rootCommands = new JsonArray();
        if (defaultCommand is not null)
        {
            rootCommands.Add(BuildDefaultCommandNode(defaultCommand, rootHelp));
        }
        foreach (var child in _commandTreeBuilder.Build(staticCommands, helpDocuments))
        {
            rootCommands.Add(BuildCommandNode(child, staticCommands, helpDocuments));
        }

        return new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = rootHelp?.Title ?? commandName,
                ["version"] = rootHelp?.Version ?? packageVersion,
                ["description"] = rootHelp?.ApplicationDescription ?? defaultCommand?.Description,
            },
            ["x-inspectra"] = new JsonObject
            {
                ["artifactSource"] = "crawled-from-clifx-help",
                ["generator"] = "InSpectra.Discovery",
                ["metadataEnriched"] = staticCommands.Count > 0,
                ["helpDocumentCount"] = helpDocuments.Count,
            },
            ["options"] = BuildOptions(defaultCommand, rootHelp),
            ["arguments"] = BuildArguments(defaultCommand, rootHelp),
            ["commands"] = rootCommands,
        };
    }
    private JsonObject BuildDefaultCommandNode(CliFxCommandDefinition command, CliFxHelpDocument? helpDocument)
    {
        var node = BuildCommandPayload("__default_command", command, helpDocument, helpDocument?.CommandDescription ?? command.Description);
        node["hidden"] = true;
        return node;
    }

    private JsonObject BuildCommandNode(
        CliFxCommandNode commandNode,
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands,
        IReadOnlyDictionary<string, CliFxHelpDocument> helpDocuments)
    {
        staticCommands.TryGetValue(commandNode.FullName, out var command);
        helpDocuments.TryGetValue(commandNode.FullName, out var helpDocument);

        var node = BuildCommandPayload(
            commandNode.DisplayName,
            command,
            helpDocument,
            helpDocument?.CommandDescription ?? command?.Description ?? commandNode.Description);

        if (commandNode.Children.Count > 0)
        {
            node["commands"] = new JsonArray(commandNode.Children
                .Select(child => BuildCommandNode(child, staticCommands, helpDocuments))
                .ToArray());
        }

        return node;
    }
    private JsonObject BuildCommandPayload(string name, CliFxCommandDefinition? command, CliFxHelpDocument? helpDocument, string? description)
    {
        var node = new JsonObject
        {
            ["name"] = name,
            ["description"] = description,
            ["hidden"] = false,
        };

        var options = BuildOptions(command, helpDocument);
        if (options is not null)
        {
            node["options"] = options;
        }

        var arguments = BuildArguments(command, helpDocument);
        if (arguments is not null)
        {
            node["arguments"] = arguments;
        }

        return node;
    }

    private JsonArray? BuildOptions(CliFxCommandDefinition? command, CliFxHelpDocument? helpDocument)
    {
        if (helpDocument?.Options.Count is > 0)
        {
            var array = new JsonArray();
            foreach (var option in helpDocument.Options)
            {
                var names = CliFxOptionNameSupport.Parse(option.Key);
                var definition = command?.Options.FirstOrDefault(candidate =>
                    (names.LongName is not null && string.Equals(candidate.Name, names.LongName, StringComparison.OrdinalIgnoreCase))
                    || (names.ShortName is not null && candidate.ShortName == names.ShortName));

                array.Add(BuildOptionNode(
                    names.LongName is not null ? $"--{names.LongName}" : $"-{names.ShortName}",
                    definition,
                    option.Description,
                    option.IsRequired,
                    names.LongName,
                    names.ShortName));
            }

            return array.Count > 0 ? array : null;
        }

        if (command?.Options.Count is not > 0)
        {
            return null;
        }

        var metadataOptions = new JsonArray();
        foreach (var option in command.Options)
        {
            metadataOptions.Add(BuildOptionNode(CliFxOptionNameSupport.GetPrimaryName(option), option, option.Description, option.IsRequired, option.Name, option.ShortName));
        }

        return metadataOptions.Count > 0 ? metadataOptions : null;
    }

    private JsonArray? BuildArguments(CliFxCommandDefinition? command, CliFxHelpDocument? helpDocument)
    {
        if (helpDocument?.Parameters.Count is > 0)
        {
            var array = new JsonArray();
            for (var index = 0; index < helpDocument.Parameters.Count; index++)
            {
                var parameter = helpDocument.Parameters[index];
                var definition = command?.Parameters.ElementAtOrDefault(index);
                array.Add(BuildArgumentNode(
                    parameter.Key,
                    definition?.IsRequired ?? parameter.IsRequired,
                    definition?.IsSequence ?? false,
                    parameter.Description ?? definition?.Description,
                    definition?.ClrType,
                    definition?.AcceptedValues));
            }

            return array.Count > 0 ? array : null;
        }

        if (command?.Parameters.Count is not > 0)
        {
            return null;
        }

        var metadataArguments = new JsonArray();
        foreach (var parameter in command.Parameters)
        {
            metadataArguments.Add(BuildArgumentNode(
                parameter.Name,
                parameter.IsRequired,
                parameter.IsSequence,
                parameter.Description,
                parameter.ClrType,
                parameter.AcceptedValues));
        }

        return metadataArguments.Count > 0 ? metadataArguments : null;
    }

    private JsonObject BuildOptionNode(
        string name,
        CliFxOptionDefinition? definition,
        string? description,
        bool required,
        string? longName,
        char? shortName)
    {
        var optionNode = new JsonObject
        {
            ["name"] = name,
            ["required"] = definition?.IsRequired ?? required,
            ["description"] = description ?? definition?.Description,
            ["recursive"] = false,
            ["hidden"] = false,
        };

        var aliases = CliFxOptionNameSupport.BuildAliases(definition, longName, shortName);
        if (aliases is not null)
        {
            optionNode["aliases"] = aliases;
        }

        var argument = BuildOptionArgument(definition, definition?.Name ?? longName ?? shortName?.ToString() ?? "VALUE");
        if (argument is not null)
        {
            optionNode["arguments"] = new JsonArray { argument };
        }

        return optionNode;
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
            ["description"] = description,
            ["hidden"] = false,
            ["arity"] = BuildArity(isSequence, required ? 1 : 0),
        };

        ApplyInputMetadata(argument, clrType, acceptedValues, null);
        return argument;
    }

    private JsonObject? BuildOptionArgument(CliFxOptionDefinition? definition, string fallbackName)
    {
        if (definition is null)
        {
            return null;
        }

        var isNullableBool = string.Equals(definition.ClrType, "System.Nullable<System.Boolean>", StringComparison.Ordinal);
        if (!definition.IsRequired && definition.IsBoolLike && !isNullableBool)
        {
            return null;
        }

        fallbackName = CliFxOptionNameSupport.NormalizeLongName(fallbackName) ?? fallbackName;
        var argument = new JsonObject
        {
            ["name"] = fallbackName.ToUpperInvariant(),
            ["required"] = definition.IsRequired,
            ["arity"] = BuildArity(definition.IsSequence, definition.IsRequired ? 1 : 0),
        };

        ApplyInputMetadata(argument, definition.ClrType, definition.AcceptedValues, definition.EnvironmentVariable);
        return argument;
    }

    private static JsonObject BuildArity(bool isSequence, int minimum)
    {
        var arity = new JsonObject
        {
            ["minimum"] = minimum,
        };

        if (!isSequence)
        {
            arity["maximum"] = 1;
        }

        return arity;
    }

    private static void ApplyInputMetadata(JsonObject node, string? clrType, IReadOnlyList<string>? acceptedValues, string? environmentVariable)
    {
        var metadata = new JsonArray();
        if (!string.IsNullOrWhiteSpace(clrType))
        {
            metadata.Add(new JsonObject
            {
                ["name"] = "ClrType",
                ["value"] = clrType,
            });
        }

        if (!string.IsNullOrWhiteSpace(environmentVariable))
        {
            metadata.Add(new JsonObject
            {
                ["name"] = "EnvironmentVariable",
                ["value"] = environmentVariable,
            });
        }

        if (metadata.Count > 0)
        {
            node["metadata"] = metadata;
        }

        if (acceptedValues is { Count: > 0 })
        {
            node["acceptedValues"] = new JsonArray(acceptedValues.Select(value => JsonValue.Create(value)).ToArray());
        }
    }
}
