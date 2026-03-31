namespace InSpectra.Discovery.Tool.Packages;

using InSpectra.Discovery.Tool.NuGet;

using InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;


using System.IO.Compression;
using System.Text.Json;

internal sealed class PackageArchiveInspector
{
    private readonly NuGetApiClient _apiClient;

    public PackageArchiveInspector(NuGetApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<SpectrePackageInspection> InspectAsync(string packageContentUrl, CancellationToken cancellationToken)
        => PackageArchiveInspectionSupport.InspectAsync(
            _apiClient,
            packageContentUrl,
            "inspectra",
            InspectArchive,
            cancellationToken);

    private static SpectrePackageInspection InspectArchive(ZipArchive archive)
    {
        var depsFilePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var spectreConsoleDependencyVersions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var spectreConsoleCliDependencyVersions = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var spectreConsoleAssemblies = new List<SpectreAssemblyVersionInfo>();
        var spectreConsoleCliAssemblies = new List<SpectreAssemblyVersionInfo>();
        var toolLayoutBuilder = new DotnetToolPackageLayoutBuilder();

        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase))
            {
                depsFilePaths.Add(entry.FullName);
                ReadDependencyVersions(entry, spectreConsoleDependencyVersions, spectreConsoleCliDependencyVersions);
                continue;
            }

            if (string.Equals(entry.Name, "DotnetToolSettings.xml", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = entry.Open();
                toolLayoutBuilder.Add(entry.FullName, DotnetToolSettingsReader.Read(stream, entry.FullName));
                continue;
            }

            if (string.Equals(entry.Name, "Spectre.Console.Cli.dll", StringComparison.OrdinalIgnoreCase))
            {
                spectreConsoleCliAssemblies.Add(ToAssemblyVersionInfo(
                    PackageArchivePortableExecutableSupport.ReadAssemblyInspection(entry)));
                continue;
            }

            if (string.Equals(entry.Name, "Spectre.Console.dll", StringComparison.OrdinalIgnoreCase))
            {
                spectreConsoleAssemblies.Add(ToAssemblyVersionInfo(
                    PackageArchivePortableExecutableSupport.ReadAssemblyInspection(entry)));
            }
        }

        var toolLayout = toolLayoutBuilder.Build();
        var toolAssemblyReferences = InspectToolAssemblyReferences(archive, toolLayout.ToolDirectories);

        return new SpectrePackageInspection(
            DepsFilePaths: depsFilePaths.ToArray(),
            SpectreConsoleDependencyVersions: spectreConsoleDependencyVersions.ToArray(),
            SpectreConsoleCliDependencyVersions: spectreConsoleCliDependencyVersions.ToArray(),
            SpectreConsoleAssemblies: spectreConsoleAssemblies
                .OrderBy(assembly => assembly.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            SpectreConsoleCliAssemblies: spectreConsoleCliAssemblies
                .OrderBy(assembly => assembly.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ToolSettingsPaths: toolLayout.ToolSettingsPaths,
            ToolCommandNames: toolLayout.ToolCommandNames,
            ToolEntryPointPaths: toolLayout.ToolEntryPointPaths,
            ToolAssembliesReferencingSpectreConsole: toolAssemblyReferences.SpectreConsolePaths,
            ToolAssembliesReferencingSpectreConsoleCli: toolAssemblyReferences.SpectreConsoleCliPaths);
    }

    private static void ReadDependencyVersions(
        ZipArchiveEntry entry,
        ISet<string> spectreConsoleDependencyVersions,
        ISet<string> spectreConsoleCliDependencyVersions)
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
            if (TryParsePackageVersion(library.Name, "Spectre.Console.Cli", out var cliVersion))
            {
                spectreConsoleCliDependencyVersions.Add(cliVersion);
                continue;
            }

            if (TryParsePackageVersion(library.Name, "Spectre.Console", out var consoleVersion))
            {
                spectreConsoleDependencyVersions.Add(consoleVersion);
            }
        }
    }

    private static SpectreToolAssemblyReferenceInspection InspectToolAssemblyReferences(
        ZipArchive archive,
        IReadOnlySet<string> toolDirectories)
    {
        if (toolDirectories.Count == 0)
        {
            return SpectreToolAssemblyReferenceInspection.Empty;
        }

        var spectreConsolePaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var spectreConsoleCliPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            if (!PackageArchivePathSupport.IsToolManagedAssembly(
                    entry,
                    toolDirectories,
                    "Spectre.Console.dll",
                    "Spectre.Console.Cli.dll"))
            {
                continue;
            }

            var inspection = PackageArchivePortableExecutableSupport.ReadAssemblyInspection(entry);
            if (PackageArchivePortableExecutableSupport.HasReference(inspection, "Spectre.Console"))
            {
                spectreConsolePaths.Add(entry.FullName);
            }

            if (PackageArchivePortableExecutableSupport.HasReference(inspection, "Spectre.Console.Cli"))
            {
                spectreConsoleCliPaths.Add(entry.FullName);
            }
        }

        return new SpectreToolAssemblyReferenceInspection(
            spectreConsolePaths.ToArray(),
            spectreConsoleCliPaths.ToArray());
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

    private static SpectreAssemblyVersionInfo ToAssemblyVersionInfo(PackageArchiveAssemblyInspection inspection)
        => new(
            inspection.Path,
            inspection.AssemblyVersion,
            inspection.FileVersion,
            inspection.InformationalVersion);

    private sealed record SpectreToolAssemblyReferenceInspection(
        IReadOnlyList<string> SpectreConsolePaths,
        IReadOnlyList<string> SpectreConsoleCliPaths)
    {
        public static SpectreToolAssemblyReferenceInspection Empty { get; } = new([], []);
    }
}

