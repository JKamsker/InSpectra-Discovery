namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class OptionDescriptionInferenceTests
{
    [Fact]
    public void InferArgumentName_Does_Not_Treat_Recursive_Flag_Nouns_As_Value_Evidence()
    {
        var signature = OptionSignatureSupport.Parse("-r, --recursive");

        var argumentName = OptionDescriptionInference.InferArgumentName(
            signature,
            "(Default: false) Recursively process specified directory.");

        Assert.Null(argumentName);
    }
}

