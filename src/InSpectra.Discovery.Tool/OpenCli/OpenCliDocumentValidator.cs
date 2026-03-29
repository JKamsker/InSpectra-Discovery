using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

internal static partial class OpenCliDocumentValidator
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

        if (!HasPublishableSurface(document))
        {
            reason = "OpenCLI artifact does not expose any commands, options, or arguments.";
            return false;
        }

        if (LooksLikeInventoryOnlyCommandShellDocument(document))
        {
            reason = "OpenCLI artifact only exposes root command inventory shells without any detailed command surface.";
            return false;
        }

        if (ContainsErrorText(document))
        {
            reason = "OpenCLI artifact contains error or exception text in its surface descriptions.";
            return false;
        }

        var totalCommandCount = CountTotalCommands(document);
        if (totalCommandCount > 500)
        {
            reason = $"OpenCLI artifact has an implausible command count ({totalCommandCount}).";
            return false;
        }

        if (ContainsBoxDrawingCommandNames(document))
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
        if (LooksLikeNonPublishableTitle(title))
        {
            reason = "OpenCLI artifact has a non-publishable 'info.title' value.";
            return false;
        }

        var description = GetString(info["description"]);
        if (LooksLikeNonPublishableDescription(description))
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

    private static bool HasPublishableSurface(JsonObject document)
        => HasVisibleItems(document["options"] as JsonArray)
            || HasVisibleItems(document["arguments"] as JsonArray)
            || HasVisibleCommandSurface(document["commands"] as JsonArray);

    private static bool HasVisibleCommandSurface(JsonArray? commands)
    {
        foreach (var command in commands?.OfType<JsonObject>() ?? [])
        {
            if (IsVisible(command)
                || HasVisibleItems(command["options"] as JsonArray)
                || HasVisibleItems(command["arguments"] as JsonArray)
                || HasVisibleCommandSurface(command["commands"] as JsonArray))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasVisibleItems(JsonArray? items)
        => items?.OfType<JsonObject>().Any(IsVisible) == true;

    private static bool IsVisible(JsonObject node)
        => node["hidden"]?.GetValue<bool?>() != true;

    private static bool LooksLikeInventoryOnlyCommandShellDocument(JsonObject document)
    {
        if (!string.Equals(GetArtifactSource(document), "crawled-from-help", StringComparison.Ordinal)
            || GetHelpDocumentCount(document) > 1
            || HasVisibleItems(document["options"] as JsonArray)
            || HasVisibleItems(document["arguments"] as JsonArray)
            || document["commands"] is not JsonArray commands)
        {
            return false;
        }

        var nonAuxiliaryCommands = commands
            .OfType<JsonObject>()
            .Where(command => !IsBuiltinAuxiliaryCommand(command))
            .ToArray();
        if (nonAuxiliaryCommands.Length == 0)
        {
            return false;
        }

        return nonAuxiliaryCommands.All(IsPlaceholderCommandShell);
    }

    private static bool IsPlaceholderCommandShell(JsonObject command)
        => !HasVisibleItems(command["options"] as JsonArray)
            && !HasVisibleItems(command["arguments"] as JsonArray)
            && !HasVisibleCommandSurface(command["commands"] as JsonArray);

    private static bool IsBuiltinAuxiliaryCommand(JsonObject command)
    {
        var name = GetString(command["name"]);
        return string.Equals(name, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "version", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetArtifactSource(JsonObject document)
        => document["x-inspectra"] is JsonObject inspectra
            ? GetString(inspectra["artifactSource"])
            : null;

    private static int GetHelpDocumentCount(JsonObject document)
    {
        if (document["x-inspectra"] is not JsonObject inspectra
            || inspectra["helpDocumentCount"] is not JsonValue countValue)
        {
            return int.MaxValue;
        }

        return countValue.TryGetValue<int>(out var count) ? count : int.MaxValue;
    }

    private static bool LooksLikeNonPublishableTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var trimmed = title.Trim();
        return trimmed.Length > 120
            || trimmed.Contains('\n', StringComparison.Ordinal)
            || trimmed.Contains(". ", StringComparison.Ordinal)
            || TitleNoiseRegex().IsMatch(trimmed)
            || PathOrUrlRegex().IsMatch(trimmed);
    }

    private static bool LooksLikeNonPublishableDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var trimmed = description.Trim();
        return DescriptionNoiseRegex().IsMatch(trimmed)
            || trimmed.Contains("\n   at ", StringComparison.Ordinal)
            || trimmed.Contains("\nat ", StringComparison.Ordinal)
            || trimmed.Contains("/tmp/inspectra-help-", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("/usr/share/dotnet/", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountTotalCommands(JsonObject node)
    {
        var count = 0;
        if (node["commands"] is JsonArray commands)
        {
            count += commands.Count;
            foreach (var command in commands.OfType<JsonObject>())
            {
                count += CountTotalCommands(command);
                if (count > 500)
                {
                    return count;
                }
            }
        }

        return count;
    }

    private static bool ContainsBoxDrawingCommandNames(JsonObject node)
    {
        if (node["commands"] is not JsonArray commands)
        {
            return false;
        }

        foreach (var command in commands.OfType<JsonObject>())
        {
            var name = GetString(command["name"]);
            if (name is not null && name.Any(ch => ch is '│' or '┌' or '┐' or '└' or '┘' or '├' or '┤' or '┬' or '┴' or '┼' or '─' or '═' or '║'))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsErrorText(JsonObject document)
    {
        var texts = new List<string>();
        CollectTextFields(document, texts, depth: 0);
        return texts.Any(LooksLikeErrorText);
    }

    private static void CollectTextFields(JsonObject node, List<string> texts, int depth)
    {
        if (depth > 5)
        {
            return;
        }

        foreach (var property in node)
        {
            if (property.Value is JsonValue value && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text)
                && property.Key is "description" or "name")
            {
                texts.Add(text);
            }
            else if (property.Value is JsonObject child)
            {
                CollectTextFields(child, texts, depth + 1);
            }
            else if (property.Value is JsonArray array)
            {
                foreach (var element in array)
                {
                    if (element is JsonObject arrayChild)
                    {
                        CollectTextFields(arrayChild, texts, depth + 1);
                    }
                }
            }
        }
    }

    private static bool LooksLikeErrorText(string text)
        => ErrorTextRegex().IsMatch(text);

    private static string? GetString(JsonNode? node)
        => node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;

    [GeneratedRegex(@"^(?:usage\b|version:|help:|unhandled exception\b|unexpected argument\b|invalid arguments?\b|now listening on\b|application started\b|hosting failed to start\b|starting\s+\w+\b|missing\s+\w+\b|\d{4}-\d{2}-\d{2}[T ]|\[(?:info|error|warn|information|debug|fatal)\]|(?:fail|error|info|warn|dbug|crit):\s)|\b(?:Unhandled exception|Unexpected argument|Invalid arguments|Now listening on|Application started|Hosting failed to start)\b|\bDefaulting to\b.*\brequires\b.+\bruntime\b|\bvia:\b.+(?:--|/)", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex TitleNoiseRegex();

    [GeneratedRegex(@"https?://|[A-Za-z]:\\|/tmp/|/usr/|\.dll\b", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PathOrUrlRegex();

    [GeneratedRegex(@"\b(?:System\.\w+Exception|Unhandled\s+[Ee]xception|FileNotFoundException|ArgumentNullException|NullReferenceException|StackOverflowException|InvalidOperationException)\b|^\s*at\s+\S+\.\S+\(|^\s*---\s*>", RegexOptions.Compiled | RegexOptions.Multiline)]
    private static partial Regex ErrorTextRegex();

    [GeneratedRegex(@"Unhandled exception\b|Hosting failed to start\b|Now listening on:|Application started\.|Microsoft\.Hosting\.Lifetime|System\.[A-Za-z]+Exception\b|Traceback \(most recent call last\):|Press any key to exit|Cannot read keys when either application does not have a console|You must install or update \.NET|A fatal error was encountered|It was not possible to find any compatible framework version|required to execute the application was not found|\bMCP\b.*\btransport\b|\btransport\b.*\bMCP\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex DescriptionNoiseRegex();
}
