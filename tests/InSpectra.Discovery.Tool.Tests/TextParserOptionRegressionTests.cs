namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class TextParserOptionRegressionTests
{
    [Fact]
    public void Parses_Short_Alias_Followed_By_Long_Option_Column()
    {
        var parser = new TextParser();

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

    [Fact]
    public void Parses_Wrapped_CommandLineParser_Option_Descriptions_From_Indented_Blocks()
    {
        var parser = new TextParser();

        var document = parser.Parse(
            """
            xstyler 3.2501.8

              -c, --config                         JSON file containing XAML Styler settings
                                                   configuration.

              --attributes-max-chars               Override: max attribute characters per
                                                   line.

              --remove-empty-ending-tag            Override: remove ending tag of empty
                                                   element.
            """);

        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "-c, --config", StringComparison.Ordinal)
            && string.Equals(option.Description, "JSON file containing XAML Styler settings\nconfiguration.", StringComparison.Ordinal));
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "--attributes-max-chars", StringComparison.Ordinal)
            && string.Equals(option.Description, "Override: max attribute characters per\nline.", StringComparison.Ordinal));
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "--remove-empty-ending-tag", StringComparison.Ordinal)
            && string.Equals(option.Description, "Override: remove ending tag of empty\nelement.", StringComparison.Ordinal));
    }

    [Fact]
    public void Parses_Multiword_And_Bracketed_Positional_Argument_Rows()
    {
        var parser = new TextParser();

        var document = parser.Parse(
            """
            tool 1.0.0

            Arguments:
              <files> (pos. 0)       Required. Files to process.
              input file (pos. 1)    Optional input file to hash.
            """);

        Assert.Contains(document.Arguments, argument =>
            string.Equals(argument.Key, "files", StringComparison.Ordinal)
            && argument.IsRequired
            && string.Equals(argument.Description, "Files to process.", StringComparison.Ordinal));
        Assert.Contains(document.Arguments, argument =>
            string.Equals(argument.Key, "input file", StringComparison.Ordinal)
            && !argument.IsRequired
            && string.Equals(argument.Description, "Optional input file to hash.", StringComparison.Ordinal));
    }

    [Fact]
    public void Starts_New_Long_Option_Rows_Instead_Of_Merging_Them_Into_Previous_Descriptions()
    {
        var parser = new TextParser();

        var document = parser.Parse(
            """
            Usage: nake [options]

            Options:
               -d  --directory DIR    Use DIR as current directory
                   --runner NAME      Use NAME as runner file name in task listing
               -t  --trace            Enables task execution tracing
                   --debug            Enables full script debugging in Visual Studio
            """);

        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "-d | --directory <DIR>", StringComparison.Ordinal)
            && string.Equals(option.Description, "Use DIR as current directory", StringComparison.Ordinal));
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "--runner <NAME>", StringComparison.Ordinal)
            && string.Equals(option.Description, "Use NAME as runner file name in task listing", StringComparison.Ordinal));
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "-t | --trace", StringComparison.Ordinal)
            && string.Equals(option.Description, "Enables task execution tracing", StringComparison.Ordinal));
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "--debug", StringComparison.Ordinal)
            && string.Equals(option.Description, "Enables full script debugging in Visual Studio", StringComparison.Ordinal));
    }
}

