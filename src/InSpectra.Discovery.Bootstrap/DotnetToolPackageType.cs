internal static class DotnetToolPackageType
{
    public static bool IsDotnetTool(CatalogLeaf leaf)
        => (leaf.PackageTypes ?? [])
            .Any(packageType => string.Equals(packageType.Name, "DotnetTool", StringComparison.OrdinalIgnoreCase));
}
