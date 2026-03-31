namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Parsing;

using Xunit;

public sealed class TextParserTitleRegressionTests
{
    [Fact]
    public void Prefers_Descriptive_Title_When_Version_Line_Is_Separate_Label()
    {
        var parser = new TextParser();

        var document = parser.Parse(
            """
            DotNetAnalyzer - .NET MCP Server for Claude Code
            Version: 1.1.2

            Usage:
              dotnet-analyzer [options] [command]

            Options:
              -v, --version     Show version information
              -h, --help        Show help information

            Commands:
              mcp serve         Start MCP server (default)

            When run without options, dotnet-analyzer starts as an MCP server
            and waits for stdio input (for use with Claude Code).

            For more information, visit:
              https://github.com/CartapenaBark/DotNetAnalyzer
            """);

        Assert.Equal("DotNetAnalyzer - .NET MCP Server for Claude Code", document.Title);
        Assert.Equal("1.1.2", document.Version);
        var command = Assert.Single(document.Commands);
        Assert.Equal("mcp serve", command.Key);
        Assert.Equal("Start MCP server (default)", command.Description);
    }

    [Fact]
    public void Skips_Banner_Link_And_Boxed_Version_Preamble_When_Finding_Title()
    {
        var parser = new TextParser();

        var document = parser.Parse(
            """
            W! - https://whizba.ng/
            | Whizbang CLI v0.1.0 (Whizbang v0.54.1) |

            Whizbang CLI - Command-line tool for Whizbang
            Version 0.1.0

            Usage: whizbang <command> [options]

            Commands:
              schema          Manage database schemas
              migrate         Migrate from Marten/Wolverine to Whizbang

            Options:
              --help, -h      Show this help message
              --version, -v   Show version information

            Run 'whizbang <command> --help' for more information on a command.
            """);

        Assert.Equal("Whizbang CLI - Command-line tool for Whizbang", document.Title);
        Assert.Equal("0.1.0", document.Version);
        Assert.Equal(2, document.Commands.Count);
        Assert.Equal("schema", document.Commands[0].Key);
        Assert.Equal("migrate", document.Commands[1].Key);
        Assert.Equal(2, document.Options.Count);
        Assert.Equal("Show version information", document.Options[1].Description);
    }
}
