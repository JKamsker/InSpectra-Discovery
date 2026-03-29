using System.Text.Json;
using Xunit;

public sealed class CliFrameworkCatalogClassifierTests
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

        var detected = CliFrameworkCatalogClassifier.Detect(catalogLeaf);

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

        var detected = CliFrameworkCatalogClassifier.Detect(catalogLeaf);

        Assert.Equal("Mono.Options / NDesk.Options", detected);
    }
}
