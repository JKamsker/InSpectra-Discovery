namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Inference.Descriptions;
using InSpectra.Discovery.Tool.Help.Signatures;

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

    [Theory]
    [InlineData("--indent-size", "Override: indent size.", "INDENT_SIZE")]
    [InlineData("--attributes-max", "Override: max attributes per line.", "ATTRIBUTES_MAX")]
    [InlineData("--attributes-max-chars", "Override: max attribute characters per line.", "ATTRIBUTES_MAX_CHARS")]
    [InlineData("--indent-tabs", "Override: indent with tabs.", null)]
    public void InferArgumentName_Uses_Override_Text_With_ValueLike_Option_Names(
        string signatureText,
        string description,
        string? expectedArgumentName)
    {
        var signature = OptionSignatureSupport.Parse(signatureText);

        var argumentName = OptionDescriptionInference.InferArgumentName(signature, description);

        Assert.Equal(expectedArgumentName, argumentName);
    }

    [Theory]
    [InlineData("--saveimages", "(Default: none) Save images referenced in docs (some|none|all).", "SAVEIMAGES")]
    [InlineData("--suppress", "Suppress the given class(es). Removes these classes (fully qualified names) from the execution set. Separate by ','.", "SUPPRESS")]
    [InlineData("--replace", "Replace string.", "REPLACE")]
    public void InferArgumentName_Recognizes_Current_CommandLineParser_Value_Phrases(
        string signatureText,
        string description,
        string expectedArgumentName)
    {
        var signature = OptionSignatureSupport.Parse(signatureText);

        var argumentName = OptionDescriptionInference.InferArgumentName(signature, description);

        Assert.Equal(expectedArgumentName, argumentName);
    }
}
