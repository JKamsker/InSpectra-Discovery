internal static class ToolHelpReachableDocumentSupport
{
    public static Dictionary<string, ToolHelpDocument> BuildReachableDocuments(
        string rootCommandName,
        IReadOnlyDictionary<string, ToolHelpDocument> parsedCaptures)
    {
        var documents = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase);
        if (!parsedCaptures.TryGetValue(string.Empty, out var rootDocument))
        {
            return documents;
        }

        var queue = new Queue<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
        documents[string.Empty] = rootDocument;
        queue.Enqueue(string.Empty);

        while (queue.Count > 0)
        {
            var commandKey = queue.Dequeue();
            var current = documents[commandKey];
            if (ToolHelpDocumentInspector.IsBuiltinAuxiliaryInventoryEcho(commandKey, current))
            {
                continue;
            }

            foreach (var child in current.Commands)
            {
                var childKey = ToolHelpCommandPathSupport.ResolveChildKey(rootCommandName, commandKey, child.Key);
                if (!seen.Add(childKey) || !parsedCaptures.TryGetValue(childKey, out var childDocument))
                {
                    continue;
                }

                documents[childKey] = childDocument;
                queue.Enqueue(childKey);
            }
        }

        return documents;
    }
}
