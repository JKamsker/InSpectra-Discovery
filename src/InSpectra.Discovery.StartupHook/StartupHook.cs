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

        try
        {
            // Resolve hook dependencies (0Harmony.dll) from the hook's own directory.
            var hookDir = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location)!;
            AssemblyLoadContext.Default.Resolving += (context, name) =>
            {
                var candidate = Path.Combine(hookDir, name.Name + ".dll");
                return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
            };

            AssemblyLoadInterceptor.Start(capturePath);
        }
        catch (Exception ex)
        {
            WriteError(capturePath, "initialize-failed", ex.ToString());
        }
    }

    private static void WriteError(string path, string status, string error)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path,
                $"{{\"captureVersion\":1,\"status\":\"{status}\",\"error\":\"{EscapeJson(error)}\"}}");
        }
        catch { }
    }

    private static string EscapeJson(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
}
