namespace InSpectra.Discovery.Tool.StaticAnalysis;

using dnlib.DotNet;

internal sealed class DnlibAssemblyScanner
{
    public IReadOnlyList<ScannedModule> ScanForFramework(string installDirectory, string assemblyName)
    {
        var assemblyPaths = Directory.EnumerateFiles(installDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = new List<ScannedModule>();
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

            if (!ReferencesAssembly(module, assemblyName))
            {
                module.Dispose();
                continue;
            }

            results.Add(new ScannedModule(path, module));
        }

        return results;
    }

    private static bool ReferencesAssembly(ModuleDefMD module, string assemblyName)
    {
        if (string.Equals(module.Assembly?.Name?.String, assemblyName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var assemblyRef in module.GetAssemblyRefs())
        {
            if (string.Equals(assemblyRef.Name?.String, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed record ScannedModule(string Path, ModuleDefMD Module) : IDisposable
{
    public void Dispose() => Module.Dispose();
}

