using Xunit;

public sealed class PackageVersionResolverTests
{
    [Theory]
    [InlineData("https://github.com/example/tool.git", "https://github.com/example/tool")]
    [InlineData("https://github.com/example/tool", "https://github.com/example/tool")]
    [InlineData(" https://github.com/example/tool.git ", "https://github.com/example/tool")]
    [InlineData("not-a-url.git", "not-a-url.git")]
    public void NormalizeRepositoryUrl_NormalizesExpectedForms(string input, string expected)
    {
        Assert.Equal(expected, PackageVersionResolver.NormalizeRepositoryUrl(input));
    }

    [Fact]
    public void NormalizeRepositoryUrl_ReturnsNullForWhitespace()
    {
        Assert.Null(PackageVersionResolver.NormalizeRepositoryUrl("   "));
    }
}
