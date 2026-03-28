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
}
