using System.Text.Json.Nodes;

internal sealed class CliFxOpenCliBuilder
{
    private readonly CliFxCommandTreeBuilder _commandTreeBuilder = new();
    private readonly CliFxOpenCliOptionBuilder _optionBuilder = new();
    private readonly CliFxOpenCliArgumentBuilder _argumentBuilder = new();

    public JsonObject Build(
        string commandName,
        string packageVersion,
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands,
        IReadOnlyDictionary<string, CliFxHelpDocument> helpDocuments)
    {
        helpDocuments.TryGetValue(string.Empty, out var rootHelp);
        staticCommands.TryGetValue(string.Empty, out var defaultCommand);

        var document = new JsonObject
        {
            ["opencli"] = "0.1-draft",
            ["info"] = BuildInfoNode(commandName, packageVersion, rootHelp, defaultCommand),
            ["x-inspectra"] = new JsonObject
            {
                ["artifactSource"] = "crawled-from-clifx-help",
                ["generator"] = "InSpectra.Discovery",
                ["metadataEnriched"] = staticCommands.Count > 0,
                ["helpDocumentCount"] = helpDocuments.Count,
            },
            ["commands"] = BuildRootCommands(staticCommands, helpDocuments),
        };

        CliFxOpenCliNodeSupport.AddIfPresent(document, "options", _optionBuilder.BuildOptions(defaultCommand, rootHelp));
        CliFxOpenCliNodeSupport.AddIfPresent(document, "arguments", _argumentBuilder.BuildArguments(defaultCommand, rootHelp));
        return OpenCliDocumentSanitizer.Sanitize(document);
    }

    private JsonArray BuildRootCommands(
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands,
        IReadOnlyDictionary<string, CliFxHelpDocument> helpDocuments)
        => new(_commandTreeBuilder
            .Build(staticCommands, helpDocuments)
            .Select(child => BuildCommandNode(child, staticCommands, helpDocuments))
            .ToArray());

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

    private JsonObject BuildCommandPayload(
        string name,
        CliFxCommandDefinition? command,
        CliFxHelpDocument? helpDocument,
        string? description)
    {
        var node = new JsonObject
        {
            ["name"] = name,
            ["hidden"] = false,
        };
        CliFxOpenCliNodeSupport.AddIfPresent(node, "description", description);
        CliFxOpenCliNodeSupport.AddIfPresent(node, "options", _optionBuilder.BuildOptions(command, helpDocument));
        CliFxOpenCliNodeSupport.AddIfPresent(node, "arguments", _argumentBuilder.BuildArguments(command, helpDocument));
        return node;
    }

    private static JsonObject BuildInfoNode(
        string commandName,
        string packageVersion,
        CliFxHelpDocument? rootHelp,
        CliFxCommandDefinition? defaultCommand)
    {
        var info = new JsonObject
        {
            ["title"] = rootHelp?.Title ?? commandName,
            ["version"] = rootHelp?.Version ?? packageVersion,
        };
        CliFxOpenCliNodeSupport.AddIfPresent(
            info,
            "description",
            rootHelp?.ApplicationDescription ?? defaultCommand?.Description);
        return info;
    }
}
