internal sealed class ToolHelpCommandTreeBuilder
{
    private readonly OpenCliCommandTreeBuilder _commandTreeBuilder = new();

    public IReadOnlyList<ToolHelpCommandNode> Build(string commandName, IReadOnlyDictionary<string, ToolHelpDocument> helpDocuments)
    {
        var commands = new List<OpenCliCommandDescriptor>();

        foreach (var pair in helpDocuments)
        {
            if (ToolHelpDocumentInspector.IsBuiltinAuxiliaryInventoryEcho(pair.Key, pair.Value))
            {
                continue;
            }

            foreach (var child in pair.Value.Commands)
            {
                var childFullName = ToolHelpCommandPathSupport.ResolveChildKey(commandName, pair.Key, child.Key);
                commands.Add(new OpenCliCommandDescriptor(childFullName, child.Description));
            }
        }
        foreach (var pair in helpDocuments.Where(pair => !string.IsNullOrWhiteSpace(pair.Key)))
        {
            commands.Add(new OpenCliCommandDescriptor(pair.Key, pair.Value.CommandDescription));
        }

        return _commandTreeBuilder
            .Build(commands)
            .Select(ConvertNode)
            .ToArray();
    }

    private static ToolHelpCommandNode ConvertNode(OpenCliCommandTreeNode node)
        => new(node.FullName, node.DisplayName, node.Description)
        {
            Children = node.Children.Select(ConvertNode).ToArray(),
        };
}

internal sealed record ToolHelpCommandNode(
    string FullName,
    string DisplayName,
    string? Description)
{
    public IReadOnlyList<ToolHelpCommandNode> Children { get; init; } = [];
}
