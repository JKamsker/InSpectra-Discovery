using System.Text.Json.Nodes;

internal static class OpenCliArtifactLoadSupport
{
    public static string? ResolveExistingPath(string repositoryRoot, params string?[] relativePaths)
    {
        foreach (var relativePath in relativePaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidatePath = Path.Combine(repositoryRoot, relativePath!);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return null;
    }

    public static bool TryLoadJsonNode(string path, out JsonNode? document)
    {
        document = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            document = JsonNode.Parse(File.ReadAllText(path));
            return document is not null;
        }
        catch
        {
            document = null;
            return false;
        }
    }

    public static bool TryLoadFirstJsonNode(
        string repositoryRoot,
        IEnumerable<string?> relativePaths,
        out JsonNode? document,
        out string? resolvedPath)
    {
        document = null;
        resolvedPath = null;

        foreach (var candidatePath in EnumerateExistingPaths(repositoryRoot, relativePaths))
        {
            if (!TryLoadJsonNode(candidatePath, out document) || document is null)
            {
                continue;
            }

            resolvedPath = candidatePath;
            return true;
        }

        return false;
    }

    public static bool TryLoadFirstValidOpenCliDocument(
        string repositoryRoot,
        IEnumerable<string?> relativePaths,
        out JsonObject? document,
        out string? resolvedPath)
    {
        document = null;
        resolvedPath = null;

        foreach (var candidatePath in EnumerateExistingPaths(repositoryRoot, relativePaths))
        {
            if (!OpenCliDocumentValidator.TryLoadValidDocument(candidatePath, out document, out _)
                || document is null)
            {
                continue;
            }

            resolvedPath = candidatePath;
            return true;
        }

        return false;
    }

    private static IEnumerable<string> EnumerateExistingPaths(string repositoryRoot, IEnumerable<string?> relativePaths)
        => relativePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(relativePath => Path.Combine(repositoryRoot, relativePath!))
            .Where(File.Exists);
}
