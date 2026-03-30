// No namespace - required by DOTNET_STARTUP_HOOKS contract.

using System.Reflection;
using System.Runtime.Loader;

internal class StartupHook
{
    public static void Initialize()
    {
        var capturePath = Environment.GetEnvironmentVariable("INSPECTRA_CAPTURE_PATH");
        if (string.IsNullOrEmpty(capturePath))
            return;

        // Resolve hook dependencies (0Harmony.dll) from the hook's own directory.
        var hookDir = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location)!;
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            var candidate = Path.Combine(hookDir, name.Name + ".dll");
            return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
        };

        AssemblyLoadInterceptor.Start(capturePath);
    }
}
