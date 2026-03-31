// No namespace - required by DOTNET_STARTUP_HOOKS contract.

internal class StartupHook
{
    public static void Initialize()
    {
        var capturePath = Environment.GetEnvironmentVariable("INSPECTRA_CAPTURE_PATH");
        var expectedCliFramework = Environment.GetEnvironmentVariable("INSPECTRA_EXPECTED_CLI_FRAMEWORK");
        if (string.IsNullOrEmpty(capturePath))
            return;

        try
        {
            HookDependencyLoader.LoadRequiredAssemblies();
            AssemblyLoadInterceptor.Start(capturePath, expectedCliFramework);
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
