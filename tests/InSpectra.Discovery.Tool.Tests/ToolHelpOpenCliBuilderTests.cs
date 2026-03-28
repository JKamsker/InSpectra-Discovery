using System.Text.Json.Nodes;
using Xunit;

public sealed class ToolHelpOpenCliBuilderTests
{
    [Fact]
    public void Builds_OpenCli_From_Help_Documents_Only()
    {
        var builder = new ToolHelpOpenCliBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.2.3",
                ApplicationDescription: "Demo CLI",
                CommandDescription: null,
                UsageLines: ["demo [command] [options]"],
                Arguments: [],
                Options:
                [
                    new ToolHelpItem("-v|--verbose", false, "Verbose output"),
                    new ToolHelpItem("-c, --config <PATH>", false, "Path to config"),
                ],
                Commands:
                [
                    new ToolHelpItem("user add", false, "Add a user"),
                ]),
            ["user add"] = new(
                Title: "demo",
                Version: "1.2.3",
                ApplicationDescription: null,
                CommandDescription: "Add a user",
                UsageLines: ["demo user add <NAME> [--admin]"],
                Arguments: [],
                Options:
                [
                    new ToolHelpItem("--admin", false, "Grant admin"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.2.3", helpDocuments);

        Assert.Equal("demo", document["info"]!["title"]!.GetValue<string>());
        Assert.Equal("1.2.3", document["info"]!["version"]!.GetValue<string>());

        var rootOptions = document["options"]!.AsArray();
        Assert.Equal("--config", rootOptions[1]!["name"]!.GetValue<string>());
        Assert.Equal("-c", rootOptions[1]!["aliases"]![0]!.GetValue<string>());
        Assert.Equal("PATH", rootOptions[1]!["arguments"]![0]!["name"]!.GetValue<string>());

        var command = document["commands"]![0]!.AsObject();
        Assert.Equal("user add", command["name"]!.GetValue<string>());
        Assert.Equal("NAME", command["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("--admin", command["options"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Prefers_More_Descriptive_Single_Dash_Alias_As_Primary_Name()
    {
        var builder = new ToolHelpOpenCliBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options:
                [
                    new ToolHelpItem("-i, -input <PATH>", false, "Input file"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var option = document["options"]![0]!;

        Assert.Equal("-input", option["name"]!.GetValue<string>());
        Assert.Equal("-i", option["aliases"]![0]!.GetValue<string>());
    }
}
