namespace InSpectra.Discovery.Tool.StaticAnalysis.Inspection;

internal static class StaticAnalysisModuleSelectionSupport
{
    public static IReadOnlyList<string> SelectPaths(
        IReadOnlyList<ScannedModuleMetadata> modules,
        string assemblyName,
        string? preferredEntryPointPath)
    {
        if (modules.Count == 0 || string.IsNullOrWhiteSpace(assemblyName))
        {
            return [];
        }

        var preferredPath = NormalizePath(preferredEntryPointPath);
        var nonFrameworkMatches = modules
            .Where(module => ReferencesAssembly(module, assemblyName) && !IsFrameworkAssembly(module, assemblyName))
            .ToArray();
        if (nonFrameworkMatches.Length > 0)
        {
            return OrderByPreferred(nonFrameworkMatches, preferredPath);
        }

        var preferredModule = FindPreferredModule(modules, preferredPath);
        if (preferredModule is not null)
        {
            return [preferredModule.Path];
        }

        var frameworkMatches = modules
            .Where(module => IsFrameworkAssembly(module, assemblyName))
            .ToArray();
        return OrderByPreferred(frameworkMatches, preferredPath);
    }

    private static IReadOnlyList<string> OrderByPreferred(
        IReadOnlyList<ScannedModuleMetadata> modules,
        string? preferredPath)
    {
        if (modules.Count == 0)
        {
            return [];
        }

        if (preferredPath is null)
        {
            return modules.Select(static module => module.Path).ToArray();
        }

        return modules
            .OrderByDescending(module => PathsEqual(module.Path, preferredPath))
            .Select(static module => module.Path)
            .ToArray();
    }

    private static ScannedModuleMetadata? FindPreferredModule(
        IReadOnlyList<ScannedModuleMetadata> modules,
        string? preferredPath)
    {
        if (preferredPath is null)
        {
            return null;
        }

        return modules.FirstOrDefault(module => PathsEqual(module.Path, preferredPath));
    }

    private static bool ReferencesAssembly(ScannedModuleMetadata module, string assemblyName)
        => module.AssemblyReferences.Any(reference => string.Equals(reference, assemblyName, StringComparison.OrdinalIgnoreCase));

    private static bool IsFrameworkAssembly(ScannedModuleMetadata module, string assemblyName)
        => string.Equals(module.AssemblyName, assemblyName, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}

internal sealed record ScannedModuleMetadata(
    string Path,
    string? AssemblyName,
    IReadOnlyList<string> AssemblyReferences);
