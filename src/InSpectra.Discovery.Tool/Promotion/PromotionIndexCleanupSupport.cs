internal static class PromotionIndexCleanupSupport
{
    public static bool RemoveIndexedVersionArtifacts(string packagesRoot, string packageId, string version)
    {
        var packagesRootPath = Path.GetFullPath(packagesRoot);
        var packageDirectory = Path.GetFullPath(Path.Combine(packagesRootPath, packageId.ToLowerInvariant()));
        var versionDirectory = Path.GetFullPath(Path.Combine(packageDirectory, version.ToLowerInvariant()));

        if (!IsUnderRoot(packagesRootPath, packageDirectory) || !IsUnderRoot(packagesRootPath, versionDirectory))
        {
            throw new InvalidOperationException($"Indexed artifact cleanup resolved outside '{packagesRootPath}'.");
        }

        var removedVersionDirectory = false;
        if (Directory.Exists(versionDirectory))
        {
            Directory.Delete(versionDirectory, recursive: true);
            removedVersionDirectory = true;
        }

        if (!Directory.Exists(packageDirectory))
        {
            return removedVersionDirectory;
        }

        var remainingVersionMetadata = Directory.GetFiles(packageDirectory, "metadata.json", SearchOption.AllDirectories)
            .Any(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase));
        if (!remainingVersionMetadata)
        {
            Directory.Delete(packageDirectory, recursive: true);
            return true;
        }

        return removedVersionDirectory;
    }

    private static bool IsUnderRoot(string rootPath, string candidatePath)
    {
        var normalizedRoot = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = candidatePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
