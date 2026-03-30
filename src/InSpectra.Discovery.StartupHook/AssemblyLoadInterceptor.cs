using System.Reflection;

internal static class AssemblyLoadInterceptor
{
    private static string? _capturePath;
    private static bool _patched;

    public static void Start(string capturePath)
    {
        _capturePath = capturePath;

        // Check assemblies already loaded.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (TryPatch(assembly))
                return;
        }

        // Watch for future loads.
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

        // If the tool exits without ever loading System.CommandLine, write a sentinel.
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
    }

    private static void OnAssemblyLoad(object? sender, AssemblyLoadEventArgs args)
    {
        TryPatch(args.LoadedAssembly);
    }

    private static bool TryPatch(Assembly assembly)
    {
        if (_patched)
            return true;

        if (!string.Equals(assembly.GetName().Name, "System.CommandLine", StringComparison.OrdinalIgnoreCase))
            return false;

        _patched = true;
        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;

        try
        {
            HarmonyPatchInstaller.Install(assembly, _capturePath!);
        }
        catch (Exception ex)
        {
            CaptureFileWriter.WriteError(_capturePath!, "patch-failed", ex.ToString());
        }

        return true;
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        if (!_patched && _capturePath is not null)
        {
            CaptureFileWriter.WriteError(_capturePath, "no-assembly-loaded",
                "System.CommandLine assembly was never loaded by the target tool.");
        }
    }
}
