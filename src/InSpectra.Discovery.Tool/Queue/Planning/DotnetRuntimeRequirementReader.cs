namespace InSpectra.Discovery.Tool.Queue.Planning;

using InSpectra.Discovery.Tool.Queue.Models;

using System.Text.Json.Nodes;

internal static class DotnetRuntimeRequirementReader
{
    public static bool TryReadRuntimeRequirements(
        JsonObject? document,
        out IReadOnlyList<DotnetRuntimeRequirement> requirements,
        out string? error)
    {
        requirements = [];
        error = null;

        var runtimeOptions = document?["runtimeOptions"]?.AsObject();
        if (runtimeOptions is null)
        {
            return true;
        }

        var values = new HashSet<DotnetRuntimeRequirement>();
        if (!TryAddRuntimeRequirement(runtimeOptions["framework"], values, out error))
        {
            return false;
        }

        foreach (var frameworkNode in runtimeOptions["frameworks"]?.AsArray() ?? [])
        {
            if (!TryAddRuntimeRequirement(frameworkNode, values, out error))
            {
                return false;
            }
        }

        requirements = values.ToArray();
        return true;
    }

    private static bool TryAddRuntimeRequirement(
        JsonNode? node,
        ISet<DotnetRuntimeRequirement> requirements,
        out string? error)
    {
        error = null;
        if (node is not JsonObject framework)
        {
            return true;
        }

        var name = framework["name"]?.GetValue<string>();
        var version = framework["version"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
        {
            return true;
        }

        var runtime = TryResolveRuntimeKind(name);
        if (runtime is null)
        {
            error = $"Unsupported shared framework '{name}'.";
            return false;
        }

        var channel = GetChannel(version);
        if (channel is null)
        {
            error = $"Unsupported shared framework version '{version}'.";
            return false;
        }

        requirements.Add(new DotnetRuntimeRequirement(name, version, channel, runtime));
        return true;
    }

    private static string? TryResolveRuntimeKind(string frameworkName)
        => frameworkName switch
        {
            "Microsoft.NETCore.App" => "dotnet",
            "Microsoft.AspNetCore.App" => "aspnetcore",
            "Microsoft.WindowsDesktop.App" => "windowsdesktop",
            _ => null,
        };

    private static string? GetChannel(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length < 2 ? null : $"{parts[0]}.{parts[1]}";
    }
}
