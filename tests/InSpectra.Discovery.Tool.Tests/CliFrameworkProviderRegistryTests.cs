namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class CliFrameworkProviderRegistryTests
{
    [Fact]
    public void Detect_ReturnsCombinedFrameworks_WhenDependenciesAndAssembliesMatch()
    {
        var catalogLeaf = new CatalogLeaf(
            "https://nuget.test/catalog/sample.tool.1.0.0.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            [new CatalogPackageEntry("tools/net10.0/any/CliFx.dll", "CliFx.dll")],
            [new CatalogDependencyGroup([new CatalogDependency("System.CommandLine")])],
            PackageTypes: null);

        var detected = CliFrameworkProviderRegistry.Detect(catalogLeaf);

        Assert.Equal("CliFx + System.CommandLine", detected);
    }

    [Fact]
    public void Detect_RecognizesMonoOptionsFromAssemblyName()
    {
        var catalogLeaf = new CatalogLeaf(
            "https://nuget.test/catalog/sample.tool.1.0.0.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            [new CatalogPackageEntry("tools/net10.0/any/Mono.Options.dll", "Mono.Options.dll")],
            DependencyGroups: null,
            PackageTypes: null);

        var detected = CliFrameworkProviderRegistry.Detect(catalogLeaf);

        Assert.Equal("Mono.Options / NDesk.Options", detected);
    }

    [Theory]
    [InlineData("CliFx", true)]
    [InlineData("CliFx + System.CommandLine", true)]
    [InlineData("System.CommandLine + CliFx", true)]
    [InlineData("System.CommandLine", false)]
    [InlineData(null, false)]
    public void HasCliFxAnalysisSupport_Detects_CliFx_In_Combined_Labels(string? cliFramework, bool expected)
    {
        Assert.Equal(expected, CliFrameworkProviderRegistry.HasCliFxAnalysisSupport(cliFramework));
    }

    [Theory]
    [InlineData("System.CommandLine", true)]
    [InlineData("CliFx + System.CommandLine", true)]
    [InlineData("CommandLineParser", true)]
    [InlineData("DocoptNet", false)]
    [InlineData("Oakton", false)]
    [InlineData("Mono.Options / NDesk.Options", false)]
    [InlineData(null, false)]
    public void HasStaticAnalysisSupport_Only_Returns_True_For_Frameworks_With_Registered_Adapters(string? cliFramework, bool expected)
    {
        Assert.Equal(expected, CliFrameworkProviderRegistry.HasStaticAnalysisSupport(cliFramework));
    }

    [Fact]
    public void ResolveStaticAnalysisAdapter_Skips_CliFx_And_Uses_SystemCommandLine_From_Combined_Label()
    {
        var adapter = CliFrameworkProviderRegistry.ResolveStaticAnalysisAdapter("CliFx + System.CommandLine");

        Assert.NotNull(adapter);
        Assert.Equal("System.CommandLine", adapter.FrameworkName);
        Assert.Equal("System.CommandLine", adapter.AssemblyName);
        Assert.IsType<SystemCommandLineAttributeReader>(adapter.Reader);
    }

    [Theory]
    [InlineData(null, "CliFx + System.CommandLine", true)]
    [InlineData("CliFx", "CliFx + System.CommandLine", true)]
    [InlineData("System.CommandLine", "CliFx + System.CommandLine", true)]
    [InlineData("CliFx + System.CommandLine", "CliFx + System.CommandLine", false)]
    [InlineData("CliFx + System.CommandLine", "CliFx", false)]
    [InlineData("System.CommandLine", "System.CommandLine", false)]
    public void ShouldReplace_Upgrades_Only_When_Candidate_Adds_CliFx(
        string? existingCliFramework,
        string? candidateCliFramework,
        bool expected)
    {
        Assert.Equal(expected, CliFrameworkProviderRegistry.ShouldReplace(existingCliFramework, candidateCliFramework));
    }
}

