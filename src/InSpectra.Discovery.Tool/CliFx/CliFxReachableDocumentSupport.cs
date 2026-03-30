internal static class CliFxReachableDocumentSupport
{
    public static Dictionary<string, CliFxHelpDocument> BuildReachableDocuments(
        IReadOnlyDictionary<string, CliFxHelpDocument> parsedHelpDocuments,
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands)
    {
        var documents = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };

        if (parsedHelpDocuments.TryGetValue(string.Empty, out var rootDocument))
        {
            documents[string.Empty] = rootDocument;
        }

        queue.Enqueue(string.Empty);
        while (queue.Count > 0)
        {
            var commandKey = queue.Dequeue();
            foreach (var childKey in EnumerateReachableChildKeys(commandKey, parsedHelpDocuments, staticCommands))
            {
                if (!seen.Add(childKey))
                {
                    continue;
                }

                if (parsedHelpDocuments.TryGetValue(childKey, out var childDocument))
                {
                    documents[childKey] = childDocument;
                }

                queue.Enqueue(childKey);
            }
        }

        return documents;
    }

    private static IEnumerable<string> EnumerateReachableChildKeys(
        string commandKey,
        IReadOnlyDictionary<string, CliFxHelpDocument> parsedHelpDocuments,
        IReadOnlyDictionary<string, CliFxCommandDefinition> staticCommands)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (parsedHelpDocuments.TryGetValue(commandKey, out var document))
        {
            foreach (var child in document.Commands)
            {
                var childKey = string.IsNullOrWhiteSpace(commandKey) ? child.Key : $"{commandKey} {child.Key}";
                if (yielded.Add(childKey))
                {
                    yield return childKey;
                }
            }
        }

        foreach (var childKey in EnumerateStaticChildKeys(commandKey, staticCommands.Keys))
        {
            if (yielded.Add(childKey))
            {
                yield return childKey;
            }
        }
    }

    private static IEnumerable<string> EnumerateStaticChildKeys(string commandKey, IEnumerable<string> staticCommandKeys)
    {
        var parentSegments = string.IsNullOrWhiteSpace(commandKey)
            ? []
            : commandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var prefix = parentSegments.Length == 0 ? string.Empty : $"{commandKey} ";

        foreach (var staticCommandKey in staticCommandKeys.Where(key => !string.IsNullOrWhiteSpace(key)))
        {
            string[] segments;
            if (parentSegments.Length == 0)
            {
                segments = staticCommandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (segments.Length == 0)
                {
                    continue;
                }

                yield return segments[0];
                continue;
            }

            if (!staticCommandKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            segments = staticCommandKey.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (segments.Length <= parentSegments.Length)
            {
                continue;
            }

            yield return string.Join(' ', segments.Take(parentSegments.Length + 1));
        }
    }
}
