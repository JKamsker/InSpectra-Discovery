namespace InSpectra.Discovery.Tool.Packages;

using InSpectra.Discovery.Tool.Frameworks;

using System.IO.Compression;

internal static class PackageArchiveCliFrameworkReferenceSupport
{
    public static IReadOnlyList<ToolCliFrameworkReferenceInspection> InspectToolAssemblyReferences(
        ZipArchive archive,
        IReadOnlySet<string> toolDirectories)
    {
        if (toolDirectories.Count == 0)
        {
            return [];
        }

        var probes = CliFrameworkProviderRegistry.ResolveRuntimeReferenceProbes();
        if (probes.Count == 0)
        {
            return [];
        }

        var excludedAssemblyNames = probes
            .SelectMany(static probe => probe.PackageAssemblyNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var referencesByFramework = probes.ToDictionary(
            static probe => probe.FrameworkName,
            static _ => new SortedSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            if (!PackageArchivePathSupport.IsToolManagedAssembly(entry, toolDirectories, excludedAssemblyNames))
            {
                continue;
            }

            var inspection = PackageArchivePortableExecutableSupport.ReadAssemblyInspection(entry);
            foreach (var probe in probes)
            {
                if (probe.RuntimeAssemblyNames.Any(runtimeAssemblyName =>
                        PackageArchivePortableExecutableSupport.HasReference(inspection, runtimeAssemblyName)))
                {
                    referencesByFramework[probe.FrameworkName].Add(entry.FullName);
                }
            }
        }

        return probes
            .Select(probe => new ToolCliFrameworkReferenceInspection(
                probe.FrameworkName,
                referencesByFramework[probe.FrameworkName].ToArray()))
            .Where(static inspection => inspection.ReferencingAssemblyPaths.Count > 0)
            .ToArray();
    }

    public static IReadOnlyList<string> GetReferencingAssemblyPaths(
        IReadOnlyList<ToolCliFrameworkReferenceInspection> inspections,
        string frameworkName)
        => inspections
            .Where(inspection => string.Equals(inspection.FrameworkName, frameworkName, StringComparison.OrdinalIgnoreCase))
            .SelectMany(static inspection => inspection.ReferencingAssemblyPaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
