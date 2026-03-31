namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Signatures;

using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

public sealed partial class OptionSignatureSupportTests
{
    [Fact]
    public void Parse_Normalizes_Pipe_Delimited_Aliases_And_Placeholders()
    {
        var signature = OptionSignatureSupport.Parse("--format|f <output-type>");

        Assert.Equal("--format", signature.PrimaryName);
        Assert.Equal(["-f"], signature.Aliases);
        Assert.Equal("OUTPUT_TYPE", signature.ArgumentName);
        Assert.True(signature.ArgumentRequired);
    }

    [Fact]
    public void AppearsInOptionClause_Detects_Preceding_Option_Token()
    {
        var line = "--output <path>";
        var match = PlaceholderRegex().Match(line);

        var appearsInOptionClause = OptionSignatureSupport.AppearsInOptionClause(line, match);

        Assert.True(appearsInOptionClause);
    }

    [Fact]
    public void InferArgumentNameFromOption_Normalizes_Separators()
    {
        var argumentName = OptionSignatureSupport.InferArgumentNameFromOption("--output-path");

        Assert.Equal("OUTPUT_PATH", argumentName);
    }

    [Fact]
    public void HasValueLikeOptionName_Distinguishes_Value_And_Flag_Options()
    {
        Assert.True(OptionSignatureSupport.HasValueLikeOptionName("--repository-url"));
        Assert.False(OptionSignatureSupport.HasValueLikeOptionName("--verbose"));
    }

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex PlaceholderRegex();
}

