using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class OpenCliDocumentSynthesizer
{
    public static JsonObject ConvertFromXmldoc(XDocument xmlDocument, string title, string version)
    {
        var rootCommands = OpenCliXmldocSupport.GetElements(xmlDocument.Root, "Command")
            .Select(command => ConvertCommand(command, []))
            .ToList();

        JsonNode? defaultOptions = null;
        JsonNode? defaultArguments = null;
        JsonNode? defaultDescription = null;
        var visibleRootCommands = new JsonArray();
        foreach (var command in rootCommands)
        {
            if (string.Equals(command["name"]?.GetValue<string>(), "__default_command", StringComparison.Ordinal))
            {
                defaultOptions = command["options"]?.DeepClone();
                defaultArguments = command["arguments"]?.DeepClone();
                defaultDescription = command["description"]?.DeepClone();
                continue;
            }

            visibleRootCommands.Add(command);
        }

        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = new JsonObject
            {
                ["title"] = title,
                ["version"] = version,
            },
            ["x-inspectra"] = new JsonObject
            {
                ["synthesized"] = true,
                ["artifactSource"] = "synthesized-from-xmldoc",
                ["sourceArtifact"] = "xmldoc.xml",
                ["generator"] = "InSpectra.Discovery",
            },
            ["commands"] = visibleRootCommands,
        };

        OpenCliXmldocSupport.AddIfPresent(document, "options", defaultOptions);
        OpenCliXmldocSupport.AddIfPresent(document, "arguments", defaultArguments);
        OpenCliXmldocSupport.AddIfPresent(document["info"]!.AsObject(), "description", defaultDescription);
        return OpenCliDocumentSanitizer.Sanitize(document);
    }

    private static JsonObject ConvertCommand(XElement commandNode, IReadOnlyList<string> parentPath)
    {
        var commandName = OpenCliXmldocSupport.NormalizeCommandName(commandNode);
        var commandPath = parentPath.Concat([commandName]).ToArray();
        var command = new JsonObject
        {
            ["name"] = commandName,
        };

        var parametersNode = OpenCliXmldocSupport.GetElements(commandNode, "Parameters").FirstOrDefault();
        var options = OpenCliXmldocInputBuilder.ConvertOptions(parametersNode);
        if (options.Count > 0)
        {
            command["options"] = options;
        }

        var arguments = OpenCliXmldocInputBuilder.ConvertArguments(parametersNode);
        if (arguments.Count > 0)
        {
            command["arguments"] = arguments;
        }

        var description = OpenCliXmldocSupport.GetDescriptionText(commandNode);
        if (!string.IsNullOrWhiteSpace(description))
        {
            command["description"] = description;
        }

        var children = new JsonArray();
        foreach (var child in OpenCliXmldocSupport.GetElements(commandNode, "Command"))
        {
            if (OpenCliXmldocSupport.IsDefaultCommand(child))
            {
                HoistDefaultCommand(command, children, child, commandPath);
                continue;
            }

            children.Add(ConvertCommand(child, commandPath));
        }

        if (children.Count > 0)
        {
            command["commands"] = children;
        }

        command["hidden"] = string.Equals(commandName, "__default_command", StringComparison.Ordinal)
            || OpenCliXmldocSupport.GetBoolean(
                commandNode,
                "Hidden",
                OpenCliXmldocSupport.GetBoolean(commandNode, "IsHidden"));
        var examples = OpenCliXmldocExampleBuilder.ConvertExamples(commandNode, commandPath);
        if (examples.Count > 0)
        {
            command["examples"] = examples;
        }

        return command;
    }

    private static void HoistDefaultCommand(
        JsonObject command,
        JsonArray childCommands,
        XElement defaultChild,
        IReadOnlyList<string> commandPath)
    {
        var parametersNode = OpenCliXmldocSupport.GetElements(defaultChild, "Parameters").FirstOrDefault();
        MergeArrayProperty(command, "options", OpenCliXmldocInputBuilder.ConvertOptions(parametersNode));
        MergeArrayProperty(command, "arguments", OpenCliXmldocInputBuilder.ConvertArguments(parametersNode));

        if (command["description"] is null)
        {
            var description = OpenCliXmldocSupport.GetDescriptionText(defaultChild);
            if (!string.IsNullOrWhiteSpace(description))
            {
                command["description"] = description;
            }
        }

        MergeArrayProperty(
            command,
            "examples",
            OpenCliXmldocExampleBuilder.ConvertExamples(defaultChild, commandPath, treatDefaultCommandAsParent: true));

        foreach (var nestedChild in OpenCliXmldocSupport.GetElements(defaultChild, "Command"))
        {
            if (OpenCliXmldocSupport.IsDefaultCommand(nestedChild))
            {
                HoistDefaultCommand(command, childCommands, nestedChild, commandPath);
                continue;
            }

            childCommands.Add(ConvertCommand(nestedChild, commandPath));
        }
    }

    private static void MergeArrayProperty(JsonObject target, string propertyName, JsonArray additions)
    {
        if (additions.Count == 0)
        {
            return;
        }

        var targetArray = target[propertyName] as JsonArray;
        if (targetArray is null)
        {
            targetArray = new JsonArray();
            target[propertyName] = targetArray;
        }

        foreach (var addition in additions)
        {
            targetArray.Add(addition?.DeepClone());
        }
    }
}
