namespace InSpectra.Discovery.Tool.Tests;

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
}

