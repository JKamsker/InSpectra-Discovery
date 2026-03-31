namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Inference.Descriptions;

using Xunit;

public sealed class RequiredDescriptionSupportTests
{
    [Theory]
    [InlineData("Required. Path to config", "Path to config")]
    [InlineData("Required path to config", "path to config")]
    [InlineData("(REQUIRED) Path to config", "Path to config")]
    [InlineData("[REQUIRED] Path to config", "Path to config")]
    public void TrimLeadingRequiredPrefix_Strips_Supported_Prefixes(string description, string expected)
    {
        Assert.True(RequiredDescriptionSupport.StartsWithRequiredPrefix(description));
        Assert.Equal(expected, RequiredDescriptionSupport.TrimLeadingRequiredPrefix(description));
    }

    [Fact]
    public void TrimLeadingRequiredPrefix_Leaves_Ordinary_Descriptions_Untouched()
    {
        const string description = "Path to config";

        Assert.False(RequiredDescriptionSupport.StartsWithRequiredPrefix(description));
        Assert.Equal(description, RequiredDescriptionSupport.TrimLeadingRequiredPrefix(description));
    }
}

