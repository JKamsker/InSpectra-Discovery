using System.Text.Json.Nodes;

internal static class OpenCliDocumentValidator
{
    private static readonly string[] CommandLikeArrayProperties = ["arguments", "commands", "examples", "options"];
    private static readonly string[] OptionArrayProperties = ["acceptedValues", "aliases", "arguments", "metadata"];
    private static readonly string[] ArgumentArrayProperties = ["acceptedValues", "metadata"];

    public static bool TryLoadValidDocument(string path, out JsonObject? document, out string? reason)
    {
        document = null;
        reason = null;

        if (!PromotionArtifactSupport.TryLoadJsonObject(path, out var parsedDocument) || parsedDocument is null)
        {
            reason = "OpenCLI artifact is not a JSON object.";
            return false;
        }

        if (!TryValidateDocument(parsedDocument, out reason))
        {
            return false;
        }

        document = parsedDocument;
        return true;
    }

    public static bool TryValidateDocument(JsonObject document, out string? reason)
    {
        reason = null;

        if (string.IsNullOrWhiteSpace(GetString(document["opencli"])))
        {
            reason = "OpenCLI artifact is missing the root 'opencli' marker.";
            return false;
        }

        if (!TryValidateInfo(document, out reason))
        {
            return false;
        }

        if (!TryValidateCommandLikeNode(document, "$", isRoot: true, out reason))
        {
            return false;
        }

        if (!OpenCliDocumentPublishabilityInspector.HasPublishableSurface(document))
        {
            reason = "OpenCLI artifact does not expose any commands, options, or arguments.";
            return false;
        }

        if (OpenCliDocumentPublishabilityInspector.LooksLikeInventoryOnlyCommandShellDocument(document))
        {
            reason = "OpenCLI artifact only exposes root command inventory shells without any detailed command surface.";
            return false;
        }

        if (OpenCliDocumentPublishabilityInspector.ContainsErrorText(document))
        {
            reason = "OpenCLI artifact contains error or exception text in its surface descriptions.";
            return false;
        }

        var totalCommandCount = OpenCliDocumentPublishabilityInspector.CountTotalCommands(document);
        if (totalCommandCount > 500)
        {
            reason = $"OpenCLI artifact has an implausible command count ({totalCommandCount}).";
            return false;
        }

        if (OpenCliDocumentPublishabilityInspector.ContainsBoxDrawingCommandNames(document))
        {
            reason = "OpenCLI artifact contains box-drawing characters in command names (table art parsed as commands).";
            return false;
        }

        return true;
    }

    private static bool TryValidateInfo(JsonObject document, out string? reason)
    {
        reason = null;

        if (!document.TryGetPropertyValue("info", out var infoNode) || infoNode is null)
        {
            return true;
        }

        if (infoNode is not JsonObject info)
        {
            reason = "OpenCLI artifact has a non-object 'info' property.";
            return false;
        }

        var title = GetString(info["title"]);
        if (OpenCliDocumentPublishabilityInspector.LooksLikeNonPublishableTitle(title))
        {
            reason = "OpenCLI artifact has a non-publishable 'info.title' value.";
            return false;
        }

        var description = GetString(info["description"]);
        if (OpenCliDocumentPublishabilityInspector.LooksLikeNonPublishableDescription(description))
        {
            reason = "OpenCLI artifact has a non-publishable 'info.description' value.";
            return false;
        }

        return true;
    }

