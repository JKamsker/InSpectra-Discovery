namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Documents;

using Xunit;

public sealed class DocumentInspectorTests
{
    [Fact]
    public void Does_Not_Treat_Mcp_Help_Text_As_Terminal_NonHelp()
    {
        const string payload =
            """
            AvaloniaMcp - MCP server for debugging Avalonia UI applications (v0.4.0)

            Usage:
              avalonia-mcp                     Start as MCP server (stdio transport, for AI agents)
              avalonia-mcp cli <method> ...    Run a single diagnostic command

            MCP Server Options:
              --pipe <name>      Connect to a specific named pipe
            """;

        Assert.False(DocumentInspector.LooksLikeTerminalNonHelpPayload(payload));
    }

    [Fact]
    public void Detects_Platform_Blocked_Payloads_As_Terminal_NonHelp()
    {
        const string payload =
            """
            AppMap for .NET is currently only supported on linux-x64.
            Platform win-x64 not supported yet.
            """;

        Assert.True(DocumentInspector.LooksLikeTerminalNonHelpPayload(payload));
        Assert.True(DocumentInspector.LooksLikePlatformBlockedPayload(payload));
    }
}
