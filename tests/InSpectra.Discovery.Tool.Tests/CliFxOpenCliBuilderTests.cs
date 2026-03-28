using System.Text.Json.Nodes;
using Xunit;

public sealed class CliFxOpenCliBuilderTests
{
    [Fact]
    public void Builds_opencli_with_bool_flags_and_value_options()
    {
        var builder = new CliFxOpenCliBuilder();
        var staticCommands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Name: null,
                Description: "Default command",
                Parameters: [],
                Options:
                [
                    new CliFxOptionDefinition("verbose", 'v', false, false, true, "System.Boolean", "Verbose output", null, []),
                    new CliFxOptionDefinition("config", 'c', false, false, false, "System.String", "Config path", "DEMO_CONFIG", []),
                ]),
            ["user add"] = new(
                Name: "user add",
                Description: "Adds a user",
                Parameters:
                [
                    new CliFxParameterDefinition(0, "name", true, false, "System.String", "User display name", []),
                ],
                Options:
                [
                    new CliFxOptionDefinition("admin", 'a', false, false, true, "System.Boolean", "Grant admin", null, []),
                    new CliFxOptionDefinition("role", null, true, false, false, "System.String", "Role name", null, []),
                ]),
        };

        var helpDocuments = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: "Demo CLI",
                CommandDescription: "Default command",
                UsageLines: ["demo [command] [...]"],
                Parameters: [],
                Options:
                [
                    new CliFxHelpItem("-v|--verbose", false, "Verbose output"),
                    new CliFxHelpItem("-c|--config", false, "Config path"),
                ],
                Commands:
                [
                    new CliFxHelpItem("user", false, "Manage users"),
                ]),
            ["user"] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: "Manage users",
                UsageLines: ["demo user [command]"],
                Parameters: [],
                Options: [],
                Commands:
                [
                    new CliFxHelpItem("add", false, "Adds a user"),
                ]),
            ["user add"] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: "Adds a user",
                UsageLines: ["demo user add <name> --role <value> [options]"],
                Parameters:
                [
                    new CliFxHelpItem("name", true, "User display name"),
                ],
                Options:
                [
                    new CliFxHelpItem("-a|--admin", false, "Grant admin"),
                    new CliFxHelpItem("--role", true, "Role name"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", staticCommands, helpDocuments);
        var rootOptions = document["options"]!.AsArray();
        var verbose = rootOptions[0]!.AsObject();
        var config = rootOptions[1]!.AsObject();

        Assert.Null(verbose["arguments"]);
        Assert.NotNull(config["arguments"]);
        Assert.Equal("DEMO_CONFIG", config["arguments"]![0]!["metadata"]![1]!["value"]!.GetValue<string>());
        Assert.False(config.ContainsKey("required"));
        Assert.False(document.ContainsKey("arguments"));

        var rootCommands = document["commands"]!.AsArray();
        var userNode = Assert.Single(rootCommands);
        Assert.NotNull(userNode);
        var user = userNode!.AsObject();
        Assert.Equal("user", user["name"]!.GetValue<string>());
        Assert.Equal("Manage users", user["description"]!.GetValue<string>());
        Assert.False(user.ContainsKey("arguments"));

        var userAdd = user["commands"]![0]!.AsObject();
        Assert.Equal("add", userAdd["name"]!.GetValue<string>());
        Assert.Single(userAdd["arguments"]!.AsArray());
        Assert.Null(userAdd["options"]!.AsArray()[0]!["arguments"]);
        Assert.True(userAdd["options"]!.AsArray()[1]!["arguments"]![0]!["required"]!.GetValue<bool>());
    }

    [Fact]
    public void Falls_back_to_metadata_tree_and_schema_when_help_documents_are_missing()
    {
        var builder = new CliFxOpenCliBuilder();
        var staticCommands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Name: null,
                Description: "Default command",
                Parameters: [],
                Options: []),
            ["godot"] = new(
                Name: "godot",
                Description: "Manage Godot installations",
                Parameters: [],
                Options: []),
            ["godot install"] = new(
                Name: "godot install",
                Description: "Install a version of Godot",
                Parameters:
                [
                    new CliFxParameterDefinition(0, "version", true, false, "System.String", "Version to install", []),
                ],
                Options:
                [
                    new CliFxOptionDefinition("--proxy", 'x', false, false, false, "System.String", "Proxy URL", null, []),
                ]),
            ["sync"] = new(
                Name: "sync",
                Description: "Synchronize data",
                Parameters:
                [
                    new CliFxParameterDefinition(0, "target", true, false, "System.String", "Target name", []),
                ],
                Options:
                [
                    new CliFxOptionDefinition("dry-run", null, false, false, true, "System.Boolean", "Preview changes", null, []),
                ]),
        };

        var document = builder.Build(
            "demo",
            "1.0.0",
            staticCommands,
            new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase));

        Assert.Equal("Default command", document["info"]!["description"]!.GetValue<string>());
        Assert.False(document.ContainsKey("options"));
        Assert.False(document.ContainsKey("arguments"));

        var commands = document["commands"]!.AsArray();
        var sync = commands.Single(command => string.Equals(command!["name"]!.GetValue<string>(), "sync", StringComparison.Ordinal))!.AsObject();
        Assert.Equal("Synchronize data", sync["description"]!.GetValue<string>());
        Assert.Equal("target", sync["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Null(sync["options"]![0]!["arguments"]);

        var godot = commands.Single(command => string.Equals(command!["name"]!.GetValue<string>(), "godot", StringComparison.Ordinal))!.AsObject();
        Assert.Equal("godot", godot["name"]!.GetValue<string>());

        var install = godot["commands"]![0]!.AsObject();
        Assert.Equal("install", install["name"]!.GetValue<string>());
        Assert.Equal("version", install["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("--proxy", install["options"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("-x", install["options"]![0]!["aliases"]![0]!.GetValue<string>());
        Assert.Equal("PROXY", install["options"]![0]!["arguments"]![0]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Uses_Metadata_ValueName_For_ShortOnly_Options()
    {
        var builder = new CliFxOpenCliBuilder();
        var staticCommands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Name: null,
                Description: "Default command",
                Parameters: [],
                Options:
                [
                    new CliFxOptionDefinition(
                        Name: null,
                        ShortName: 's',
                        IsRequired: true,
                        IsSequence: false,
                        IsBoolLike: false,
                        ClrType: "System.String",
                        Description: "Script path",
                        EnvironmentVariable: null,
                        AcceptedValues: [],
                        ValueName: "scriptPath"),
                ]),
        };

        var document = builder.Build(
            "demo",
            "1.0.0",
            staticCommands,
            new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase));

        var optionNode = Assert.Single(document["options"]!.AsArray());
        Assert.NotNull(optionNode);
        var option = optionNode!.AsObject();
        var argumentNode = Assert.Single(option["arguments"]!.AsArray());
        Assert.NotNull(argumentNode);
        var argument = argumentNode!.AsObject();

        Assert.Equal("-s", option["name"]!.GetValue<string>());
        Assert.Equal("SCRIPT_PATH", argument["name"]!.GetValue<string>());
    }

    [Fact]
    public void Infers_HelpOnly_Option_Arguments_From_Usage_Lines()
    {
        var builder = new CliFxOpenCliBuilder();
        var helpDocuments = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: "Demo CLI",
                CommandDescription: null,
                UsageLines: ["demo [command] [...]"],
                Parameters: [],
                Options: [],
                Commands:
                [
                    new CliFxHelpItem("upload", false, "Uploads a package"),
                ]),
            ["upload"] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: "Uploads a package",
                UsageLines: ["demo upload --pat <token> [--folder <path>] [options]"],
                Parameters: [],
                Options:
                [
                    new CliFxHelpItem("-p|--pat", true, "Personal access token"),
                    new CliFxHelpItem("-f|--folder", false, "Folder to upload"),
                    new CliFxHelpItem("-h|--help", false, "Shows help text."),
                ],
                Commands: []),
        };

        var document = builder.Build(
            "demo",
            "1.0.0",
            new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase),
            helpDocuments);

        var commandNode = Assert.Single(document["commands"]!.AsArray());
        Assert.NotNull(commandNode);
        var command = commandNode!.AsObject();

        var options = command["options"]!.AsArray();
        Assert.Equal("TOKEN", options[0]!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Equal("PATH", options[1]!["arguments"]![0]!["name"]!.GetValue<string>());
        Assert.Null(options[2]!["arguments"]);
    }

    [Fact]
    public void Preserves_Metadata_Only_Options_And_Parameters_When_Help_Is_Partial()
    {
        var builder = new CliFxOpenCliBuilder();
        var staticCommands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["upload"] = new(
                Name: "upload",
                Description: "Upload artifacts",
                Parameters:
                [
                    new CliFxParameterDefinition(0, "source", true, false, "System.String", "Source path", []),
                    new CliFxParameterDefinition(1, "destination", false, false, "System.String", "Destination path", []),
                ],
                Options:
                [
                    new CliFxOptionDefinition("visible", null, false, false, false, "System.String", "Visible option", null, []),
                    new CliFxOptionDefinition("hidden", null, false, false, false, "System.String", "Hidden metadata option", null, []),
                ]),
        };

        var helpDocuments = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            ["upload"] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: "Upload artifacts",
                UsageLines: ["demo upload <destination> --visible <value> [options]"],
                Parameters:
                [
                    new CliFxHelpItem("destination", false, "Destination from help"),
                ],
                Options:
                [
                    new CliFxHelpItem("--visible", true, "Visible option from help"),
                ],
                Commands: []),
        };

        var document = builder.Build("demo", "1.0.0", staticCommands, helpDocuments);
        var command = Assert.Single(document["commands"]!.AsArray())!.AsObject();

        var arguments = command["arguments"]!.AsArray();
        Assert.Equal(2, arguments.Count);
        Assert.Equal("source", arguments[0]!["name"]!.GetValue<string>());
        Assert.True(arguments[0]!["required"]!.GetValue<bool>());
        Assert.Equal("destination", arguments[1]!["name"]!.GetValue<string>());
        Assert.False(arguments[1]!["required"]!.GetValue<bool>());
        Assert.Equal("Destination from help", arguments[1]!["description"]!.GetValue<string>());

        var options = command["options"]!.AsArray();
        Assert.Equal(2, options.Count);
        Assert.Equal("--visible", options[0]!["name"]!.GetValue<string>());
        Assert.Equal("--hidden", options[1]!["name"]!.GetValue<string>());
    }
}
