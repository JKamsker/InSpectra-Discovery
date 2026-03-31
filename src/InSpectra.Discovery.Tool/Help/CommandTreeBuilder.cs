namespace InSpectra.Discovery.Tool.Help;

internal sealed class CommandTreeBuilder
{
    private readonly OpenCliCommandTreeBuilder _commandTreeBuilder = new();

    public IReadOnlyList<CommandNode> Build(string commandName, IReadOnlyDictionary<string, Document> helpDocuments)
    {
        var commands = new List<OpenCliCommandDescriptor>();

        foreach (var pair in helpDocuments)
        {
            if (DocumentInspector.IsBuiltinAuxiliaryInventoryEcho(pair.Key, pair.Value))
            {
                continue;
            }

            foreach (var child in pair.Value.Commands)
            {
                var childFullName = CommandPathSupport.ResolveChildKey(commandName, pair.Key, child.Key);
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

    private static CommandNode ConvertNode(OpenCliCommandTreeNode node)
        => new(node.FullName, node.DisplayName, node.Description)
        {
            Children = node.Children.Select(ConvertNode).ToArray(),
        };
}

