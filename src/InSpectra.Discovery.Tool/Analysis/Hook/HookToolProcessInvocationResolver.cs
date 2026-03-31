namespace InSpectra.Discovery.Tool.Analysis.Hook;

using System.Runtime.InteropServices;
using System.Xml.Linq;

internal static class HookToolProcessInvocationResolver
{
    public static HookToolProcessInvocation Resolve(string installDirectory, string commandName, string commandPath)
        => TryResolveDotnetRunnerInvocation(installDirectory, commandName)
            ?? new HookToolProcessInvocation(commandPath, ["--help"]);

    private static HookToolProcessInvocation? TryResolveDotnetRunnerInvocation(string installDirectory, string commandName)
    {
        if (string.IsNullOrWhiteSpace(installDirectory) || string.IsNullOrWhiteSpace(commandName))
        {
            return null;
        }

        foreach (var settingsPath in Directory.EnumerateFiles(installDirectory, "DotnetToolSettings.xml", SearchOption.AllDirectories))
        {
            var invocation = TryResolveFromSettings(settingsPath, commandName);
            if (invocation is not null)
            {
                return invocation;
            }
        }

        return null;
    }

    private static HookToolProcessInvocation? TryResolveFromSettings(string settingsPath, string commandName)
    {
        try
        {
            var document = XDocument.Load(settingsPath);
            var commandElement = document
                .Descendants()
                .FirstOrDefault(element =>
                    string.Equals(element.Name.LocalName, "Command", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(
                        element.Attribute("Name")?.Value,
                        commandName,
                        StringComparison.OrdinalIgnoreCase));

            if (commandElement is null)
            {
                return null;
            }

            var runner = commandElement.Attribute("Runner")?.Value?.Trim();
            var entryPoint = commandElement.Attribute("EntryPoint")?.Value?.Trim();
            if (!string.Equals(runner, "dotnet", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(entryPoint))
            {
                return null;
            }

            var settingsDirectory = Path.GetDirectoryName(settingsPath);
            if (string.IsNullOrWhiteSpace(settingsDirectory))
            {
                return null;
            }

            var entryPointPath = Path.GetFullPath(Path.Combine(
                settingsDirectory,
                entryPoint.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)));
            if (!File.Exists(entryPointPath))
            {
                return null;
            }

            return new HookToolProcessInvocation(ResolveDotnetHostPath(), [entryPointPath, "--help"]);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveDotnetHostPath()
    {
        foreach (var variableName in GetPreferredDotnetRootVariables())
        {
            var dotnetRoot = Environment.GetEnvironmentVariable(variableName);
            if (string.IsNullOrWhiteSpace(dotnetRoot))
            {
                continue;
            }

            var dotnetPath = Path.Combine(dotnetRoot, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
            if (File.Exists(dotnetPath))
            {
                return dotnetPath;
            }
        }

        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(hostPath) && File.Exists(hostPath))
        {
            return hostPath;
        }

        return "dotnet";
    }

    private static IEnumerable<string> GetPreferredDotnetRootVariables()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "DOTNET_ROOT_X64",
                Architecture.X86 => "DOTNET_ROOT_X86",
                Architecture.Arm64 => "DOTNET_ROOT_ARM64",
                _ => "DOTNET_ROOT",
            };
        }

        yield return "DOTNET_ROOT";
    }
}

internal sealed record HookToolProcessInvocation(
    string FilePath,
    IReadOnlyList<string> ArgumentList);
