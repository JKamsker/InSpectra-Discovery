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

        var user = document["commands"]![0]!.AsObject();
        Assert.Equal("user", user["name"]!.GetValue<string>());

        var add = user["commands"]![0]!.AsObject();
        Assert.Equal("add", add["name"]!.GetValue<string>());
        Assert.Equal("NAME", add["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("--admin", add["options"]![0]!["name"]!.GetValue<string>());
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

    [Fact]
    public void Omits_Null_Collections_And_Does_Not_Emit_Option_Level_Required()
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
                    new ToolHelpItem("--verbose", false, null),
                    new ToolHelpItem("--config <PATH>", false, "Path to config"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var options = document["options"]!.AsArray();

        Assert.Null(document["arguments"]);
        Assert.Null(document["info"]!["description"]);
        Assert.Null(options[0]!["required"]);
        Assert.Null(options[0]!["description"]);
        Assert.Null(options[1]!["required"]);
        Assert.Equal("PATH", options[1]!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.True(options[1]!["arguments"]![0]!["required"]!.GetValue<bool>());
    }

    [Fact]
    public void Does_Not_Emit_Subcommand_Usage_Placeholders_As_Arguments()
    {
        var builder = new ToolHelpOpenCliBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["demo <subcommand> [<options>]"],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new ToolHelpItem("sync", false, "Synchronize data"),
                ]),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);

        Assert.Null(document["arguments"]);
        Assert.Single(document["commands"]!.AsArray());
    }

    [Fact]
    public void Sanitizes_Polluted_Argument_Names_And_Variadic_Placeholders()
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
                Arguments:
                [
                    new ToolHelpItem("FUNCTION-NAME> THE NAME OF THE FUNCTION TO INVOKE", true, null),
                    new ToolHelpItem("THE PROJECT TO USE.", false, null),
                    new ToolHelpItem("<search_pattern1 search_pattern2 ...>", false, null),
                ],
                Options: [],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var arguments = document["arguments"]!.AsArray();

        Assert.Equal(2, arguments.Count);
        Assert.Equal("FUNCTION_NAME", arguments[0]!["name"]!.GetValue<string>());
        Assert.Equal("SEARCH_PATTERN", arguments[1]!["name"]!.GetValue<string>());
        Assert.Equal(0, arguments[1]!["arity"]!["minimum"]!.GetValue<int>());
        Assert.Null(arguments[1]!["arity"]!["maximum"]);
    }

    [Fact]
    public void Skips_Option_Shaped_Placeholders_And_Normalizes_Choice_Metavars()
    {
        var builder = new ToolHelpOpenCliBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["demo [options] <--path|-p=<START_PATH>>"],
                Arguments:
                [
                    new ToolHelpItem("--format-json", false, "Bad positional"),
                ],
                Options:
                [
                    new ToolHelpItem("--path|-p=<START_PATH>", false, "Start path"),
                    new ToolHelpItem("-f, --format <Csv|Json|Table|Yaml>", false, "Output format"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var options = document["options"]!.AsArray();

        Assert.Null(document["arguments"]);
        Assert.Equal("START_PATH", options[0]!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("FORMAT", options[1]!["arguments"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Does_Not_Emit_Dispatcher_Target_As_Root_Argument_When_Child_Commands_Exist()
    {
        var builder = new ToolHelpOpenCliBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "abp",
                Version: "10.2.0-dev",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["abp <command> <target> [options]"],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new ToolHelpItem("add-module", false, "Add a multi-package module."),
                ]),
        };

        var document = builder.Build("abp", "10.2.0-dev", helpDocuments);

        Assert.Null(document["arguments"]);
        Assert.Single(document["commands"]!.AsArray());
    }

    [Fact]
    public void Does_Not_Emit_Option_Metavars_As_Usage_Only_Positional_Arguments()
    {
        var builder = new ToolHelpOpenCliBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["demo [--path <PATH>] [--format <Csv|Json|Table|Yaml>] <PROJECT>"],
                Arguments: [],
                Options: [],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", helpDocuments);
        var arguments = document["arguments"]!.AsArray();

        var project = Assert.Single(arguments);
        Assert.Equal("PROJECT", project!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Emits_Arguments_For_Bare_Word_Option_Metavars()
    {
        var builder = new ToolHelpOpenCliBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "nake",
                Version: "4.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options:
                [
                    new ToolHelpItem("--runner NAME", false, "Runner to execute"),
                ],
                Commands: []),
        };

        var document = builder.Build("nake", "4.0.0", helpDocuments);
        var option = document["options"]![0]!;

        Assert.Equal("--runner", option["name"]!.GetValue<string>());
        Assert.Equal("NAME", option["arguments"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Prefers_Root_Command_Description_Over_Preamble_Description()
    {
        var builder = new ToolHelpOpenCliBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "adfs",
                Version: "1.0.1",
                ApplicationDescription: "No parameters file found in the current directory.",
                CommandDescription: "ADFS Authentication CLI tool",
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands: []),
        };

        var document = builder.Build("adfs", "1.0.1", helpDocuments);

        Assert.Equal("ADFS Authentication CLI tool", document["info"]!["description"]!.GetValue<string>());
    }
}
