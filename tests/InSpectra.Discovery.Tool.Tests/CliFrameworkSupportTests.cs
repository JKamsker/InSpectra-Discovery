using Xunit;

public sealed class CliFrameworkSupportTests
{
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
