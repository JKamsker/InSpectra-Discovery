namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.CliFx.Crawling;

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

    [Fact]
    public void Prefers_first_title_line_over_noisy_preamble_lines()
    {
        var parser = new CliFxHelpTextParser();
        var document = parser.Parse(
            """
            C:\Temp\logs\trace.txt
            Another noisy line
            msworddiff 0.4.4
            Compare Word documents

            USAGE
              msworddiff compare <left> <right>
            """);

        Assert.Equal("msworddiff", document.Title);
        Assert.Equal("0.4.4", document.Version);
        Assert.Equal("Compare Word documents", document.ApplicationDescription);
    }

    [Fact]
    public void Parses_TitleCase_Section_Headers()
    {
        var parser = new CliFxHelpTextParser();
        var document = parser.Parse(
            """
            Training Modules convertor v0.0.9
              Training Modules convertor

            Usage
              dotnet tool.dll [command] [options]

            Options
              -h|--help         Shows help text.

            Commands
              convert           Convert a module.
            """);

        Assert.Equal("Training Modules convertor", document.Title);
        Assert.Equal("v0.0.9", document.Version);
        Assert.Single(document.UsageLines);
        Assert.Single(document.Options);
        Assert.Single(document.Commands);
        Assert.Equal("convert", document.Commands[0].Key);
    }

    [Fact]
    public void Ignores_Narrative_Paragraph_Rows_In_Commands_Sections()
    {
        var parser = new CliFxHelpTextParser();
        var document = parser.Parse(
            """
            Rember v0.0.4

            USAGE
              rember [command] [...]

            COMMANDS
              init              Creates a pre-push git hook.

              By default the hook will both build and test your code unless specified otherwise with the -b and -t flags.
              Alternatively you can point to a config yml file using the -f flag.
            """);

        var commands = Assert.Single(document.Commands);
        Assert.Equal("init", commands.Key);
        Assert.Equal(
            """
            Creates a pre-push git hook.
            By default the hook will both build and test your code unless specified otherwise with the -b and -t flags.
            Alternatively you can point to a config yml file using the -f flag.
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            commands.Description);
    }

    [Fact]
    public void Treats_Wrapped_Narrative_Command_Paragraphs_As_Command_Descriptions()
    {
        var parser = new CliFxHelpTextParser();
        var document = parser.Parse(
            """
            Rember v0.0.4

            USAGE
              rember [command] [...]

            COMMANDS
              init              Creates a pre-push git hook.

              By default, you will be asked whether you want to run builds and tests which is great if you are using the terminal
              but will probably cause issues with GUIs. Be sure to use the -y flag if you are using GUIs.
            """);

        var command = Assert.Single(document.Commands);
        Assert.Equal("init", command.Key);
        Assert.Equal(
            """
            Creates a pre-push git hook.
            By default, you will be asked whether you want to run builds and tests which is great if you are using the terminal
            but will probably cause issues with GUIs. Be sure to use the -y flag if you are using GUIs.
            """.Replace("\r\n", "\n", StringComparison.Ordinal),
            command.Description);
    }
}
