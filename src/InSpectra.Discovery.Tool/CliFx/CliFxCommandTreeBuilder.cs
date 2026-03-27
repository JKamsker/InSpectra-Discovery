internal sealed class CliFxCommandTreeBuilder
{
    public IReadOnlyList<CliFxCommandNode> Build(
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands,
        IReadOnlyDictionary<string, CliFxHelpDocument> helpDocuments)
    {
        var knownCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var edges = new Dictionary<string, List<CliFxCommandNode>>(StringComparer.OrdinalIgnoreCase);
        var edgeKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in helpDocuments)
        {
            foreach (var child in pair.Value.Commands)
            {
                var childFullName = string.IsNullOrWhiteSpace(pair.Key) ? child.Key : $"{pair.Key} {child.Key}";
                knownCommands.Add(childFullName);
                AddEdge(edges, edgeKeys, pair.Key, new CliFxCommandNode(childFullName, child.Key, child.Description));
            }
        }

        foreach (var commandName in staticCommands.Keys.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            knownCommands.Add(commandName);
        }

        foreach (var commandName in helpDocuments.Keys.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            knownCommands.Add(commandName);
        }

        foreach (var commandName in knownCommands.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            var parentName = FindParent(commandName, knownCommands);
            var displayName = string.IsNullOrWhiteSpace(parentName)
                ? commandName
                : commandName[(parentName.Length + 1)..];
            var description = ResolveDescription(commandName, staticCommands, helpDocuments);
            AddEdge(edges, edgeKeys, parentName, new CliFxCommandNode(commandName, displayName, description));
        }

        return BuildNodes(string.Empty, edges);
    }

    private static void AddEdge(
        IDictionary<string, List<CliFxCommandNode>> edges,
        IDictionary<string, HashSet<string>> edgeKeys,
        string? parentName,
        CliFxCommandNode node)
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

    private static string? ResolveDescription(
        string commandName,
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands,
        IReadOnlyDictionary<string, CliFxHelpDocument> helpDocuments)
    {
        if (helpDocuments.TryGetValue(commandName, out var helpDocument)
            && !string.IsNullOrWhiteSpace(helpDocument.CommandDescription))
        {
            return helpDocument.CommandDescription;
        }

        return staticCommands.TryGetValue(commandName, out var command)
            ? command.Description
            : null;
    }

    private static IReadOnlyList<CliFxCommandNode> BuildNodes(
        string parentName,
        IReadOnlyDictionary<string, List<CliFxCommandNode>> edges)
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

internal sealed record CliFxCommandNode(
    string FullName,
    string DisplayName,
    string? Description)
{
    public IReadOnlyList<CliFxCommandNode> Children { get; init; } = [];
}
