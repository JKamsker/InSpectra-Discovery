using System.Reflection;
using System.Runtime.InteropServices;

internal static class HookAssemblySelectionSupport
{
    public static bool ShouldPatch(Assembly assembly, string cliFramework, string? preferredFrameworkDirectory)
    {
        string? assemblyLocation;
        try
        {
            assemblyLocation = assembly.Location;
        }
        catch
        {
            assemblyLocation = null;
        }

        return ShouldPatch(
            assembly.GetName().Name,
            assemblyLocation,
            cliFramework,
            preferredFrameworkDirectory);
    }

    internal static bool ShouldPatch(
        string? assemblyName,
        string? assemblyLocation,
        string cliFramework,
        string? preferredFrameworkDirectory)
    {
        if (!string.Equals(
                assemblyName,
                HookCliFrameworkSupport.GetExpectedAssemblyName(cliFramework),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(preferredFrameworkDirectory)
            || string.IsNullOrWhiteSpace(assemblyLocation))
        {
            return true;
        }

        return IsUnderDirectory(assemblyLocation, preferredFrameworkDirectory);
    }

    private static bool IsUnderDirectory(string assemblyLocation, string preferredFrameworkDirectory)
    {
        try
        {
            var assemblyDirectory = Path.GetDirectoryName(Path.GetFullPath(assemblyLocation));
            var targetDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(preferredFrameworkDirectory));
            if (string.IsNullOrWhiteSpace(assemblyDirectory) || string.IsNullOrWhiteSpace(targetDirectory))
            {
                return false;
            }

            var comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            var current = Path.TrimEndingDirectorySeparator(assemblyDirectory);
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (string.Equals(current, targetDirectory, comparison))
                {
                    return true;
                }

                var parentDirectory = Directory.GetParent(current)?.FullName;
                if (string.IsNullOrWhiteSpace(parentDirectory)
                    || string.Equals(parentDirectory, current, comparison))
                {
                    break;
                }

                current = Path.TrimEndingDirectorySeparator(parentDirectory);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
