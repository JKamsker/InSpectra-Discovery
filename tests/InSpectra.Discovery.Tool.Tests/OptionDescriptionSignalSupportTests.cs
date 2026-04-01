namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Inference.Descriptions;
using InSpectra.Discovery.Tool.Help.Signatures;

using Xunit;

public sealed class OptionDescriptionSignalSupportTests
{
    [Fact]
    public void LooksLikeFlagDescription_Recognizes_Recursive_Flag_Prefix()
    {
        var looksLikeFlag = OptionDescriptionSignalSupport.LooksLikeFlagDescription(
            "Recursively process the specified directory.");

        Assert.True(looksLikeFlag);
    }

    [Fact]
    public void ContainsStrongValueDescriptionHint_Recognizes_Path_Hints()
    {
        var hasStrongValueHint = OptionDescriptionSignalSupport.ContainsStrongValueDescriptionHint(
            "Path to the output directory.");

        Assert.True(hasStrongValueHint);
    }

    [Fact]
    public void ContainsInlineOptionExample_Ignores_Reference_Words_After_Option()
    {
        var signature = OptionSignatureSupport.Parse("--output");

        var containsExample = OptionDescriptionSignalSupport.ContainsInlineOptionExample(
            signature,
            "The value passed to --output is used for writing.");

        Assert.False(containsExample);
    }

    [Fact]
    public void ContainsInlineOptionExample_Detects_Concrete_Example_Value()
    {
        var signature = OptionSignatureSupport.Parse("--output");

        var containsExample = OptionDescriptionSignalSupport.ContainsInlineOptionExample(
            signature,
            "Equivalent to passing --output result.json on the command line.");

        Assert.True(containsExample);
    }

    [Fact]
    public void ContainsIllustrativeValueExample_Recognizes_Pipe_Delimited_Choice_Sets()
    {
        var containsExample = OptionDescriptionSignalSupport.ContainsIllustrativeValueExample(
            "Save images referenced in docs (some|none|all).");

        Assert.True(containsExample);
    }
}
