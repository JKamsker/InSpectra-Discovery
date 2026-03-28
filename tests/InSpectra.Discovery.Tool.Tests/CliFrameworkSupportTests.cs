using Xunit;

public sealed class CliFrameworkSupportTests
{
    [Theory]
    [InlineData("CliFx", true)]
    [InlineData("CliFx + System.CommandLine", true)]
    [InlineData("System.CommandLine + CliFx", true)]
    [InlineData("System.CommandLine", false)]
    [InlineData(null, false)]
    public void HasCliFx_Detects_CliFx_In_Combined_Labels(string? cliFramework, bool expected)
    {
        Assert.Equal(expected, CliFrameworkSupport.HasCliFx(cliFramework));
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
        Assert.Equal(expected, CliFrameworkSupport.ShouldReplace(existingCliFramework, candidateCliFramework));
    }
}
