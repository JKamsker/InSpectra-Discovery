using Xunit;

public sealed class ToolHelpTextParserOptionRegressionTests
{
    [Fact]
    public void Parses_Short_Alias_Followed_By_Long_Option_Column()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Nake 4.0.0

            Options:
              -f  --nakefile FILE    Path to the Nakefile to execute.
              -T  --target NAME      Target to run.
            """);

        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "-f | --nakefile <FILE>", StringComparison.Ordinal)
            && string.Equals(option.Description, "Path to the Nakefile to execute.", StringComparison.Ordinal));
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "-T | --target <NAME>", StringComparison.Ordinal)
            && string.Equals(option.Description, "Target to run.", StringComparison.Ordinal));
    }
}
