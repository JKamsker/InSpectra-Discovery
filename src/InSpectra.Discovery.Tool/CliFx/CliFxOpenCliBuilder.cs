using System.Text;
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
        var rootCommands = new JsonArray(_commandTreeBuilder
            .Build(staticCommands, helpDocuments)
            .Select(child => BuildCommandNode(child, staticCommands, helpDocuments))
            .ToArray());
        var info = new JsonObject
        {
            ["title"] = rootHelp?.Title ?? commandName,
            ["version"] = rootHelp?.Version ?? packageVersion,
        };
        AddIfPresent(info, "description", rootHelp?.ApplicationDescription ?? defaultCommand?.Description);

        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = info,
            ["x-inspectra"] = new JsonObject
            {
                ["artifactSource"] = "crawled-from-clifx-help",
                ["generator"] = "InSpectra.Discovery",
                ["metadataEnriched"] = staticCommands.Count > 0,
                ["helpDocumentCount"] = helpDocuments.Count,
            },
            ["commands"] = rootCommands,
        };

        AddIfPresent(document, "options", BuildOptions(defaultCommand, rootHelp));
        AddIfPresent(document, "arguments", BuildArguments(defaultCommand, rootHelp));
        return OpenCliDocumentSanitizer.Sanitize(document);
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
            ["hidden"] = false,
        };
        AddIfPresent(node, "description", description);

        var options = BuildOptions(command, helpDocument);
        AddIfPresent(node, "options", options);
        var arguments = BuildArguments(command, helpDocument);
        AddIfPresent(node, "arguments", arguments);

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
            ["recursive"] = false,
            ["hidden"] = false,
        };
        AddIfPresent(optionNode, "description", description ?? definition?.Description);

        var aliases = CliFxOptionNameSupport.BuildAliases(definition, longName, shortName);
        if (aliases is not null)
        {
            optionNode["aliases"] = aliases;
        }

        var argument = BuildOptionArgument(definition, definition?.ValueName ?? definition?.Name ?? longName ?? shortName?.ToString() ?? "VALUE");
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
            ["hidden"] = false,
            ["arity"] = BuildArity(isSequence, required ? 1 : 0),
        };

        AddIfPresent(argument, "description", description);
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

        var argument = new JsonObject
        {
            ["name"] = NormalizeOptionArgumentName(fallbackName),
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

    private static string NormalizeOptionArgumentName(string fallbackName)
    {
        fallbackName = CliFxOptionNameSupport.NormalizeLongName(fallbackName) ?? fallbackName;
        if (string.IsNullOrWhiteSpace(fallbackName))
        {
            return "VALUE";
        }

        var builder = new StringBuilder();
        char? previous = null;
        foreach (var character in fallbackName)
        {
            if (!char.IsLetterOrDigit(character))
            {
                if (builder.Length > 0 && builder[^1] != '_')
                {
                    builder.Append('_');
                }

                previous = character;
                continue;
            }

            if (builder.Length > 0
                && char.IsUpper(character)
                && previous is { } previousValue
                && (char.IsLower(previousValue) || char.IsDigit(previousValue))
                && builder[^1] != '_')
            {
                builder.Append('_');
            }

            builder.Append(char.ToUpperInvariant(character));
            previous = character;
        }

        var normalized = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(normalized) ? "VALUE" : normalized;
    }

    private static void AddIfPresent(JsonObject target, string propertyName, JsonNode? value)
    {
        if (value is not null)
        {
            target[propertyName] = value;
        }
    }

    private static void AddIfPresent(JsonObject target, string propertyName, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[propertyName] = value;
        }
    }
}
