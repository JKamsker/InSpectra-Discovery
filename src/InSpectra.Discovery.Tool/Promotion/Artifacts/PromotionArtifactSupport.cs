namespace InSpectra.Discovery.Tool.Promotion.Artifacts;

using InSpectra.Discovery.Tool.Infrastructure.Json;

using System.Text.Json.Nodes;

internal static class PromotionArtifactSupport
{
    public static string? ResolveOptionalArtifactPath(string? artifactDirectory, string? artifactName)
    {
        if (string.IsNullOrWhiteSpace(artifactDirectory) || string.IsNullOrWhiteSpace(artifactName))
        {
            return null;
        }

        var rootPath = Path.GetFullPath(artifactDirectory);
        var candidatePath = Path.GetFullPath(Path.Combine(rootPath, artifactName));
        if (!IsWithinDirectory(rootPath, candidatePath) || !File.Exists(candidatePath))
        {
            return null;
        }

        return candidatePath;
    }

    public static bool TryLoadJsonObject(string path, out JsonObject? document)
    {
        document = JsonNodeFileLoader.TryLoadJsonObject(path);
        return document is not null;
    }

    public static bool SyncOptionalArtifact(string? artifactDirectory, string? artifactName, string destinationPath)
    {
        var sourcePath = ResolveOptionalArtifactPath(artifactDirectory, artifactName);

        if (sourcePath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(sourcePath, destinationPath, overwrite: true);
            return true;
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        return false;
    }

    public static bool HasSameOptionalArtifactContent(string? artifactDirectory, string? artifactName, string destinationPath)
    {
        var sourcePath = ResolveOptionalArtifactPath(artifactDirectory, artifactName);
        if (sourcePath is null)
        {
            return !File.Exists(destinationPath);
        }

        if (!File.Exists(destinationPath))
        {
            return false;
        }

        return File.ReadAllBytes(sourcePath).AsSpan().SequenceEqual(File.ReadAllBytes(destinationPath));
    }

    public static bool HasSameJsonObjectContent(string path, JsonObject? document)
    {
        if (document is null)
        {
            return !File.Exists(path);
        }

        if (!File.Exists(path))
        {
            return false;
        }

        return JsonNode.DeepEquals(JsonNodeFileLoader.TryLoadJsonObject(path), document);
    }

    public static bool HasSameTextContent(string path, string? content)
    {
        if (content is null)
        {
            return !File.Exists(path);
        }

        if (!File.Exists(path))
        {
            return false;
        }

        return string.Equals(File.ReadAllText(path), content, StringComparison.Ordinal);
    }

    private static bool IsWithinDirectory(string directoryPath, string candidatePath)
    {
        var normalizedDirectory = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(candidatePath, normalizedDirectory, StringComparison.OrdinalIgnoreCase)
            || candidatePath.StartsWith(normalizedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || candidatePath.StartsWith(normalizedDirectory + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}

