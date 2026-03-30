using System.IO.Compression;

internal static class PackageArchivePathSupport
{
    public static string GetArchiveDirectory(string path)
    {
        var normalized = path.Replace('\\', '/');
        var index = normalized.LastIndexOf('/');
        return index >= 0 ? normalized[..index] : string.Empty;
    }

    public static string? NormalizeArchivePath(string baseDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var segments = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseDirectory))
        {
            segments.AddRange(baseDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries));
        }

        foreach (var segment in relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count > 0)
                {
                    segments.RemoveAt(segments.Count - 1);
                }

                continue;
            }

            segments.Add(segment);
        }

        return string.Join("/", segments);
    }

    public static bool IsToolManagedAssembly(
        ZipArchiveEntry entry,
        IReadOnlySet<string> toolDirectories,
        params string[] excludedAssemblyNames)
    {
        if (!entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            && !entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var excludedAssemblyName in excludedAssemblyNames)
        {
            if (string.Equals(entry.Name, excludedAssemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        foreach (var directory in toolDirectories)
        {
            if (entry.FullName.StartsWith(directory + "/", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry.FullName, directory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
