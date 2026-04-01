using System.Reflection;

internal static class AssemblyLoadInterceptor
{
    private static string? _capturePath;
    private static string _expectedCliFramework = HookCliFrameworkSupport.SystemCommandLine;
    private static string? _preferredFrameworkDirectory;
    private static bool _patched;

    public static void Start(string capturePath, string? expectedCliFramework, string? preferredFrameworkDirectory)
    {
        _capturePath = capturePath;
        _expectedCliFramework = HookCliFrameworkSupport.NormalizeExpectedFramework(expectedCliFramework);
        _preferredFrameworkDirectory = preferredFrameworkDirectory;

        // Check assemblies already loaded.
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (TryPatch(assembly))
                return;
        }

        // Watch for future loads.
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;

        // Surface target crashes so the caller gets a concrete classification instead of a missing file.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

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

        if (!HookAssemblySelectionSupport.ShouldPatch(assembly, _expectedCliFramework, _preferredFrameworkDirectory))
            return false;

        _patched = true;
        AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;

        try
        {
            HookCliFrameworkSupport.InstallPatches(assembly, _expectedCliFramework, _capturePath!);
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
                $"{HookCliFrameworkSupport.GetExpectedAssemblyName(_expectedCliFramework)} assembly was never loaded by the target tool.",
                overwrite: false);
        }
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs args)
    {
        if (_capturePath is null)
            return;

        var error = args.ExceptionObject?.ToString()
            ?? "The target tool terminated with an unhandled exception before startup hook capture completed.";
        CaptureFileWriter.WriteError(_capturePath, "target-unhandled-exception", error, overwrite: false);
    }
}
