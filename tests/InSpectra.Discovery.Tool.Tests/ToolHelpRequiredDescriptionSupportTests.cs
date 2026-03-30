using Xunit;

public sealed class ToolHelpRequiredDescriptionSupportTests
{
    [Theory]
    [InlineData("Required. Path to config", "Path to config")]
    [InlineData("Required path to config", "path to config")]
    [InlineData("(REQUIRED) Path to config", "Path to config")]
    [InlineData("[REQUIRED] Path to config", "Path to config")]
    public void TrimLeadingRequiredPrefix_Strips_Supported_Prefixes(string description, string expected)
    {
        Assert.True(ToolHelpRequiredDescriptionSupport.StartsWithRequiredPrefix(description));
        Assert.Equal(expected, ToolHelpRequiredDescriptionSupport.TrimLeadingRequiredPrefix(description));
    }

    [Fact]
    public void TrimLeadingRequiredPrefix_Leaves_Ordinary_Descriptions_Untouched()
    {
        const string description = "Path to config";

        Assert.False(ToolHelpRequiredDescriptionSupport.StartsWithRequiredPrefix(description));
        Assert.Equal(description, ToolHelpRequiredDescriptionSupport.TrimLeadingRequiredPrefix(description));
    }
}
