namespace InSpectra.Discovery.Tool.OpenCli.Structure;

using InSpectra.Discovery.Tool.OpenCli.Options;

using System.Text.Json.Nodes;

internal static class OpenCliNodeValidationSupport
{
    private const int MaxCommandPathRecurrence = 3;
    private static readonly string[] CommandLikeArrayProperties = ["arguments", "commands", "examples", "options"];
    private static readonly string[] OptionArrayProperties = ["acceptedValues", "aliases", "arguments", "metadata"];
    private static readonly string[] ArgumentArrayProperties = ["acceptedValues", "metadata"];

    public static bool TryValidateCommandLikeNode(JsonObject node, string path, bool isRoot, out string? reason)
        => TryValidateCommandLikeNode(node, path, isRoot, [], out reason);

    private static bool TryValidateCommandLikeNode(
        JsonObject node,
        string path,
        bool isRoot,
        IReadOnlyList<string> ancestorCommandNames,
        out string? reason)
    {
        reason = null;

        foreach (var arrayProperty in CommandLikeArrayProperties)
        {
            if (!OpenCliValidationSupport.TryValidateArrayProperty(node, arrayProperty, path, out reason))
            {
                return false;
            }
        }

        if (node["examples"] is JsonArray examples
            && !OpenCliValidationSupport.TryValidateStringEntries(examples, $"{path}.examples", out reason))
        {
            return false;
        }

        if (!isRoot && string.Equals(OpenCliValidationSupport.GetString(node["name"]), "__default_command", StringComparison.Ordinal))
        {
            reason = $"OpenCLI artifact contains a '__default_command' node at '{path}'.";
            return false;
        }

        var commandName = GetValidatedCommandName(node, path, isRoot, ancestorCommandNames, out reason);
        if (reason is not null)
        {
            return false;
        }

        var childAncestorCommandNames = commandName is null
            ? ancestorCommandNames
            : [.. ancestorCommandNames, commandName];

        if (!TryValidateOptions(node["options"] as JsonArray, path, out reason))
        {
            return false;
        }

        if (!TryValidateArguments(node["arguments"] as JsonArray, $"{path}.arguments", out reason))
        {
            return false;
        }

        if (!TryValidateCommands(node["commands"] as JsonArray, $"{path}.commands", childAncestorCommandNames, out reason))
        {
            return false;
        }

        return true;
    }

    private static bool TryValidateOptions(JsonArray? options, string path, out string? reason)
    {
        reason = null;
        if (options is null)
        {
            return true;
        }

        var seenTokens = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < options.Count; index++)
        {
            var optionPath = $"{path}.options[{index}]";
            if (options[index] is not JsonObject option)
            {
                reason = $"OpenCLI artifact has a non-object entry at '{optionPath}'.";
                return false;
            }

            if (!TryValidateOptionNode(option, optionPath, seenTokens, out reason))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateCommands(
        JsonArray? commands,
        string pathPrefix,
        IReadOnlyList<string> ancestorCommandNames,
        out string? reason)
    {
        reason = null;
        if (commands is null)
        {
            return true;
        }

        for (var index = 0; index < commands.Count; index++)
        {
            var commandPath = $"{pathPrefix}[{index}]";
            if (commands[index] is not JsonObject command)
            {
                reason = $"OpenCLI artifact has a non-object entry at '{commandPath}'.";
                return false;
            }

            if (!TryValidateCommandLikeNode(command, commandPath, isRoot: false, ancestorCommandNames, out reason))
            {
                return false;
            }
        }

        return true;
    }

    private static string? GetValidatedCommandName(
        JsonObject node,
        string path,
        bool isRoot,
        IReadOnlyList<string> ancestorCommandNames,
        out string? reason)
    {
        reason = null;
        if (isRoot)
        {
            return null;
        }

        var commandName = OpenCliValidationSupport.GetString(node["name"])?.Trim();
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        if (!OpenCliNameValidationSupport.TryValidateCommandName(commandName, path, out reason))
        {
            return null;
        }

        var recurrenceCount = CountCommandNameOccurrences(ancestorCommandNames, commandName) + 1;
        if (recurrenceCount <= MaxCommandPathRecurrence)
        {
            return commandName;
        }

        reason = $"OpenCLI artifact repeats command name '{commandName}' more than {MaxCommandPathRecurrence} times within the same command path at '{path}'.";
        return null;
    }

    private static int CountCommandNameOccurrences(IReadOnlyList<string> commandNames, string candidate)
    {
        var count = 0;
        foreach (var commandName in commandNames)
        {
            if (string.Equals(commandName, candidate, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryValidateOptionNode(
        JsonObject node,
        string path,
        IDictionary<string, string> seenTokens,
        out string? reason)
    {
        reason = null;

        foreach (var arrayProperty in OptionArrayProperties)
        {
            if (!OpenCliValidationSupport.TryValidateArrayProperty(node, arrayProperty, path, out reason))
            {
                return false;
            }
        }

        if (node["aliases"] is JsonArray aliases
            && !OpenCliValidationSupport.TryValidateStringEntries(aliases, $"{path}.aliases", out reason))
        {
            return false;
        }

        if (node["acceptedValues"] is JsonArray acceptedValues
            && !OpenCliValidationSupport.TryValidateStringEntries(acceptedValues, $"{path}.acceptedValues", out reason))
        {
            return false;
        }

        if (!TryValidateArguments(node["arguments"] as JsonArray, $"{path}.arguments", out reason))
        {
            return false;
        }

        foreach (var token in OpenCliOptionTokenValidationSupport.EnumerateOptionTokens(node))
        {
            if (seenTokens.TryGetValue(token, out var existingPath))
            {
                reason = $"OpenCLI artifact has a duplicate option token '{token}' at '{path}' colliding with '{existingPath}'.";
                return false;
            }

            seenTokens[token] = path;
        }

        return true;
    }

    private static bool TryValidateArguments(JsonArray? arguments, string pathPrefix, out string? reason)
    {
        reason = null;
        if (arguments is null)
        {
            return true;
        }

        for (var index = 0; index < arguments.Count; index++)
        {
            var argumentPath = $"{pathPrefix}[{index}]";
            if (arguments[index] is not JsonObject argument)
            {
                reason = $"OpenCLI artifact has a non-object entry at '{argumentPath}'.";
                return false;
            }

            if (!TryValidateArgumentNode(argument, argumentPath, out reason))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryValidateArgumentNode(JsonObject node, string path, out string? reason)
    {
        reason = null;

        if (!OpenCliNameValidationSupport.TryValidateArgumentName(
                OpenCliValidationSupport.GetString(node["name"]),
                path,
                out reason))
        {
            return false;
        }

        foreach (var arrayProperty in ArgumentArrayProperties)
        {
            if (!OpenCliValidationSupport.TryValidateArrayProperty(node, arrayProperty, path, out reason))
            {
                return false;
            }
        }

        if (node["acceptedValues"] is JsonArray acceptedValues
            && !OpenCliValidationSupport.TryValidateStringEntries(acceptedValues, $"{path}.acceptedValues", out reason))
        {
            return false;
        }

        return true;
    }
}
