using System.IO.Compression;
using System.Text.Json;

internal sealed class CliFxPackageArchiveInspector
{
    private readonly NuGetApiClient _apiClient;

    public CliFxPackageArchiveInspector(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<CliFxPackageInspection> InspectAsync(string packageContentUrl, CancellationToken cancellationToken)
        => PackageArchiveInspectionSupport.InspectAsync(
            _apiClient,
            packageContentUrl,
            "inspectra-clifx",
            InspectArchive,
            cancellationToken);

    private static CliFxPackageInspection InspectArchive(ZipArchive archive)
    {
        var depsFilePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var dependencyVersions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var assemblies = new List<CliFxAssemblyVersionInfo>();
        var toolLayoutBuilder = new DotnetToolPackageLayoutBuilder();

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
            {
                depsFilePaths.Add(entry.FullName);
                ReadDependencyVersions(entry, dependencyVersions);
                continue;
            }

            if (string.Equals(entry.Name, "DotnetToolSettings.xml", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = entry.Open();
                toolLayoutBuilder.Add(entry.FullName, DotnetToolSettingsReader.Read(stream, entry.FullName));
                continue;
            }

            if (string.Equals(entry.Name, "CliFx.dll", StringComparison.OrdinalIgnoreCase))
            {
                assemblies.Add(ToAssemblyVersionInfo(
                    PackageArchivePortableExecutableSupport.ReadAssemblyInspection(entry)));
            }
        }

        var toolLayout = toolLayoutBuilder.Build();
        var toolAssembliesReferencingCliFx = InspectToolAssemblyReferences(archive, toolLayout.ToolDirectories);

        return new CliFxPackageInspection(
            DepsFilePaths: depsFilePaths.ToArray(),
            CliFxDependencyVersions: dependencyVersions.ToArray(),
            CliFxAssemblies: assemblies.OrderBy(assembly => assembly.Path, StringComparer.OrdinalIgnoreCase).ToArray(),
            ToolSettingsPaths: toolLayout.ToolSettingsPaths,
            ToolCommandNames: toolLayout.ToolCommandNames,
            ToolEntryPointPaths: toolLayout.ToolEntryPointPaths,
            ToolAssembliesReferencingCliFx: toolAssembliesReferencingCliFx);
    }

    private static void ReadDependencyVersions(ZipArchiveEntry entry, ISet<string> dependencyVersions)
    {
        using var stream = entry.Open();
        using var document = JsonDocument.Parse(stream);
        if (!document.RootElement.TryGetProperty("libraries", out var libraries)
            || libraries.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (var library in libraries.EnumerateObject())
        {
            if (TryParsePackageVersion(library.Name, "CliFx", out var version))
            {
                dependencyVersions.Add(version);
            }
        }
    }

    private static IReadOnlyList<string> InspectToolAssemblyReferences(
        ZipArchive archive,
        IReadOnlySet<string> toolDirectories)
    {
        if (toolDirectories.Count == 0)
        {
            return [];
        }

        var toolAssembliesReferencingCliFx = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            if (!PackageArchivePathSupport.IsToolManagedAssembly(entry, toolDirectories, "CliFx.dll"))
            {
                continue;
            }

            var inspection = PackageArchivePortableExecutableSupport.ReadAssemblyInspection(entry);
            if (PackageArchivePortableExecutableSupport.HasReference(inspection, "CliFx"))
            {
                toolAssembliesReferencingCliFx.Add(entry.FullName);
            }
        }

        return toolAssembliesReferencingCliFx.ToArray();
    }

    private static bool TryParsePackageVersion(string key, string packageId, out string version)
    {
        var prefix = packageId + "/";
        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && key.Length > prefix.Length)
        {
            version = key[prefix.Length..];
            return true;
        }

        version = string.Empty;
        return false;
    }

    private static CliFxAssemblyVersionInfo ToAssemblyVersionInfo(PackageArchiveAssemblyInspection inspection)
        => new(
            inspection.Path,
            inspection.AssemblyVersion,
            inspection.FileVersion,
            inspection.InformationalVersion);
}
