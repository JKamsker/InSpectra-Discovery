namespace InSpectra.Discovery.Tool.Queue.Planning;

using InSpectra.Discovery.Tool.Queue.Models;

internal static class DotnetTargetFrameworkRuntimeSupport
{
    public static DotnetRuntimeRequirement? TryResolveRequirement(string targetFrameworkMoniker)
    {
        if (string.IsNullOrWhiteSpace(targetFrameworkMoniker))
        {
            return null;
        }

        var normalized = targetFrameworkMoniker.Trim();
        var hyphenIndex = normalized.IndexOf('-');
        var suffix = hyphenIndex >= 0 ? normalized[hyphenIndex..] : string.Empty;
        var baseMoniker = hyphenIndex >= 0 ? normalized[..hyphenIndex] : normalized;

        if (baseMoniker.StartsWith("netcoreapp", StringComparison.OrdinalIgnoreCase)
            && TryParseVersion(baseMoniker["netcoreapp".Length..], out var channel))
        {
            return CreateRequirement("Microsoft.NETCore.App", channel, runtime: "dotnet");
        }

        if (baseMoniker.StartsWith("net", StringComparison.OrdinalIgnoreCase)
            && TryParseVersion(baseMoniker["net".Length..], out channel))
        {
            if (suffix.Length > 0 && !suffix.Contains("windows", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var frameworkName = suffix.Contains("windows", StringComparison.OrdinalIgnoreCase)
                ? "Microsoft.WindowsDesktop.App"
                : "Microsoft.NETCore.App";
            var runtime = suffix.Contains("windows", StringComparison.OrdinalIgnoreCase)
                ? "windowsdesktop"
                : "dotnet";
            return CreateRequirement(frameworkName, channel, runtime);
        }

        return null;
    }

    private static DotnetRuntimeRequirement CreateRequirement(string name, string channel, string runtime)
        => new(
            Name: name,
            Version: channel + ".0",
            Channel: channel,
            Runtime: runtime);

    private static bool TryParseVersion(string value, out string channel)
    {
        channel = string.Empty;
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor))
        {
            return false;
        }

        if (major < 2)
        {
            return false;
        }

        if (major < 5
            && !string.Equals($"{major}.{minor}", "2.1", StringComparison.Ordinal)
            && !string.Equals($"{major}.{minor}", "2.2", StringComparison.Ordinal)
            && !string.Equals($"{major}.{minor}", "3.0", StringComparison.Ordinal)
            && !string.Equals($"{major}.{minor}", "3.1", StringComparison.Ordinal))
        {
            return false;
        }

        channel = $"{major}.{minor}";
        return true;
    }
}
