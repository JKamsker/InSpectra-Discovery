internal sealed class ToolHelpCommandTreeBuilder
{
    public IReadOnlyList<ToolHelpCommandNode> Build(string commandName, IReadOnlyDictionary<string, ToolHelpDocument> helpDocuments)
    {
        var knownCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inferredDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var edges = new Dictionary<string, List<ToolHelpCommandNode>>(StringComparer.OrdinalIgnoreCase);
        var edgeKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in helpDocuments)
        {
            if (ToolHelpDocumentInspector.IsBuiltinAuxiliaryInventoryEcho(pair.Key, pair.Value))
            {
                continue;
            }

            foreach (var child in pair.Value.Commands)
            {
                var childFullName = ToolHelpCommandPathSupport.ResolveChildKey(commandName, pair.Key, child.Key);
                AddCommandAndPrefixes(knownCommands, childFullName);
                RememberDescription(inferredDescriptions, childFullName, child.Description);
            }
        }

        foreach (var knownCommand in helpDocuments.Keys.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            AddCommandAndPrefixes(knownCommands, knownCommand);
        }

        foreach (var knownCommand in knownCommands.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            var parentName = FindParent(knownCommand, knownCommands);
            var displayName = string.IsNullOrWhiteSpace(parentName)
                ? knownCommand
                : knownCommand[(parentName.Length + 1)..];
            var description = helpDocuments.TryGetValue(knownCommand, out var helpDocument)
                && !string.IsNullOrWhiteSpace(helpDocument.CommandDescription)
                ? helpDocument.CommandDescription
                : inferredDescriptions.GetValueOrDefault(knownCommand);
            AddEdge(edges, edgeKeys, parentName, new ToolHelpCommandNode(knownCommand, displayName, description));
        }

        return BuildNodes(string.Empty, edges);
    }

    private static void RememberDescription(
        IDictionary<string, string> descriptions,
        string commandName,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(commandName) || string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        if (!descriptions.TryGetValue(commandName, out var existingDescription)
            || description.Length > existingDescription.Length)
        {
            descriptions[commandName] = description.Trim();
        }
    }

    private static void AddEdge(
        IDictionary<string, List<ToolHelpCommandNode>> edges,
        IDictionary<string, HashSet<string>> edgeKeys,
        string? parentName,
        ToolHelpCommandNode node)
    {
        var normalizedParent = parentName ?? string.Empty;
        if (!edgeKeys.TryGetValue(normalizedParent, out var knownChildren))
        {
            knownChildren = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            edgeKeys[normalizedParent] = knownChildren;
        }

        if (!knownChildren.Add(node.FullName))
        {
            return;
        }

        if (!edges.TryGetValue(normalizedParent, out var children))
        {
            children = [];
            edges[normalizedParent] = children;
        }

        children.Add(node);
    }

    private static string FindParent(string commandName, IReadOnlySet<string> knownCommands)
    {
        var segments = commandName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var length = segments.Length - 1; length > 0; length--)
        {
            var candidate = string.Join(' ', segments.Take(length));
            if (knownCommands.Contains(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static void AddCommandAndPrefixes(ISet<string> knownCommands, string? commandName)
    {
        if (string.IsNullOrWhiteSpace(commandName))
        {
            return;
        }

        var segments = commandName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var length = 1; length <= segments.Length; length++)
        {
            knownCommands.Add(string.Join(' ', segments.Take(length)));
        }
    }

    private static IReadOnlyList<ToolHelpCommandNode> BuildNodes(
        string parentName,
        IReadOnlyDictionary<string, List<ToolHelpCommandNode>> edges)
    {
        if (!edges.TryGetValue(parentName, out var children))
        {
            return [];
        }

        return children
            .Select(child => child with { Children = BuildNodes(child.FullName, edges) })
            .ToArray();
    }
}

internal sealed record ToolHelpCommandNode(
    string FullName,
    string DisplayName,
    string? Description)
{
    public IReadOnlyList<ToolHelpCommandNode> Children { get; init; } = [];
}