    private static bool TryValidateCommandLikeNode(JsonObject node, string path, bool isRoot, out string? reason)
    {
        reason = null;

        foreach (var arrayProperty in CommandLikeArrayProperties)
        {
            if (!TryValidateArrayProperty(node, arrayProperty, path, out reason))
            {
                return false;
            }
        }

        if (node["examples"] is JsonArray examples
            && !TryValidateStringEntries(examples, $"{path}.examples", out reason))
        {
            return false;
        }

        if (!isRoot && string.Equals(GetString(node["name"]), "__default_command", StringComparison.Ordinal))
        {
            reason = $"OpenCLI artifact contains a '__default_command' node at '{path}'.";
            return false;
        }

        var optionNodes = new List<JsonObject>();
        if (node["options"] is JsonArray options)
        {
            for (var index = 0; index < options.Count; index++)
            {
                if (options[index] is not JsonObject option)
                {
                    reason = $"OpenCLI artifact has a non-object entry at '{path}.options[{index}]'.";
                    return false;
                }

                optionNodes.Add(option);
                if (!TryValidateOptionNode(option, $"{path}.options[{index}]", out reason))
                {
                    return false;
                }
            }
        }

        if (!TryValidateOptionCollisions(optionNodes, path, out reason))
        {
            return false;
        }

        if (node["arguments"] is JsonArray arguments)
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                if (arguments[index] is not JsonObject argument)
                {
                    reason = $"OpenCLI artifact has a non-object entry at '{path}.arguments[{index}]'.";
                    return false;
                }

                if (!TryValidateArgumentNode(argument, $"{path}.arguments[{index}]", out reason))
                {
                    return false;
                }
            }
        }

        if (node["commands"] is JsonArray commands)
        {
            for (var index = 0; index < commands.Count; index++)
            {
                if (commands[index] is not JsonObject command)
                {
                    reason = $"OpenCLI artifact has a non-object entry at '{path}.commands[{index}]'.";
                    return false;
                }

                if (!TryValidateCommandLikeNode(command, $"{path}.commands[{index}]", isRoot: false, out reason))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryValidateOptionNode(JsonObject node, string path, out string? reason)
    {
        reason = null;

        foreach (var arrayProperty in OptionArrayProperties)
        {
            if (!TryValidateArrayProperty(node, arrayProperty, path, out reason))
            {
                return false;
            }
        }

        if (node["aliases"] is JsonArray aliases
            && !TryValidateStringEntries(aliases, $"{path}.aliases", out reason))
        {
            return false;
        }

        if (node["acceptedValues"] is JsonArray acceptedValues
            && !TryValidateStringEntries(acceptedValues, $"{path}.acceptedValues", out reason))
        {
            return false;
        }

        if (node["arguments"] is JsonArray arguments)
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                if (arguments[index] is not JsonObject argument)
                {
                    reason = $"OpenCLI artifact has a non-object entry at '{path}.arguments[{index}]'.";
                    return false;
                }

                if (!TryValidateArgumentNode(argument, $"{path}.arguments[{index}]", out reason))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryValidateArgumentNode(JsonObject node, string path, out string? reason)
    {
        reason = null;

        foreach (var arrayProperty in ArgumentArrayProperties)
        {
            if (!TryValidateArrayProperty(node, arrayProperty, path, out reason))
            {
                return false;
            }
        }

        if (node["acceptedValues"] is JsonArray acceptedValues
            && !TryValidateStringEntries(acceptedValues, $"{path}.acceptedValues", out reason))
        {
            return false;
        }

        return true;
    }

    private static bool TryValidateArrayProperty(JsonObject node, string propertyName, string path, out string? reason)
    {
        reason = null;

        if (!node.TryGetPropertyValue(propertyName, out var value))
        {
            return true;
        }

        if (value is null)
        {
            reason = $"OpenCLI artifact has a null '{propertyName}' property at '{path}'.";
            return false;
        }

        if (value is not JsonArray)
        {
            reason = $"OpenCLI artifact has a non-array '{propertyName}' property at '{path}'.";
            return false;
        }

        return true;
    }

    private static bool TryValidateStringEntries(JsonArray array, string path, out string? reason)
    {
        reason = null;

        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] is not JsonValue value || !value.TryGetValue<string>(out _))
            {
                reason = $"OpenCLI artifact has a non-string entry at '{path}[{index}]'.";
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateOptionCollisions(
        IReadOnlyList<JsonObject> optionNodes,
        string path,
        out string? reason)
    {
        reason = null;
        var seenTokens = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < optionNodes.Count; index++)
        {
            var optionPath = $"{path}.options[{index}]";
            foreach (var token in EnumerateOptionTokens(optionNodes[index]))
            {
                if (seenTokens.TryGetValue(token, out var existingPath))
                {
                    reason = $"OpenCLI artifact has a duplicate option token '{token}' at '{optionPath}' colliding with '{existingPath}'.";
                    return false;
                }

                seenTokens[token] = optionPath;
            }
        }

        return true;
    }

    private static IEnumerable<string> EnumerateOptionTokens(JsonObject optionNode)
    {
        var name = GetString(optionNode["name"]);
        if (!string.IsNullOrWhiteSpace(name))
        {
            yield return name.Trim();
        }

        if (optionNode["aliases"] is not JsonArray aliases)
        {
            yield break;
        }

        foreach (var alias in aliases)
        {
            var aliasValue = GetString(alias);
            if (!string.IsNullOrWhiteSpace(aliasValue))
            {
                yield return aliasValue.Trim();
            }
        }
    }

    private static string? GetString(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
}
