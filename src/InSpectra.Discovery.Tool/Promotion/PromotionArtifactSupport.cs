internal static class PromotionArtifactSupport
{
    public static bool SyncOptionalArtifact(string? artifactDirectory, string? artifactName, string destinationPath)
    {
        var hasArtifact = !string.IsNullOrWhiteSpace(artifactName)
            && artifactDirectory is not null
            && File.Exists(Path.Combine(artifactDirectory, artifactName));

        if (hasArtifact)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.Copy(Path.Combine(artifactDirectory!, artifactName!), destinationPath, overwrite: true);
            return true;
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        return false;
    }
}
