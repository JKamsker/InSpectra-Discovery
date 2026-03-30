namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class ToolHelpOptionDescriptionInferenceTests
{
    [Fact]
    public void InferArgumentName_Does_Not_Treat_Recursive_Flag_Nouns_As_Value_Evidence()
    {
        var signature = ToolHelpOptionSignatureSupport.Parse("-r, --recursive");

        var argumentName = ToolHelpOptionDescriptionInference.InferArgumentName(
            signature,
            "(Default: false) Recursively process specified directory.");

        Assert.Null(argumentName);
    }
}

