namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class ToolHelpOptionDescriptionSignalSupportTests
{
    [Fact]
    public void LooksLikeFlagDescription_Recognizes_Recursive_Flag_Prefix()
    {
        var looksLikeFlag = ToolHelpOptionDescriptionSignalSupport.LooksLikeFlagDescription(
            "Recursively process the specified directory.");

        Assert.True(looksLikeFlag);
    }

    [Fact]
    public void ContainsStrongValueDescriptionHint_Recognizes_Path_Hints()
    {
        var hasStrongValueHint = ToolHelpOptionDescriptionSignalSupport.ContainsStrongValueDescriptionHint(
            "Path to the output directory.");

        Assert.True(hasStrongValueHint);
    }

    [Fact]
    public void ContainsInlineOptionExample_Ignores_Reference_Words_After_Option()
    {
        var signature = ToolHelpOptionSignatureSupport.Parse("--output");

        var containsExample = ToolHelpOptionDescriptionSignalSupport.ContainsInlineOptionExample(
            signature,
            "The value passed to --output is used for writing.");

        Assert.False(containsExample);
    }

    [Fact]
    public void ContainsInlineOptionExample_Detects_Concrete_Example_Value()
    {
        var signature = ToolHelpOptionSignatureSupport.Parse("--output");

        var containsExample = ToolHelpOptionDescriptionSignalSupport.ContainsInlineOptionExample(
            signature,
            "Equivalent to passing --output result.json on the command line.");

        Assert.True(containsExample);
    }
}

