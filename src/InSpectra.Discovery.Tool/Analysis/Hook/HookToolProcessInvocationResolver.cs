namespace InSpectra.Discovery.Tool.Analysis.Hook;

using InSpectra.Discovery.Tool.Infrastructure.Commands;
using InSpectra.Discovery.Tool.Queue.Planning;

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

internal static class HookToolProcessInvocationResolver
{
    public static HookToolProcessInvocationResolution Resolve(string installDirectory, string commandName, string commandPath)
        => TryResolveDotnetRunnerInvocation(installDirectory, commandName)
            ?? HookToolProcessInvocationResolution.FromInvocation(new HookToolProcessInvocation(commandPath, ["--help"]));

    public static string? TryResolvePreferredAssemblyDirectory(HookToolProcessInvocation invocation)
    {
        var candidatePath = invocation.ArgumentList.Count > 0
            && string.Equals(Path.GetFileNameWithoutExtension(invocation.FilePath), "dotnet", StringComparison.OrdinalIgnoreCase)
            ? invocation.ArgumentList[0]
            : invocation.FilePath;
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(candidatePath);
            return Path.GetDirectoryName(fullPath);
        }
        catch
        {
            return null;
        }
    }

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

    private static HookToolProcessInvocationResolution? TryResolveDotnetRunnerInvocation(string installDirectory, string commandName)
    {
        var command = InstalledDotnetToolCommandSupport.TryResolve(installDirectory, commandName);
        if (command is null
            || !string.Equals(command.Runner, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var entryPointPath = command.EntryPointPath;
        if (!File.Exists(entryPointPath))
        {
            return HookToolProcessInvocationResolution.TerminalFailure(
                "hook-invalid-dotnet-entrypoint",
                $"Dotnet tool entry point '{Path.GetFileName(entryPointPath)}' was declared in DotnetToolSettings.xml but was not found.");
        }

        if (!EnsureRuntimeConfig(entryPointPath, command.SettingsPath))
        {
            return null;
        }

        if (TryValidateManagedEntryPoint(entryPointPath, out var validationFailureMessage))
        {
            return HookToolProcessInvocationResolution.TerminalFailure(
                "hook-invalid-dotnet-entrypoint",
                validationFailureMessage);
        }

        return HookToolProcessInvocationResolution.FromInvocation(
            new HookToolProcessInvocation(ResolveDotnetHostPath(), [entryPointPath, "--help"]));
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

    private static bool TryValidateManagedEntryPoint(string entryPointPath, out string failureMessage)
    {
        if (!string.Equals(Path.GetExtension(entryPointPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            failureMessage = string.Empty;
            return false;
        }

        try
        {
            using var stream = File.OpenRead(entryPointPath);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                failureMessage = $"Dotnet tool entry point '{Path.GetFileName(entryPointPath)}' is not a managed .NET assembly.";
                return true;
            }

            _ = peReader.GetMetadataReader();
            if (peReader.PEHeaders.CorHeader?.EntryPointTokenOrRelativeVirtualAddress > 0)
            {
                failureMessage = string.Empty;
                return false;
            }

            failureMessage = $"Dotnet tool entry point '{Path.GetFileName(entryPointPath)}' does not contain a managed entry point.";
            return true;
        }
        catch (BadImageFormatException)
        {
            failureMessage = $"Dotnet tool entry point '{Path.GetFileName(entryPointPath)}' is not a valid managed .NET assembly.";
            return true;
        }
        catch (IOException ex)
        {
            failureMessage = $"Dotnet tool entry point '{Path.GetFileName(entryPointPath)}' could not be inspected: {ex.Message}";
            return true;
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

internal sealed record HookToolProcessInvocationResolution(
    HookToolProcessInvocation? Invocation,
    string? TerminalFailureClassification,
    string? TerminalFailureMessage)
{
    public static HookToolProcessInvocationResolution FromInvocation(HookToolProcessInvocation invocation)
        => new(invocation, null, null);

    public static HookToolProcessInvocationResolution TerminalFailure(string classification, string message)
        => new(null, classification, message);
}
