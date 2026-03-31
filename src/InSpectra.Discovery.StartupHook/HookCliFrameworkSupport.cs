using System.Reflection;

internal static class HookCliFrameworkSupport
{
    public const string SystemCommandLine = "System.CommandLine";
    public const string McMasterExtensionsCommandLineUtils = "McMaster.Extensions.CommandLineUtils";
    public const string MicrosoftExtensionsCommandLineUtils = "Microsoft.Extensions.CommandLineUtils";

    public static string NormalizeExpectedFramework(string? cliFramework)
        => cliFramework switch
        {
            McMasterExtensionsCommandLineUtils => McMasterExtensionsCommandLineUtils,
            MicrosoftExtensionsCommandLineUtils => MicrosoftExtensionsCommandLineUtils,
            _ => SystemCommandLine,
        };

    public static string GetExpectedAssemblyName(string cliFramework)
        => cliFramework;

    public static bool MatchesExpectedAssembly(Assembly assembly, string cliFramework)
        => string.Equals(
            assembly.GetName().Name,
            GetExpectedAssemblyName(cliFramework),
            StringComparison.OrdinalIgnoreCase);

    public static void InstallPatches(Assembly assembly, string cliFramework, string capturePath)
    {
        switch (cliFramework)
        {
            case McMasterExtensionsCommandLineUtils:
            case MicrosoftExtensionsCommandLineUtils:
                CommandLineUtilsPatchInstaller.Install(assembly, cliFramework, capturePath);
                return;
            default:
                HarmonyPatchInstaller.Install(assembly, capturePath);
                return;
        }
    }
}
