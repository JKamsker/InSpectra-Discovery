namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.Tools;
using InSpectra.Discovery.Tool.Catalog.Filtering.SpectreConsole;
using InSpectra.Discovery.Tool.NuGet;
using InSpectra.Discovery.Tool.Packages;

using Xunit;

public sealed class ToolDescriptorResolverTests
{
    [Fact]
    public void ResolveFromCatalogLeaf_UsesCandidateReasonWithoutArchiveInspection()
    {
        var catalogLeaf = new CatalogLeaf(
            "https://nuget.test/catalog/sample.tool.1.0.0.json",
            Title: "Sample Tool",
            Description: "Sample description",
            ProjectUrl: null,
            Repository: null,
            [
                new CatalogPackageEntry("tools/net8.0/any/System.CommandLine.dll", "System.CommandLine.dll"),
                new CatalogPackageEntry("tools/net8.0/any/Sample.Tool.dll", "Sample.Tool.dll"),
            ],
            [
                new CatalogDependencyGroup(
                    [
                        new CatalogDependency("System.CommandLine"),
                    ]),
            ],
            PackageTypes: null);

        var descriptor = ToolDescriptorResolver.ResolveFromCatalogLeaf(
            "Sample.Tool",
            "1.0.0",
            catalogLeaf,
            packageUrl: "https://www.nuget.org/packages/Sample.Tool/1.0.0",
            packageContentUrl: "https://nuget.test/sample.tool.1.0.0.nupkg",
            catalogEntryUrl: catalogLeaf.Id);

        Assert.Equal("Sample.Tool", descriptor.PackageId);
        Assert.Equal("System.CommandLine", descriptor.CliFramework);
        Assert.Equal("static", descriptor.PreferredAnalysisMode);
        Assert.Equal("candidate-static-analysis-framework", descriptor.SelectionReason);
        Assert.Null(descriptor.HookCliFramework);
        Assert.Null(descriptor.CommandName);
    }

    [Fact]
    public void ResolveFromCatalogLeaf_ConfirmsHookFramework_WhenToolAssemblyReferencesIt()
    {
        var catalogLeaf = new CatalogLeaf(
            "https://nuget.test/catalog/sample.tool.1.0.0.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            [
                new CatalogPackageEntry("tools/net8.0/any/System.CommandLine.dll", "System.CommandLine.dll"),
                new CatalogPackageEntry("tools/net8.0/any/Sample.Tool.dll", "Sample.Tool.dll"),
            ],
            [
                new CatalogDependencyGroup(
                    [
                        new CatalogDependency("System.CommandLine"),
                    ]),
            ],
            PackageTypes: null);

        var packageInspection = SpectrePackageInspection.Empty with
        {
            ToolCliFrameworkReferences =
            [
                new ToolCliFrameworkReferenceInspection("System.CommandLine", ["tools/net8.0/any/Sample.Tool.dll"]),
            ],
        };

        var descriptor = ToolDescriptorResolver.ResolveFromCatalogLeaf(
            "Sample.Tool",
            "1.0.0",
            catalogLeaf,
            packageUrl: null,
            packageContentUrl: null,
            catalogEntryUrl: catalogLeaf.Id,
            packageInspection);

        Assert.Equal("confirmed-static-analysis-framework", descriptor.SelectionReason);
        Assert.Equal("System.CommandLine", descriptor.HookCliFramework);
    }

    [Fact]
    public void ResolveFromCatalogLeaf_OnlyConfirmsHookEligibleSubset_ForCompositeFrameworkHints()
    {
        var catalogLeaf = new CatalogLeaf(
            "https://nuget.test/catalog/sample.tool.1.0.0.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            [
                new CatalogPackageEntry("tools/net8.0/any/System.CommandLine.dll", "System.CommandLine.dll"),
                new CatalogPackageEntry("tools/net8.0/any/CommandLine.dll", "CommandLine.dll"),
                new CatalogPackageEntry("tools/net8.0/any/Sample.Tool.dll", "Sample.Tool.dll"),
            ],
            [
                new CatalogDependencyGroup(
                    [
                        new CatalogDependency("System.CommandLine"),
                        new CatalogDependency("CommandLineParser"),
                    ]),
            ],
            PackageTypes: null);

        var packageInspection = SpectrePackageInspection.Empty with
        {
            ToolCliFrameworkReferences =
            [
                new ToolCliFrameworkReferenceInspection("System.CommandLine", ["tools/net8.0/any/Sample.Tool.dll"]),
            ],
        };

        var descriptor = ToolDescriptorResolver.ResolveFromCatalogLeaf(
            "Sample.Tool",
            "1.0.0",
            catalogLeaf,
            packageUrl: null,
            packageContentUrl: null,
            catalogEntryUrl: catalogLeaf.Id,
            packageInspection);

        Assert.Equal("System.CommandLine + CommandLineParser", descriptor.CliFramework);
        Assert.Equal("System.CommandLine", descriptor.HookCliFramework);
        Assert.Equal("confirmed-static-analysis-framework", descriptor.SelectionReason);
    }

    [Fact]
    public void ResolveFromCatalogLeaf_ConfirmsSpectreWithoutArchiveInspection()
    {
        var catalogLeaf = new CatalogLeaf(
            "https://nuget.test/catalog/spectre.tool.1.0.0.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            [
                new CatalogPackageEntry("tools/net8.0/any/Spectre.Console.Cli.dll", "Spectre.Console.Cli.dll"),
            ],
            DependencyGroups: null,
            PackageTypes: null);

        var descriptor = ToolDescriptorResolver.ResolveFromCatalogLeaf(
            "Spectre.Tool",
            "1.0.0",
            catalogLeaf,
            packageUrl: null,
            packageContentUrl: null,
            catalogEntryUrl: catalogLeaf.Id);

        Assert.Equal("Spectre.Console.Cli", descriptor.CliFramework);
        Assert.Equal("native", descriptor.PreferredAnalysisMode);
        Assert.Equal("confirmed-spectre-console-cli", descriptor.SelectionReason);
    }
}
