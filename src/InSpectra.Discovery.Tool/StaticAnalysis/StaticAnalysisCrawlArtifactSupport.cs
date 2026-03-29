using System.Text.Json.Nodes;

internal static class StaticAnalysisCrawlArtifactSupport
{
    public static JsonObject BuildMetadata(
        IReadOnlyDictionary<string, StaticCommandDefinition> staticCommands,
        JsonObject coverage)
        => new()
        {
            ["coverage"] = coverage.DeepClone(),
            ["staticCommands"] = SerializeStaticCommands(staticCommands),
        };

    public static JsonArray SerializeStaticCommands(IReadOnlyDictionary<string, StaticCommandDefinition> staticCommands)
        => new(staticCommands
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new JsonObject
            {
                ["key"] = pair.Key,
                ["name"] = pair.Value.Name,
                ["description"] = pair.Value.Description,
                ["isDefault"] = pair.Value.IsDefault,
                ["isHidden"] = pair.Value.IsHidden,
                ["values"] = new JsonArray(pair.Value.Values
                    .Select(value => new JsonObject
                    {
                        ["index"] = value.Index,
                        ["name"] = value.Name,
                        ["isRequired"] = value.IsRequired,
                        ["isSequence"] = value.IsSequence,
                        ["clrType"] = value.ClrType,
                        ["description"] = value.Description,
                        ["defaultValue"] = value.DefaultValue,
                        ["acceptedValues"] = new JsonArray(value.AcceptedValues.Select(v => JsonValue.Create(v)).ToArray()),
                    })
                    .ToArray()),
                ["options"] = new JsonArray(pair.Value.Options
                    .Select(option => new JsonObject
                    {
                        ["longName"] = option.LongName,
                        ["shortName"] = option.ShortName?.ToString(),
                        ["isRequired"] = option.IsRequired,
                        ["isSequence"] = option.IsSequence,
                        ["isBoolLike"] = option.IsBoolLike,
                        ["clrType"] = option.ClrType,
                        ["description"] = option.Description,
                        ["defaultValue"] = option.DefaultValue,
                        ["metaValue"] = option.MetaValue,
                        ["acceptedValues"] = new JsonArray(option.AcceptedValues.Select(v => JsonValue.Create(v)).ToArray()),
                        ["propertyName"] = option.PropertyName,
                    })
                    .ToArray()),
            })
            .ToArray());

    public static Dictionary<string, StaticCommandDefinition> DeserializeStaticCommands(JsonNode? node)
    {
        var commands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var commandNode in node as JsonArray ?? [])
        {
            if (commandNode is not JsonObject commandObject)
            {
                continue;
            }

            var key = commandObject["key"]?.GetValue<string>() ?? string.Empty;
            commands[key] = new StaticCommandDefinition(
                Name: commandObject["name"]?.GetValue<string>(),
                Description: commandObject["description"]?.GetValue<string>(),
                IsDefault: commandObject["isDefault"]?.GetValue<bool>() ?? false,
                IsHidden: commandObject["isHidden"]?.GetValue<bool>() ?? false,
                Values: DeserializeValues(commandObject["values"]).OrderBy(v => v.Index).ToArray(),
                Options: DeserializeOptions(commandObject["options"])
                    .OrderByDescending(o => o.IsRequired)
                    .ThenBy(o => o.LongName)
                    .ThenBy(o => o.ShortName)
                    .ToArray());
        }

        return commands;
    }

    private static IReadOnlyList<StaticValueDefinition> DeserializeValues(JsonNode? node)
        => (node as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(value => new StaticValueDefinition(
                Index: value["index"]?.GetValue<int>() ?? 0,
                Name: value["name"]?.GetValue<string>(),
                IsRequired: value["isRequired"]?.GetValue<bool>() ?? false,
                IsSequence: value["isSequence"]?.GetValue<bool>() ?? false,
                ClrType: value["clrType"]?.GetValue<string>(),
                Description: value["description"]?.GetValue<string>(),
                DefaultValue: value["defaultValue"]?.GetValue<string>(),
                AcceptedValues: ReadStrings(value["acceptedValues"])))
            .ToArray();

    private static IReadOnlyList<StaticOptionDefinition> DeserializeOptions(JsonNode? node)
        => (node as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(option => new StaticOptionDefinition(
                LongName: option["longName"]?.GetValue<string>(),
                ShortName: ReadShortName(option["shortName"]),
                IsRequired: option["isRequired"]?.GetValue<bool>() ?? false,
                IsSequence: option["isSequence"]?.GetValue<bool>() ?? false,
                IsBoolLike: option["isBoolLike"]?.GetValue<bool>() ?? false,
                ClrType: option["clrType"]?.GetValue<string>(),
                Description: option["description"]?.GetValue<string>(),
                DefaultValue: option["defaultValue"]?.GetValue<string>(),
                MetaValue: option["metaValue"]?.GetValue<string>(),
                AcceptedValues: ReadStrings(option["acceptedValues"]),
                PropertyName: option["propertyName"]?.GetValue<string>()))
            .ToArray();

    private static IReadOnlyList<string> ReadStrings(JsonNode? node)
        => (node as JsonArray ?? [])
            .OfType<JsonValue>()
            .Select(value => value.GetValue<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

    private static char? ReadShortName(JsonNode? node)
    {
        var value = node?.GetValue<string>();
        return string.IsNullOrWhiteSpace(value) ? null : value[0];
    }
}
