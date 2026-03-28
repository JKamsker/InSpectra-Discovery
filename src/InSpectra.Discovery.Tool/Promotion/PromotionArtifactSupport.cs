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
        document = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            document = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            return document is not null;
        }
        catch
        {
            document = null;
            return false;
        }
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

    private static bool IsWithinDirectory(string directoryPath, string candidatePath)
    {
        var normalizedDirectory = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(candidatePath, normalizedDirectory, StringComparison.OrdinalIgnoreCase)
            || candidatePath.StartsWith(normalizedDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || candidatePath.StartsWith(normalizedDirectory + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
