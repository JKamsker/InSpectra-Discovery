namespace InSpectra.Discovery.Tool.Analysis.Hook;

using InSpectra.Discovery.Tool.Queue.Planning;

using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Xml.Linq;

internal static class HookToolProcessInvocationResolver
{
    public static HookToolProcessInvocation Resolve(string installDirectory, string commandName, string commandPath)
        => TryResolveDotnetRunnerInvocation(installDirectory, commandName)
            ?? new HookToolProcessInvocation(commandPath, ["--help"]);

    public static IReadOnlyList<HookToolProcessInvocation> BuildHelpFallbackInvocations(HookToolProcessInvocation invocation)
    {
        if (invocation.ArgumentList.Count == 0
            || !string.Equals(invocation.ArgumentList[^1], "--help", StringComparison.Ordinal))
        {
            return [];
        }

        var baseArguments = invocation.ArgumentList
            .Take(invocation.ArgumentList.Count - 1)
            .ToArray();
        return
        [
            new HookToolProcessInvocation(invocation.FilePath, [.. baseArguments, "-h"]),
            new HookToolProcessInvocation(invocation.FilePath, [.. baseArguments, "-?"]),
            new HookToolProcessInvocation(invocation.FilePath, [.. baseArguments, "--h"]),
        ];
    }

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

            if (!EnsureRuntimeConfig(entryPointPath, settingsPath))
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

    private static bool EnsureRuntimeConfig(string entryPointPath, string settingsPath)
    {
        if (!string.Equals(Path.GetExtension(entryPointPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var runtimeConfigPath = Path.ChangeExtension(entryPointPath, ".runtimeconfig.json");
        if (File.Exists(runtimeConfigPath))
        {
            return true;
        }

        return TryWriteSyntheticRuntimeConfig(settingsPath, runtimeConfigPath);
    }

    private static bool TryWriteSyntheticRuntimeConfig(string settingsPath, string runtimeConfigPath)
    {
        try
        {
            if (!TryResolveTargetFrameworkMoniker(settingsPath, out var targetFrameworkMoniker))
            {
                return false;
            }

            var requirement = DotnetTargetFrameworkRuntimeSupport.TryResolveRequirement(targetFrameworkMoniker);
            if (requirement is null)
            {
                return false;
            }

            var runtimeConfig = new JsonObject
            {
                ["runtimeOptions"] = new JsonObject
                {
                    ["tfm"] = targetFrameworkMoniker,
                    ["framework"] = new JsonObject
                    {
                        ["name"] = requirement.Name,
                        ["version"] = requirement.Version,
                    },
                },
            };

            File.WriteAllText(runtimeConfigPath, runtimeConfig.ToJsonString());
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryResolveTargetFrameworkMoniker(string settingsPath, out string targetFrameworkMoniker)
    {
        var normalized = settingsPath.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length - 2; index++)
        {
            if (!string.Equals(segments[index], "tools", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            targetFrameworkMoniker = segments[index + 1];
            return !string.IsNullOrWhiteSpace(targetFrameworkMoniker);
        }

        targetFrameworkMoniker = string.Empty;
        return false;
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
