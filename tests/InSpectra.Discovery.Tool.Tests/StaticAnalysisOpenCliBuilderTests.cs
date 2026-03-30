using System.Text.Json.Nodes;
using Xunit;

public sealed class StaticAnalysisOpenCliBuilderTests
{
    [Fact]
    public void Build_Nests_MultiSegment_Help_Commands_And_Preserves_Leaf_Inputs()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = CreateHelpDocument(
                commands:
                [
                    new ToolHelpItem("config", false, "Configuration commands"),
                ]),
            ["config"] = CreateHelpDocument(
                description: "Configuration commands",
                commands:
                [
                    new ToolHelpItem("credentials", false, "Manage credentials"),
                ]),
            ["config credentials"] = CreateHelpDocument(
                description: "Manage credentials",
                commands:
                [
                    new ToolHelpItem("set", false, "Set a credential"),
                ]),
            ["config credentials set"] = CreateHelpDocument(
                description: "Set a credential",
                options:
                [
                    new ToolHelpItem("--key", true, "Credential key"),
                    new ToolHelpItem("--value", true, "Credential value"),
                ]),
        };

        var document = builder.Build(
            "workbench",
            "1.0.0",
            "System.CommandLine",
            new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase),
            helpDocuments);

        var rootCommands = Assert.IsType<JsonArray>(document["commands"]);
        var config = Assert.IsType<JsonObject>(Assert.Single(rootCommands));
        Assert.Equal("config", config["name"]!.GetValue<string>());

        var configCommands = Assert.IsType<JsonArray>(config["commands"]);
        var credentials = Assert.IsType<JsonObject>(Assert.Single(configCommands));
        Assert.Equal("credentials", credentials["name"]!.GetValue<string>());

        var credentialCommands = Assert.IsType<JsonArray>(credentials["commands"]);
        var set = Assert.IsType<JsonObject>(Assert.Single(credentialCommands));
        Assert.Equal("set", set["name"]!.GetValue<string>());
        Assert.Equal("Set a credential", set["description"]!.GetValue<string>());
        Assert.Contains(
            Assert.IsType<JsonArray>(set["options"]),
            option => string.Equals(option?["name"]?.GetValue<string>(), "--key", StringComparison.Ordinal));
        Assert.Contains(
            Assert.IsType<JsonArray>(set["options"]),
            option => string.Equals(option?["name"]?.GetValue<string>(), "--value", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_Synthesizes_Intermediate_Parents_For_Static_Only_MultiSegment_Commands()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var staticCommands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["deploy release create"] = new(
                Name: "deploy release create",
                Description: "Create a release deployment.",
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options:
                [
                    new StaticOptionDefinition(
                        LongName: "target",
                        ShortName: 't',
                        IsRequired: true,
                        IsSequence: false,
                        IsBoolLike: false,
                        ClrType: "System.String",
                        Description: "Deployment target.",
                        DefaultValue: null,
                        MetaValue: "TARGET",
                        AcceptedValues: [],
                        PropertyName: "Target"),
                ]),
        };

        var document = builder.Build(
            "deployer",
            "1.0.0",
            "CommandLineParser",
            staticCommands,
            new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase));

        var rootCommands = Assert.IsType<JsonArray>(document["commands"]);
        var deploy = Assert.IsType<JsonObject>(Assert.Single(rootCommands));
        Assert.Equal("deploy", deploy["name"]!.GetValue<string>());

        var deployCommands = Assert.IsType<JsonArray>(deploy["commands"]);
        var release = Assert.IsType<JsonObject>(Assert.Single(deployCommands));
        Assert.Equal("release", release["name"]!.GetValue<string>());

        var releaseCommands = Assert.IsType<JsonArray>(release["commands"]);
        var create = Assert.IsType<JsonObject>(Assert.Single(releaseCommands));
        Assert.Equal("create", create["name"]!.GetValue<string>());
        Assert.Equal("Create a release deployment.", create["description"]!.GetValue<string>());
        Assert.Contains(
            Assert.IsType<JsonArray>(create["options"]),
            option => string.Equals(option?["name"]?.GetValue<string>(), "--target", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_Skips_Metadata_Only_Builtin_Help_And_Version_Flags()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var staticCommands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Name: null,
                Description: "Default command",
                IsDefault: true,
                IsHidden: false,
                Values: [],
                Options:
                [
                    new StaticOptionDefinition(
                        LongName: "help",
                        ShortName: 'h',
                        IsRequired: false,
                        IsSequence: false,
                        IsBoolLike: true,
                        ClrType: "System.Boolean",
                        Description: "Show help information.",
                        DefaultValue: null,
                        MetaValue: null,
                        AcceptedValues: [],
                        PropertyName: "Help"),
                    new StaticOptionDefinition(
                        LongName: "version",
                        ShortName: null,
                        IsRequired: false,
                        IsSequence: false,
                        IsBoolLike: true,
                        ClrType: "System.Boolean",
                        Description: "Display version information.",
                        DefaultValue: null,
                        MetaValue: null,
                        AcceptedValues: [],
                        PropertyName: "Version"),
                    new StaticOptionDefinition(
                        LongName: "config",
                        ShortName: 'c',
                        IsRequired: true,
                        IsSequence: false,
                        IsBoolLike: false,
                        ClrType: "System.String",
                        Description: "Configuration path.",
                        DefaultValue: null,
                        MetaValue: "CONFIG",
                        AcceptedValues: [],
                        PropertyName: "Config"),
                ]),
        };

        var document = builder.Build(
            "demo",
            "1.0.0",
            "System.CommandLine",
            staticCommands,
            new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase));

        var options = Assert.IsType<JsonArray>(document["options"]);
        var config = Assert.Single(options);
        Assert.Equal("--config", config!["name"]?.GetValue<string>());
    }

    private static ToolHelpDocument CreateHelpDocument(
        string? description = null,
        IReadOnlyList<ToolHelpItem>? options = null,
        IReadOnlyList<ToolHelpItem>? commands = null)
        => new(
            Title: null,
            Version: null,
            ApplicationDescription: null,
            CommandDescription: description,
            UsageLines: [],
            Arguments: [],
            Options: options ?? [],
            Commands: commands ?? []);
}
