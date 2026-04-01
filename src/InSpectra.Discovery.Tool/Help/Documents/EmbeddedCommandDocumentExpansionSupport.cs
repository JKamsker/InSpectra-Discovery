namespace InSpectra.Discovery.Tool.Help.Documents;

using InSpectra.Discovery.Tool.Help.OpenCli;

internal static class EmbeddedCommandDocumentExpansionSupport
{
    public static IReadOnlyDictionary<string, Document> Expand(
        string rootCommandName,
        IReadOnlyDictionary<string, Document> helpDocuments)
    {
        var expanded = new Dictionary<string, Document>(helpDocuments, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<KeyValuePair<string, Document>>(helpDocuments);

        while (queue.Count > 0)
        {
            var pair = queue.Dequeue();
            foreach (var embedded in pair.Value.EmbeddedCommandDocuments)
            {
                var commandKey = CommandPathSupport.ResolveChildKey(rootCommandName, pair.Key, embedded.Key);
                if (expanded.TryGetValue(commandKey, out var existing)
                    && DocumentInspector.Score(existing) >= DocumentInspector.Score(embedded.Value))
                {
                    continue;
                }

                expanded[commandKey] = embedded.Value;
                queue.Enqueue(new KeyValuePair<string, Document>(commandKey, embedded.Value));
            }
        }

        return expanded;
    }
}
