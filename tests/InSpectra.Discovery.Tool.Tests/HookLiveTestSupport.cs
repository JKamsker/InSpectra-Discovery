namespace InSpectra.Discovery.Tool.Tests;

using System.Text.Json.Nodes;

using Xunit;

internal static class HookLiveTestSupport
{
    private const string EnableEnvVar = "INSPECTRA_DISCOVERY_LIVE_HOOK_TESTS";
    private const string DotnetRootOverrideEnvVar = "INSPECTRA_DISCOVERY_LIVE_DOTNET_ROOT";

    public static bool ShouldRun()
        => string.Equals(Environment.GetEnvironmentVariable(EnableEnvVar), "1", StringComparison.Ordinal);

    public static IDisposable UseOptionalDotnetRootOverride()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(DotnetRootOverrideEnvVar);
        return string.IsNullOrWhiteSpace(overrideRoot)
            ? NoopDisposable.Instance
            : new DotnetRootOverrideScope(overrideRoot);
    }

    public static IReadOnlyList<string> GetTopLevelNames(JsonNode? document, string propertyName)
        => document?[propertyName]?.AsArray()
            .Select(item => item?["name"]?.GetValue<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray()
            ?? [];

    public static void AssertPatchTarget(JsonNode? openCli, params string[] expectedPrefixes)
    {
        var patchTarget = openCli?["x-inspectra"]?["hookCapture"]?["patchTarget"]?.GetValue<string>();
        Assert.NotNull(patchTarget);
        Assert.Contains(expectedPrefixes, prefix => patchTarget.StartsWith(prefix, StringComparison.Ordinal));
    }

    private sealed class DotnetRootOverrideScope : IDisposable
    {
        private readonly string? _previousDotnetRoot;
        private readonly string? _previousDotnetRootX64;

        public DotnetRootOverrideScope(string dotnetRoot)
        {
            _previousDotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            _previousDotnetRootX64 = Environment.GetEnvironmentVariable("DOTNET_ROOT_X64");

            Environment.SetEnvironmentVariable("DOTNET_ROOT", dotnetRoot);
            if (OperatingSystem.IsWindows())
            {
                Environment.SetEnvironmentVariable("DOTNET_ROOT_X64", dotnetRoot);
            }
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("DOTNET_ROOT", _previousDotnetRoot);
            if (OperatingSystem.IsWindows())
            {
                Environment.SetEnvironmentVariable("DOTNET_ROOT_X64", _previousDotnetRootX64);
            }
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static NoopDisposable Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
