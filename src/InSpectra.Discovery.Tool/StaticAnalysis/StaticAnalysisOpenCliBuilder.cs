using System.Text.Json.Nodes;

internal sealed class StaticAnalysisOpenCliBuilder
{
    public JsonObject Build(
        string commandName,
        string packageVersion,
        string framework,
        IReadOnlyDictionary<string, StaticCommandDefinition> staticCommands,
        IReadOnlyDictionary<string, ToolHelpDocument> helpDocuments)
    {
        helpDocuments.TryGetValue(string.Empty, out var rootHelp);
        staticCommands.TryGetValue(string.Empty, out var defaultCommand);

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
            ["x-inspectra"] = BuildExtensionMetadata(framework, staticCommands, helpDocuments),
        };

        var commandNodes = BuildCommandNodes(staticCommands, helpDocuments);
        if (commandNodes.Count > 0)
        {
            document["commands"] = commandNodes;
        }

        AddIfPresent(document, "options", BuildOptions(defaultCommand, rootHelp));
        AddIfPresent(document, "arguments", BuildArguments(defaultCommand, rootHelp));
        return OpenCliDocumentSanitizer.Sanitize(document);
    }

    private static JsonObject BuildExtensionMetadata(
        string framework,
        IReadOnlyDictionary<string, StaticCommandDefinition> staticCommands,
        IReadOnlyDictionary<string, ToolHelpDocument> helpDocuments)
    {
        var optionCount = staticCommands.Values.Sum(c => c.Options.Count);
        var valueCount = staticCommands.Values.Sum(c => c.Values.Count);
        var verbCount = staticCommands.Count(pair => !string.IsNullOrEmpty(pair.Key));

        var limitations = new JsonArray();
        limitations.Add("property-defaults-not-captured");
        limitations.Add("fluent-api-configuration-not-visible");

        return new JsonObject
        {
            ["artifactSource"] = "static-analysis",
            ["generator"] = "InSpectra.Discovery",
            ["metadataEnriched"] = staticCommands.Count > 0,
            ["helpDocumentCount"] = helpDocuments.Count,
            ["staticAnalysis"] = new JsonObject
            {
                ["framework"] = framework,
                ["inspectorType"] = "dnlib",
                ["confidence"] = staticCommands.Count > 0 ? "high" : "low",
                ["verbCount"] = verbCount,
                ["optionCount"] = optionCount,
                ["valueCount"] = valueCount,
                ["limitations"] = limitations,
            },
        };
    }

    private static JsonArray BuildCommandNodes(
        IReadOnlyDictionary<string, StaticCommandDefinition> staticCommands,
        IReadOnlyDictionary<string, ToolHelpDocument> helpDocuments)
    {
        var commandNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in staticCommands.Keys.Where(k => !string.IsNullOrEmpty(k)))
        {
            commandNames.Add(key);
        }

        foreach (var key in helpDocuments.Keys.Where(k => !string.IsNullOrEmpty(k)))
        {
            commandNames.Add(key);
        }

        if (helpDocuments.TryGetValue(string.Empty, out var rootHelp))
        {
            foreach (var command in rootHelp.Commands)
            {
                if (!string.IsNullOrWhiteSpace(command.Key)
                    && !string.Equals(command.Key, "help", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(command.Key, "version", StringComparison.OrdinalIgnoreCase))
                {
                    commandNames.Add(command.Key);
                }
            }
        }

        var nodes = new JsonArray();
        foreach (var name in commandNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            staticCommands.TryGetValue(name, out var staticCommand);
            helpDocuments.TryGetValue(name, out var helpDocument);

            var rootHelpCommand = rootHelp?.Commands.FirstOrDefault(c =>
                string.Equals(c.Key, name, StringComparison.OrdinalIgnoreCase));

            nodes.Add(BuildCommandNode(
                name,
                staticCommand,
                helpDocument,
                helpDocument?.CommandDescription ?? staticCommand?.Description ?? rootHelpCommand?.Description));
        }

        return nodes;
    }

    private static JsonObject BuildCommandNode(
        string name,
        StaticCommandDefinition? staticCommand,
        ToolHelpDocument? helpDocument,
        string? description)
    {
        var node = new JsonObject
        {
            ["name"] = name,
            ["hidden"] = staticCommand?.IsHidden ?? false,
        };
        AddIfPresent(node, "description", description);

        var options = BuildOptions(staticCommand, helpDocument);
        AddIfPresent(node, "options", options);
        var arguments = BuildArguments(staticCommand, helpDocument);
        AddIfPresent(node, "arguments", arguments);

        return node;
    }

    private static JsonArray? BuildOptions(StaticCommandDefinition? staticCommand, ToolHelpDocument? helpDocument)
    {
        if (helpDocument?.Options.Count is not > 0 && staticCommand?.Options.Count is not > 0)
        {
            return null;
        }

        var array = new JsonArray();
        var remainingStaticOptions = staticCommand?.Options.ToList() ?? [];

        foreach (var helpOption in helpDocument?.Options ?? [])
        {
            var names = ParseHelpOptionNames(helpOption.Key);
            var definition = remainingStaticOptions.FirstOrDefault(candidate =>
                (names.LongName is not null && string.Equals(candidate.LongName, names.LongName, StringComparison.OrdinalIgnoreCase))
                || (names.ShortName is not null && candidate.ShortName == names.ShortName));

            if (definition is not null)
            {
                remainingStaticOptions.Remove(definition);
            }

            array.Add(BuildOptionNode(
                names.LongName is not null ? $"--{names.LongName}" : $"-{names.ShortName}",
                definition,
                helpOption.Description ?? definition?.Description,
                helpOption.IsRequired || definition?.IsRequired == true,
                names.LongName,
                names.ShortName));
        }

        foreach (var option in remainingStaticOptions)
        {
            if (option.IsBoolLike && string.Equals(option.LongName, "help", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (option.IsBoolLike && string.Equals(option.LongName, "version", StringComparison.OrdinalIgnoreCase))
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

    private static JsonArray? BuildArguments(StaticCommandDefinition? staticCommand, ToolHelpDocument? helpDocument)
    {
        if (staticCommand?.Values.Count is > 0)
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

                ToolHelpItem? helpArgument = null;
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

            for (var i = 0; i < helpArguments.Count; i++)
            {
                if (matchedHelpArguments[i])
                {
                    continue;
                }

                var helpArg = helpArguments[i];
                array.Add(BuildArgumentNode(
                    helpArg.Key,
                    helpArg.IsRequired,
                    isSequence: false,
                    helpArg.Description,
                    clrType: null,
                    acceptedValues: null));
            }

            return array.Count > 0 ? array : null;
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

    private static JsonObject BuildOptionNode(
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
            ["hidden"] = definition?.IsHidden() ?? false,
        };
        AddIfPresent(optionNode, "description", description ?? definition?.Description);

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

    private static JsonObject BuildArgumentNode(
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
        ApplyInputMetadata(argument, clrType, acceptedValues);
        return argument;
    }

    private static JsonObject? BuildOptionArgument(
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
                ["name"] = NormalizeArgumentName(longName ?? shortName?.ToString() ?? "VALUE"),
                ["required"] = required,
                ["arity"] = BuildArity(false, 1),
            };
        }

        if (!definition.IsRequired && definition.IsBoolLike)
        {
            return null;
        }

        var argument = new JsonObject
        {
            ["name"] = NormalizeArgumentName(definition.MetaValue ?? definition.PropertyName ?? definition.LongName ?? shortName?.ToString() ?? "VALUE"),
            ["required"] = definition.IsRequired,
            ["arity"] = BuildArity(definition.IsSequence, definition.IsRequired ? 1 : 0),
        };

        ApplyInputMetadata(argument, definition.ClrType, definition.AcceptedValues);
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

    private static JsonObject BuildArity(bool isSequence, int minimum)
    {
        var arity = new JsonObject { ["minimum"] = minimum };
        if (!isSequence)
        {
            arity["maximum"] = 1;
        }

        return arity;
    }

    private static (string? LongName, char? ShortName) ParseHelpOptionNames(string key)
    {
        string? longName = null;
        char? shortName = null;

        var parts = key.Split(',', '|', ' ');
        foreach (var raw in parts)
        {
            var part = raw.Trim().TrimEnd(':');
            if (part.StartsWith("--", StringComparison.Ordinal) && part.Length > 2)
            {
                longName = part[2..];
            }
            else if (part.StartsWith("-", StringComparison.Ordinal) && part.Length == 2 && char.IsLetterOrDigit(part[1]))
            {
                shortName = part[1];
            }
        }

        return (longName, shortName);
    }

    private static int FindHelpArgumentIndex(
        IReadOnlyList<ToolHelpItem> helpArguments,
        IReadOnlyList<bool> matched,
        string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return -1;
        }

        var normalized = NormalizeForLookup(name);
        for (var index = 0; index < helpArguments.Count; index++)
        {
            if (matched[index])
            {
                continue;
            }

            if (string.Equals(NormalizeForLookup(helpArguments[index].Key), normalized, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }

    private static string NormalizeForLookup(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string NormalizeArgumentName(string value)
    {
        var cleaned = value.Trim('-').Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return "VALUE";
        }

        return string.Join("_", cleaned.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }

    private static void ApplyInputMetadata(JsonObject node, string? clrType, IReadOnlyList<string>? acceptedValues)
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

        if (metadata.Count > 0)
        {
            node["metadata"] = metadata;
        }

        if (acceptedValues is { Count: > 0 })
        {
            node["acceptedValues"] = new JsonArray(acceptedValues.Select(v => JsonValue.Create(v)).ToArray());
        }
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

file static class StaticOptionDefinitionExtensions
{
    public static bool IsHidden(this StaticOptionDefinition definition)
    {
        var longName = definition.LongName;
        return longName is not null
            && (string.Equals(longName, "help", StringComparison.OrdinalIgnoreCase)
                || string.Equals(longName, "version", StringComparison.OrdinalIgnoreCase));
    }
}
