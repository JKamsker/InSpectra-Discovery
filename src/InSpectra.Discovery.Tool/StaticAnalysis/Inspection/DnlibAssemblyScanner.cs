namespace InSpectra.Discovery.Tool.StaticAnalysis.Inspection;


using dnlib.DotNet;

internal sealed class DnlibAssemblyScanner
{
    public IReadOnlyList<ScannedModule> ScanForFramework(
        string installDirectory,
        string assemblyName,
        string? preferredEntryPointPath = null)
    {
        var assemblyPaths = Directory.EnumerateFiles(installDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var loadedModules = new List<ScannedModule>();
        foreach (var path in assemblyPaths)
        {
            ModuleDefMD? module;
            try
            {
                module = ModuleDefMD.Load(path, new ModuleCreationOptions { TryToLoadPdbFromDisk = false });
            }
            catch (BadImageFormatException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }

            loadedModules.Add(new ScannedModule(path, module));
        }

        var selectedPaths = StaticAnalysisModuleSelectionSupport.SelectPaths(
            loadedModules.Select(CreateMetadata).ToArray(),
            assemblyName,
            preferredEntryPointPath);
        var selectedPathSet = new HashSet<string>(
            selectedPaths.Select(Path.GetFullPath),
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        var results = new List<ScannedModule>();
        foreach (var module in loadedModules)
        {
            if (selectedPathSet.Contains(Path.GetFullPath(module.Path)))
            {
                results.Add(module);
            }
            else
            {
                module.Dispose();
            }
        }

        return results;
    }

    private static ScannedModuleMetadata CreateMetadata(ScannedModule module)
        => new(
            module.Path,
            module.Module.Assembly?.Name?.String,
            module.Module.GetAssemblyRefs()
                .Select(static assemblyRef => assemblyRef.Name?.String)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>()
                .ToArray());
}

internal sealed record ScannedModule(string Path, ModuleDefMD Module) : IDisposable
{
    public void Dispose() => Module.Dispose();
}
