namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.NuGet;
using InSpectra.Discovery.Tool.Queue.Planning;

using Xunit;

public sealed class RunnerSelectionResolverTests
{
    [Fact]
    public void SelectRunner_UsesWindowsForWindowsDesktop()
    {
        var selection = RunnerSelectionResolver.SelectRunner(
            ["Microsoft.WindowsDesktop.App"],
            [],
            [],
            inspectionError: null,
            hintSource: "test");

        Assert.Equal("windows-latest", selection.RunsOn);
        Assert.Equal("framework-microsoft.windowsdesktop.app", selection.Reason);
    }

    [Fact]
    public void SelectRunner_UsesMacOsForMacOnlyToolRids()
    {
        var selection = RunnerSelectionResolver.SelectRunner(
            [],
            ["osx-x64", "osx-arm64"],
            [],
            inspectionError: null,
            hintSource: "test");

        Assert.Equal("macos-latest", selection.RunsOn);
        Assert.Equal("tool-rids-macos-only", selection.Reason);
    }

    [Fact]
    public void SelectRunner_UsesMacOsForMacOnlyRuntimeRids()
    {
        var selection = RunnerSelectionResolver.SelectRunner(
            [],
            [],
            ["osx-x64"],
            inspectionError: null,
            hintSource: "test");

        Assert.Equal("macos-latest", selection.RunsOn);
        Assert.Equal("runtime-rids-macos-only", selection.Reason);
    }

    [Fact]
    public void SelectRunner_DefaultsToUbuntuOtherwise()
    {
        var selection = RunnerSelectionResolver.SelectRunner(
            ["Microsoft.NETCore.App"],
            ["linux-x64"],
            [],
            inspectionError: null,
            hintSource: "test");

        Assert.Equal("ubuntu-latest", selection.RunsOn);
        Assert.Equal("default-ubuntu", selection.Reason);
    }

    [Fact]
    public void TryResolveFromCatalog_UsesWindowsForWindowsOnlyToolRid()
    {
        var catalogLeaf = new CatalogLeaf(
            "https://nuget.test/catalog/sample.tool.1.0.0.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            [
                new CatalogPackageEntry("tools/net8.0/win-x64/Sample.Tool.dll", "Sample.Tool.dll"),
            ],
            DependencyGroups: null,
            PackageTypes: null);

        var selection = RunnerSelectionResolver.TryResolveFromCatalog(catalogLeaf);

        Assert.NotNull(selection);
        Assert.Equal("windows-latest", selection!.RunsOn);
        Assert.Equal("tool-rids-windows-only", selection.Reason);
        Assert.Contains("win-x64", selection.ToolRids);
    }

    [Fact]
    public void TryResolveFromCatalog_RecognizesWindowsDesktopFromWindowsTargetFramework()
    {
        var catalogLeaf = new CatalogLeaf(
            "https://nuget.test/catalog/sample.tool.1.0.0.json",
            Title: null,
            Description: null,
            ProjectUrl: null,
            Repository: null,
            [
                new CatalogPackageEntry("tools/net8.0-windows/any/Sample.Tool.dll", "Sample.Tool.dll"),
            ],
            DependencyGroups: null,
            PackageTypes: null);

        var selection = RunnerSelectionResolver.TryResolveFromCatalog(catalogLeaf);

        Assert.NotNull(selection);
        Assert.Equal("windows-latest", selection!.RunsOn);
        Assert.Equal("framework-microsoft.windowsdesktop.app", selection.Reason);
        Assert.Contains("Microsoft.WindowsDesktop.App", selection.RequiredFrameworks);
    }
}
