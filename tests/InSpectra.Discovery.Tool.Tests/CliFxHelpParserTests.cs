using Xunit;

public sealed class CliFxHelpParserTests
{
    [Fact]
    public void Parses_root_help_sections_and_relative_commands()
    {
        var parser = new CliFxHelpTextParser();
        var document = parser.Parse(
            """
            DemoTool v1.2.3
            Demo application

            USAGE
              demo [command] [...]

            OPTIONS
              -h|--help         Shows help text.
              --version         Shows version information.

            COMMANDS
              user              Manage users. Subcommands: user list, user add.
              report export     Export a report.

            You can run "demo [command] --help" to show help for a specific command.
            """);

        Assert.Equal("DemoTool", document.Title);
        Assert.Equal("v1.2.3", document.Version);
        Assert.Equal("Demo application", document.ApplicationDescription);
        Assert.Single(document.UsageLines);
        Assert.Equal(2, document.Options.Count);
        Assert.Equal(2, document.Commands.Count);
        Assert.Equal("user", document.Commands[0].Key);
        Assert.Equal("report export", document.Commands[1].Key);
    }

    [Fact]
    public void Parses_named_command_parameters_and_options()
    {
        var parser = new CliFxHelpTextParser();
        var document = parser.Parse(
            """
            USAGE
              demo user add <name> [options]

            DESCRIPTION
              Adds a user.

            PARAMETERS
            * name              User display name.

            OPTIONS
              -a|--admin        Grants admin permissions.
              --age             User age.
            """);

        Assert.Equal("Adds a user.", document.CommandDescription);
        Assert.Single(document.Parameters);
        Assert.True(document.Parameters[0].IsRequired);
        Assert.Equal("name", document.Parameters[0].Key);
        Assert.Equal(2, document.Options.Count);
        Assert.Equal("-a|--admin", document.Options[0].Key);
        Assert.Equal("--age", document.Options[1].Key);
    }
}
