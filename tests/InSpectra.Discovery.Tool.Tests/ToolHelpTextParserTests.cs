using Xunit;

public sealed class ToolHelpTextParserTests
{
    [Fact]
    public void Parses_CliFx_Style_Help_Text()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            husky v0.9.1

            USAGE
              husky [options]
              husky [command] [...]

            OPTIONS
              -h|--help         Shows help text.
              --version         Shows version information.

            COMMANDS
              add               Add husky hook
              install           Install Husky hooks
            """);

        Assert.Equal("husky", document.Title);
        Assert.Equal("v0.9.1", document.Version);
        Assert.Equal(2, document.UsageLines.Count);
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-h|--help", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "add", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "install", StringComparison.Ordinal));
    }

    [Fact]
    public void Parses_Colon_Sections_With_Multiline_Descriptions()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            dotnet-serve 1.10.194

            Usage: dotnet serve [options]

            Options:
              -d|--directory <DIR>   The root directory to serve.
                                      Supports relative paths.
              -v|--verbose           Show more console output.
            """);

        Assert.Equal("dotnet-serve", document.Title);
        Assert.Single(document.UsageLines);
        Assert.Contains(document.Options, option =>
            string.Equals(option.Key, "-d|--directory <DIR>", StringComparison.Ordinal)
            && option.Description!.Contains("Supports relative paths.", StringComparison.Ordinal));
    }

    [Fact]
    public void Parses_Localized_Section_Headers()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            VERWENDUNG:
                dotnet cake [SCRIPT] [OPTIONEN]

            ARGUMENTE:
                [SCRIPT]    The Cake script. Defaults to build.cake

            OPTIONEN:
                -v, --verbosity <VERBOSITY>  Specifies the amount of information to be displayed.
            """);

        Assert.Contains("dotnet cake [SCRIPT] [OPTIONEN]", document.UsageLines);
        Assert.Single(document.Arguments);
        Assert.Equal("SCRIPT", document.Arguments[0].Key);
        Assert.Single(document.Options);
        Assert.Equal("-v, --verbosity <VERBOSITY>", document.Options[0].Key);
    }

    [Fact]
    public void Parses_Subcommands_Section_Alias()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Paket version 10.3.1

            USAGE: paket [<subcommand> [<options>]]

            SUBCOMMANDS:

                add <options>         add a new dependency
                install <options>     compute dependency graph
            """);

        Assert.Single(document.UsageLines);
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "add", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "install", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_Not_Treat_Wrapped_Command_Descriptions_As_New_Commands()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Paket version 10.3.1

            SUBCOMMANDS:

                show-conditions <options>
                                      show conditions defined on groups
                simplify <options>    simplify declared dependencies
            """);

        Assert.Contains(document.Commands, command => string.Equals(command.Key, "show-conditions", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "simplify", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Commands, command => string.Equals(command.Key, "show conditions defined on groups", StringComparison.Ordinal));
    }

    [Fact]
    public void Falls_Back_To_Indented_Command_List_When_No_Commands_Header_Exists()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            snapx 10.0.0+67eae04d993a714128cefbb77cc80fed8b0f7fc3
            Copyright © Finter As

              demote     Demote one or multiple releases
              promote    Promote a snap to next release channel
              pack       Publish a new release
              help       Display more information on a specific command.
            """);

        Assert.Contains(document.Commands, command => string.Equals(command.Key, "demote", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "promote", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "pack", StringComparison.Ordinal));
    }

    [Fact]
    public void Infers_Options_From_Preamble_Without_Options_Header()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Pickles version 0.0.0.0
              -f, --feature-directory=VALUE
                                         directory to start scanning recursively for
                                           features
              -o, --output-directory=VALUE
                                         directory where output files will be placed
              -h, -?, --help
            """);

        Assert.Contains(document.Options, option => string.Equals(option.Key, "-f, --feature-directory=VALUE", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-h, -?, --help", StringComparison.Ordinal));
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Infers_Usage_From_Preamble_Without_Usage_Section_Header()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            DependenSee

            Usage - DependenSee <SourceFolder> [<OutputPath>] -options

            GlobalOption                       Description
            Help (-H)                          Shows help descriptions.
            """);

        Assert.Single(document.UsageLines);
        Assert.Equal("DependenSee <SourceFolder> [<OutputPath>] -options", document.UsageLines[0]);
        Assert.Empty(document.Commands);
    }

    [Fact]
    public void Infers_Legacy_Option_Table_Without_Classifying_Usage_Arguments_As_Options()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Usage - DependenSee <SourceFolder> [<OutputPath>] -options

            GlobalOption                       Description
            Help (-H)                          Shows help descriptions.
            SourceFolder* (-S)                 Root folder.
            OutputPath (-O)                    Output path.
            IncludePackages (-P)               Include packages.
            """);

        Assert.Contains(document.Options, option => string.Equals(option.Key, "-H, --help", StringComparison.Ordinal));
        Assert.Contains(document.Options, option => string.Equals(option.Key, "-P, --include-packages", StringComparison.Ordinal));
        Assert.DoesNotContain(document.Options, option => option.Key.Contains("source-folder", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(document.Options, option => option.Key.Contains("output-path", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Normalizes_Command_Keys_By_Removing_Usage_Placeholders()
    {
        var parser = new ToolHelpTextParser();

        var document = parser.Parse(
            """
            Usage: dotnet-trace [command] [options]

            Commands:
              convert <input-filename>  Converts traces to alternate formats.
              report <trace_filename>   Generates a report.
            """);

        Assert.Contains(document.Commands, command => string.Equals(command.Key, "convert", StringComparison.Ordinal));
        Assert.Contains(document.Commands, command => string.Equals(command.Key, "report", StringComparison.Ordinal));
    }
}
