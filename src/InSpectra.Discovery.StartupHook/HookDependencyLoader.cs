using System.Reflection;

internal static class HookDependencyLoader
{
    private static readonly string[] RequiredAssemblyFileNames = ["0Harmony.dll"];

    public static void LoadRequiredAssemblies()
    {
        var hookDirectory = Path.GetDirectoryName(typeof(StartupHook).Assembly.Location);
        if (string.IsNullOrWhiteSpace(hookDirectory))
            throw new InvalidOperationException("Could not resolve the startup hook directory.");

        foreach (var fileName in RequiredAssemblyFileNames)
            LoadAssemblyIfNeeded(hookDirectory, fileName);
    }

    private static void LoadAssemblyIfNeeded(string hookDirectory, string fileName)
    {
        var assemblyName = Path.GetFileNameWithoutExtension(fileName);
        if (AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
                string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var assemblyPath = Path.Combine(hookDirectory, fileName);
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Required startup hook dependency '{fileName}' was not found.", assemblyPath);

        Assembly.LoadFrom(assemblyPath);
    }
}
