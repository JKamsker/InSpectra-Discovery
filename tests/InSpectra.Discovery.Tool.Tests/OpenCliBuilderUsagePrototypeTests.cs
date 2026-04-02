namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.OpenCli;
using InSpectra.Discovery.Tool.Help.Parsing;

using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliBuilderUsagePrototypeTests
{
    [Fact]
    public void Synthesizes_Leaf_Command_Options_From_Usage_Only_Prototypes()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new TextParser().Parse(
                """
                mcpdebugger - AI-controlled cooperative debugger via MCP

                Usage:
                  mcpdebugger serve [--port <port>]   Start the HTTP debug server (default port: 5200)
                  mcpdebugger mcp [--port <port>]     Start the MCP server (talks to debug server)
                  mcpdebugger --help                  Show this help message
                """),
        };

        var document = builder.Build("mcpdebugger", "0.1.0", helpDocuments);
        var commands = Assert.IsType<JsonArray>(document["commands"]);

        var serve = Assert.Single(commands.Where(command => string.Equals(command?["name"]?.GetValue<string>(), "serve", StringComparison.Ordinal)));
        Assert.Equal("--port", serve!["options"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("PORT", serve["options"]![0]!["arguments"]![0]!["name"]!.GetValue<string>());

        var mcp = Assert.Single(commands.Where(command => string.Equals(command?["name"]?.GetValue<string>(), "mcp", StringComparison.Ordinal)));
        Assert.Equal("--port", mcp!["options"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("PORT", mcp["options"]![0]!["arguments"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Synthesizes_Leaf_Command_Arguments_From_Usage_Only_Prototypes()
    {
        var builder = new OpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new TextParser().Parse(
                """
                AvaloniaMcp - MCP server for debugging Avalonia UI applications (v0.4.0)

                Usage:
                  avalonia-mcp                     Start as MCP server (stdio transport, for AI agents)
                  avalonia-mcp cli <method> ...    Run a single diagnostic command

                MCP Server Options:
                  --pipe <name>      Connect to a specific named pipe
                  --pid <processId>  Connect to an Avalonia app by PID
                """),
        };

        var document = builder.Build("avalonia-mcp", "0.4.0", helpDocuments);
        var cli = Assert.Single(document["commands"]!.AsArray().Where(command => string.Equals(command?["name"]?.GetValue<string>(), "cli", StringComparison.Ordinal)));

        Assert.Equal("METHOD", cli!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Equal(1, cli["arguments"]![0]!["arity"]!["minimum"]!.GetValue<int>());
        Assert.Null(cli["options"]);
    }
}
