namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class AutoModeSupportTests
{
    [Theory]
    [InlineData("clifx", "System.CommandLine", "clifx")]
    [InlineData("static", "CliFx", "static")]
    [InlineData("help", "CliFx", "help")]
    [InlineData("native", "CliFx", "clifx")]
    [InlineData("native", "System.CommandLine", "static")]
    [InlineData("native", null, "help")]
    [InlineData("unexpected", "DocoptNet", "help")]
    public void ResolveFallbackMode_Returns_Expected_Mode(string preferredMode, string? cliFramework, string expectedMode)
    {
        var descriptor = new ToolDescriptor(
            "Sample.Tool",
            "1.2.3",
            "sample",
            cliFramework,
            preferredMode,
            "test",
            "https://www.nuget.org/packages/Sample.Tool/1.2.3",
            "https://nuget.test/sample.tool.1.2.3.nupkg",
            "https://nuget.test/catalog/sample.tool.1.2.3.json");

        var mode = AutoModeSupport.ResolveFallbackMode(descriptor);

        Assert.Equal(expectedMode, mode);
    }
}


