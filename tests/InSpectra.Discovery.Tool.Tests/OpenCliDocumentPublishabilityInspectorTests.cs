namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.OpenCli.Documents;

using System.Text.Json.Nodes;
using Xunit;

public sealed class OpenCliDocumentPublishabilityInspectorTests
{
    [Fact]
    public void HasPublishableSurface_Accepts_Nested_Command_Options()
    {
        var document = new JsonObject
        {
            ["commands"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "serve",
                    ["commands"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "run",
                            ["options"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["name"] = "--verbose",
                                },
                            },
                        },
                    },
                },
            },
        };

        Assert.True(OpenCliDocumentPublishabilityInspector.HasPublishableSurface(document));
    }

    [Fact]
    public void LooksLikeInventoryOnlyCommandShellDocument_Rejects_Crawled_Command_Shells()
    {
        var document = new JsonObject
        {
            ["x-inspectra"] = new JsonObject
            {
                ["artifactSource"] = "crawled-from-help",
                ["helpDocumentCount"] = 1,
            },
            ["commands"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "config",
                },
                new JsonObject
                {
                    ["name"] = "status",
                },
            },
        };

        Assert.True(OpenCliDocumentPublishabilityInspector.LooksLikeInventoryOnlyCommandShellDocument(document));
    }

    [Fact]
    public void ContainsErrorText_Detects_Stack_Trace_Descriptions()
    {
        var document = new JsonObject
        {
            ["description"] = "Unhandled exception.\n   at Demo.Program.Run()",
            ["options"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "--verbose",
                },
            },
        };

        Assert.True(OpenCliDocumentPublishabilityInspector.ContainsErrorText(document));
    }

    [Fact]
    public void ContainsBoxDrawingCommandNames_Detects_Table_Artifacts()
    {
        var document = new JsonObject
        {
            ["commands"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "│ serve",
                },
            },
        };

        Assert.True(OpenCliDocumentPublishabilityInspector.ContainsBoxDrawingCommandNames(document));
    }

    [Fact]
    public void LooksLikeNonPublishableDescription_Detects_Runtime_Noise()
    {
        const string description = "You must install or update .NET to run this application.";

        Assert.True(OpenCliDocumentPublishabilityInspector.LooksLikeNonPublishableDescription(description));
    }

    [Fact]
    public void LooksLikeNonPublishableDescription_Does_Not_Reject_Mcp_Server_Help_Text()
    {
        const string description = "Start as MCP server (stdio transport, for AI agents)";

        Assert.False(OpenCliDocumentPublishabilityInspector.LooksLikeNonPublishableDescription(description));
    }
}
