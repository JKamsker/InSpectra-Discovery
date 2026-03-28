using System.Text.Json.Nodes;

internal static class CliFxCrawlArtifactSupport
{
    public static JsonObject BuildMetadata(
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands,
        JsonObject coverage)
        => new()
        {
            ["coverage"] = coverage.DeepClone(),
            ["staticCommands"] = SerializeStaticCommands(staticCommands),
        };

    public static JsonArray SerializeStaticCommands(IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands)
        => new(staticCommands
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => new JsonObject
            {
                ["key"] = pair.Key,
                ["name"] = pair.Value.Name,
                ["description"] = pair.Value.Description,
                ["parameters"] = new JsonArray(pair.Value.Parameters
                    .Select(parameter => new JsonObject
                    {
                        ["order"] = parameter.Order,
                        ["name"] = parameter.Name,
                        ["isRequired"] = parameter.IsRequired,
                        ["isSequence"] = parameter.IsSequence,
                        ["clrType"] = parameter.ClrType,
                        ["description"] = parameter.Description,
                        ["acceptedValues"] = new JsonArray(parameter.AcceptedValues.Select(value => JsonValue.Create(value)).ToArray()),
                    })
                    .ToArray()),
                ["options"] = new JsonArray(pair.Value.Options
                    .Select(option => new JsonObject
                    {
                        ["name"] = option.Name,
                        ["shortName"] = option.ShortName?.ToString(),
                        ["isRequired"] = option.IsRequired,
                        ["isSequence"] = option.IsSequence,
                        ["isBoolLike"] = option.IsBoolLike,
                        ["clrType"] = option.ClrType,
                        ["description"] = option.Description,
                        ["environmentVariable"] = option.EnvironmentVariable,
                        ["acceptedValues"] = new JsonArray(option.AcceptedValues.Select(value => JsonValue.Create(value)).ToArray()),
                        ["valueName"] = option.ValueName,
                    })
                    .ToArray()),
            })
            .ToArray());

    public static Dictionary<string, CliFxCommandDefinition> DeserializeStaticCommands(JsonNode? node)
    {
        var commands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var commandNode in node as JsonArray ?? [])
        {
            if (commandNode is not JsonObject commandObject)
            {
                continue;
            }

            var key = commandObject["key"]?.GetValue<string>() ?? string.Empty;
            commands[key] = new CliFxCommandDefinition(
                Name: commandObject["name"]?.GetValue<string>(),
                Description: commandObject["description"]?.GetValue<string>(),
                Parameters: DeserializeParameters(commandObject["parameters"]).OrderBy(parameter => parameter.Order).ToArray(),
                Options: DeserializeOptions(commandObject["options"])
                    .OrderByDescending(option => option.IsRequired)
                    .ThenBy(option => option.Name)
                    .ThenBy(option => option.ShortName)
                    .ToArray());
        }

        return commands;
    }

    private static IReadOnlyList<CliFxParameterDefinition> DeserializeParameters(JsonNode? node)
        => (node as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(parameter => new CliFxParameterDefinition(
                Order: parameter["order"]?.GetValue<int?>() ?? 0,
                Name: parameter["name"]?.GetValue<string>() ?? string.Empty,
                IsRequired: parameter["isRequired"]?.GetValue<bool?>() ?? false,
                IsSequence: parameter["isSequence"]?.GetValue<bool?>() ?? false,
                ClrType: parameter["clrType"]?.GetValue<string>(),
                Description: parameter["description"]?.GetValue<string>(),
                AcceptedValues: ReadStrings(parameter["acceptedValues"])))
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .ToArray();

    private static IReadOnlyList<CliFxOptionDefinition> DeserializeOptions(JsonNode? node)
        => (node as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(option => new CliFxOptionDefinition(
                Name: option["name"]?.GetValue<string>(),
                ShortName: ReadShortName(option["shortName"]),
                IsRequired: option["isRequired"]?.GetValue<bool?>() ?? false,
                IsSequence: option["isSequence"]?.GetValue<bool?>() ?? false,
                IsBoolLike: option["isBoolLike"]?.GetValue<bool?>() ?? false,
                ClrType: option["clrType"]?.GetValue<string>(),
                Description: option["description"]?.GetValue<string>(),
                EnvironmentVariable: option["environmentVariable"]?.GetValue<string>(),
                AcceptedValues: ReadStrings(option["acceptedValues"]),
                ValueName: option["valueName"]?.GetValue<string>()))
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
