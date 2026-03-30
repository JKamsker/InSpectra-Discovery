internal sealed record ArtifactRegenerationScope(string? PackageId, string? Version)
{
    public static ArtifactRegenerationScope All { get; } = new(null, null);

    public bool HasPackageId => !string.IsNullOrWhiteSpace(PackageId);

    public bool HasVersion => !string.IsNullOrWhiteSpace(Version);

    public bool HasFilters => HasPackageId || HasVersion;

    public string DisplayLabel
        => HasVersion
            ? $"{PackageId} {Version}"
            : HasPackageId
                ? PackageId!
                : "all packages";

    public bool Matches(string? packageId, string? version)
    {
        if (HasPackageId && !string.Equals(NormalizePackageId(packageId), NormalizePackageId(PackageId), StringComparison.Ordinal))
        {
            return false;
        }

        return !HasVersion
            || string.Equals(version, Version, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesMetadataPath(string metadataPath)
    {
        if (!HasFilters)
        {
            return true;
        }

        var versionDirectory = Path.GetDirectoryName(metadataPath);
        if (string.IsNullOrWhiteSpace(versionDirectory))
        {
            return false;
        }

        var version = Path.GetFileName(versionDirectory);
        var packageDirectory = Path.GetDirectoryName(versionDirectory);
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return false;
        }

        var packageId = Path.GetFileName(packageDirectory);
        return Matches(packageId, version);
    }

    private static string NormalizePackageId(string? packageId)
        => (packageId ?? string.Empty).Trim().ToLowerInvariant();
}

internal static class ArtifactRegenerationMetadataPathSupport
{
    public static IReadOnlyList<string> EnumerateMetadataPaths(string packagesRoot, ArtifactRegenerationScope? scope)
    {
        var resolvedScope = scope ?? ArtifactRegenerationScope.All;
        return Directory.GetFiles(packagesRoot, "metadata.json", SearchOption.AllDirectories)
            .Where(path => !string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "latest", StringComparison.OrdinalIgnoreCase))
            .Where(resolvedScope.MatchesMetadataPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
