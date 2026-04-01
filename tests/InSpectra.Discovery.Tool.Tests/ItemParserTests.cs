namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Parsing;

using Xunit;

public sealed class ItemParserTests
{
    [Fact]
    public void ParseItems_Normalizes_Leading_Option_Alias_From_Description()
    {
        var items = ItemParser.ParseItems(
            ["--output  -o OUTPUT write file"],
            ItemKind.Option);

        var item = Assert.Single(items);
        Assert.Equal("--output | -o <OUTPUT>", item.Key);
        Assert.Equal("write file", item.Description);
    }

    [Fact]
    public void SplitArgumentSectionLines_Keeps_Indented_Option_Continuation_With_Options()
    {
        ItemParser.SplitArgumentSectionLines(
            [
                "--output  Write file",
                "  to disk",
                "PATH  input path"
            ],
            out var argumentLines,
            out var optionLines);

        Assert.Equal(["--output  Write file", "  to disk"], optionLines);
        Assert.Equal(["PATH  input path"], argumentLines);
    }

    [Fact]
    public void InferCommands_Drops_Builtin_Auxiliary_Blank_Entries_When_Real_Commands_Appear()
    {
        var commands = ItemParser.InferCommands(
            [
                "Commands:",
                "  build  Build the project",
                "  help"
            ],
            [],
            sawInventoryHeader: false);

        var command = Assert.Single(commands);
        Assert.Equal("build", command.Key);
        Assert.Equal("Build the project", command.Description);
    }

    [Fact]
    public void InferCommands_Uses_Multiline_Usage_Inventories_When_No_Command_Section_Exists()
    {
        var commands = ItemParser.InferCommands(
            [],
            [
                "mcpdebugger serve [--port <port>]   Start the HTTP debug server (default port: 5200)",
                "mcpdebugger mcp [--port <port>]     Start the MCP server (talks to debug server)",
                "mcpdebugger --help                  Show this help message"
            ],
            sawInventoryHeader: false);

        var commandKeys = commands.Select(command => command.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "mcp", "serve" }, commandKeys);
        Assert.Contains(commands, command =>
            string.Equals(command.Key, "serve", StringComparison.Ordinal)
            && string.Equals(command.Description, "Start the HTTP debug server (default port: 5200)", StringComparison.Ordinal));
        Assert.DoesNotContain(commands, command => string.Equals(command.Key, "help", StringComparison.Ordinal));
    }
}
