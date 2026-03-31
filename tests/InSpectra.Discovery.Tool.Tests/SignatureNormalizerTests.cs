namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Signatures;

using Xunit;

public sealed class SignatureNormalizerTests
{
    [Fact]
    public void NormalizeCommandKey_Prefers_Longest_Alias_Per_Segment()
    {
        var key = SignatureNormalizer.NormalizeCommandKey("cfg, config set, s <name>");

        Assert.Equal("config set", key);
    }

    [Fact]
    public void NormalizeCommandItemLine_Rewrites_Prompt_Style_Command_Rows()
    {
        var line = SignatureNormalizer.NormalizeCommandItemLine("> list: show items");

        Assert.Equal("list  show items", line);
    }

    [Fact]
    public void NormalizeOptionSignatureKey_Normalizes_Bare_Placeholders()
    {
        var key = SignatureNormalizer.NormalizeOptionSignatureKey("--output path");

        Assert.Equal("--output <PATH>", key);
    }

    [Fact]
    public void TryExtractLeadingAliasFromDescription_Consumes_Split_Column_Placeholders()
    {
        var matched = SignatureNormalizer.TryExtractLeadingAliasFromDescription(
            "-o OUTPUT write file",
            out var alias,
            out var normalizedDescription);

        Assert.True(matched);
        Assert.Equal("-o <OUTPUT>", alias);
        Assert.Equal("write file", normalizedDescription);
    }
}

