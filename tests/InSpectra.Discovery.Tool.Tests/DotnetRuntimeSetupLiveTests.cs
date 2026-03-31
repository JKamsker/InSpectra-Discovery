namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.NuGet;
using InSpectra.Discovery.Tool.Queue.Planning;

using System.Text.Json.Nodes;
using Xunit;
using Xunit.Abstractions;

[Collection("LiveToolAnalysis")]
public sealed class DotnetRuntimeSetupLiveTests
{
    private const string EnableEnvVar = "INSPECTRA_DISCOVERY_LIVE_RUNTIME_SETUP_TESTS";
    private readonly ITestOutputHelper _output;

    public DotnetRuntimeSetupLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static TheoryData<LiveRuntimeSetupCase> Cases()
    {
        var data = new TheoryData<LiveRuntimeSetupCase>();
        data.Add(new LiveRuntimeSetupCase(
            "dotnet-mgcb-editor-windows",
            "3.8.5-preview.3",
            [
                "Microsoft.NETCore.App|8.0|dotnet",
            ]));
        data.Add(new LiveRuntimeSetupCase(
            "dotnet-tdcb-editor",
            "4.0.0.71-develop",
            [
                "Microsoft.NETCore.App|6.0|dotnet",
            ]));
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Category", "Live")]
    public async Task ResolveForPlanItemAsync_UsesExpectedRuntimeRequirements_ForRealPackages(LiveRuntimeSetupCase testCase)
    {
        if (!ShouldRun())
        {
            return;
        }

        var item = new JsonObject
        {
            ["packageContentUrl"] = $"https://api.nuget.org/v3-flatcontainer/{testCase.PackageId.ToLowerInvariant()}/{testCase.Version.ToLowerInvariant()}/{testCase.PackageId.ToLowerInvariant()}.{testCase.Version.ToLowerInvariant()}.nupkg",
        };

        using var httpClient = new HttpClient();
        var client = new NuGetApiClient(httpClient);

        var plan = await DotnetRuntimeSetupResolver.ResolveForPlanItemAsync(
            item,
            catalogLeaf: null,
            runsOn: "ubuntu-latest",
            client,
            CancellationToken.None);

        Assert.Equal("runtime-only", plan.Mode);
        Assert.Equal("archive-runtimeconfig", plan.Source);
        Assert.Equal(testCase.ExpectedRequirements, plan.RequiredRuntimes
            .Select(requirement => $"{requirement.Name}|{requirement.Channel}|{requirement.Runtime}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());

        _output.WriteLine($"{testCase.PackageId} {testCase.Version} resolved to: {string.Join(", ", testCase.ExpectedRequirements)}");
    }

    private static bool ShouldRun()
        => string.Equals(Environment.GetEnvironmentVariable(EnableEnvVar), "1", StringComparison.Ordinal);

    public sealed record LiveRuntimeSetupCase(
        string PackageId,
        string Version,
        IReadOnlyList<string> ExpectedRequirements)
    {
        public override string ToString()
            => $"{PackageId} {Version}";
    }
}
