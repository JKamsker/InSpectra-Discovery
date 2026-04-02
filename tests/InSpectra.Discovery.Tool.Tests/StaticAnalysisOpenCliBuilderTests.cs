namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.StaticAnalysis.Models;
using InSpectra.Discovery.Tool.StaticAnalysis.OpenCli;

using InSpectra.Discovery.Tool.Help.Documents;

using System.Text.Json.Nodes;
using Xunit;

public sealed class StaticAnalysisOpenCliBuilderTests
{
    [Fact]
    public void Build_Nests_MultiSegment_Help_Commands_And_Preserves_Leaf_Inputs()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = CreateHelpDocument(
                commands:
                [
                    new Item("config", false, "Configuration commands"),
                ]),
            ["config"] = CreateHelpDocument(
                description: "Configuration commands",
                commands:
                [
                    new Item("credentials", false, "Manage credentials"),
                ]),
            ["config credentials"] = CreateHelpDocument(
                description: "Manage credentials",
                commands:
                [
                    new Item("set", false, "Set a credential"),
                ]),
            ["config credentials set"] = CreateHelpDocument(
                description: "Set a credential",
                options:
                [
                    new Item("--key", true, "Credential key"),
                    new Item("--value", true, "Credential value"),
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
            new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase));

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
            new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase));

        var options = Assert.IsType<JsonArray>(document["options"]);
        var config = Assert.Single(options);
        Assert.Equal("--config", config!["name"]?.GetValue<string>());
    }

    [Fact]
    public void Build_Prefers_Package_Version_And_Skips_Placeholder_Root_Command()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var staticCommands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["root"] = new(
                Name: "root",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
        };
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = CreateHelpDocument(
                title: "DotNetAnalyzer - .NET MCP Server for Claude Code",
                version: "1.1.2",
                commands:
                [
                    new Item("mcp serve", false, "Start MCP server (default)"),
                ]),
        };

        var document = builder.Build(
            "dotnet-analyzer",
            "1.5.0",
            "System.CommandLine",
            staticCommands,
            helpDocuments);

        var info = Assert.IsType<JsonObject>(document["info"]);
        Assert.Equal("1.5.0", info["version"]!.GetValue<string>());

        var commands = Assert.IsType<JsonArray>(document["commands"]);
        var rootCommand = Assert.IsType<JsonObject>(Assert.Single(commands));
        Assert.Equal("mcp", rootCommand["name"]!.GetValue<string>());
    }

    [Fact]
    public void Build_Keeps_Lone_Placeholder_Root_Command_When_No_Other_Surface_Exists()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var staticCommands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["root"] = new(
                Name: "root",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
        };

        var document = builder.Build(
            "mgcb-editor-windows",
            "3.8.5-preview.3",
            "System.CommandLine",
            staticCommands,
            new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase));

        var commands = Assert.IsType<JsonArray>(document["commands"]);
        var rootCommand = Assert.IsType<JsonObject>(Assert.Single(commands));
        Assert.Equal("root", rootCommand["name"]!.GetValue<string>());
    }

    [Fact]
    public void Build_Maps_Slash_Style_Help_Options_Into_Publishable_Names()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var staticCommands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["root"] = new(
                Name: "root",
                Description: null,
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
        };
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = CreateHelpDocument(
                title: "MonoGame Content Builder:",
                version: "v3.8.5.0",
                options:
                [
                    new Item("/b, /build:<sourceFile>", false, "Build the content source file."),
                    new Item("/c, /clean", false, "Delete all previously built content and intermediate files."),
                    new Item("/compress", false, "Compress the XNB files for smaller file sizes."),
                    new Item("/h, /help", false, "Displays this help."),
                ]),
        };

        var document = builder.Build(
            "mgcb",
            "3.8.5-preview.3",
            "System.CommandLine",
            staticCommands,
            helpDocuments);

        var options = Assert.IsType<JsonArray>(document["options"]);
        Assert.Equal(
            new[]
            {
                "--build",
                "--clean",
                "--compress",
                "--help",
            },
            options
                .OfType<JsonObject>()
                .Select(option => option["name"]!.GetValue<string>())
                .ToArray());
    }

    [Fact]
    public void Build_Prefers_Help_Surface_Over_Unmatched_Static_Metadata_When_Help_Graph_Exists()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var staticCommands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Name: null,
                Description: "Default command",
                IsDefault: true,
                IsHidden: false,
                Values:
                [
                    new StaticValueDefinition(
                        0,
                        "FILE",
                        true,
                        false,
                        "System.String",
                        "Static-only positional argument.",
                        null,
                        []),
                ],
                Options:
                [
                    new StaticOptionDefinition(
                        LongName: "value",
                        ShortName: 'v',
                        IsRequired: true,
                        IsSequence: false,
                        IsBoolLike: false,
                        ClrType: "System.String",
                        Description: "Static-only root option.",
                        DefaultValue: null,
                        MetaValue: "VALUE",
                        AcceptedValues: [],
                        PropertyName: "Value"),
                ]),
            ["greet"] = new(
                Name: "greet",
                Description: "Greet a user.",
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
            ["gethashcode"] = new(
                Name: "gethashcode",
                Description: "Compiler noise.",
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
            ["<clone>$"] = new(
                Name: "<clone>$",
                Description: "Compiler noise.",
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
        };
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = CreateHelpDocument(
                title: "demo",
                version: "1.0.0",
                options:
                [
                    new Item("-h, --help", false, "Show help."),
                    new Item("--version", false, "Show version."),
                ],
                commands:
                [
                    new Item("greet", false, "Greet a user."),
                ]),
            ["greet"] = CreateHelpDocument(
                description: "Greet a user."),
        };

        var document = builder.Build(
            "demo",
            "1.0.0",
            "Cocona",
            staticCommands,
            helpDocuments);

        var commands = Assert.IsType<JsonArray>(document["commands"]);
        var greet = Assert.IsType<JsonObject>(Assert.Single(commands));
        Assert.Equal("greet", greet["name"]!.GetValue<string>());

        var options = Assert.IsType<JsonArray>(document["options"]);
        Assert.Equal(
            new[] { "--help", "--version" },
            options
                .OfType<JsonObject>()
                .Select(option => option["name"]!.GetValue<string>())
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());

        Assert.Null(document["arguments"]);
    }

    [Fact]
    public void Build_Does_Not_Import_Help_Derived_Commands_For_Default_Only_Static_Option_Tools()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var staticCommands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Name: null,
                Description: null,
                IsDefault: true,
                IsHidden: false,
                Values: [],
                Options:
                [
                    new StaticOptionDefinition(
                        LongName: "project-name",
                        ShortName: 'p',
                        IsRequired: false,
                        IsSequence: false,
                        IsBoolLike: false,
                        ClrType: "System.String",
                        Description: "Project Name",
                        DefaultValue: null,
                        MetaValue: null,
                        AcceptedValues: [],
                        PropertyName: "ProjectName"),
                ]),
        };
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "DotnetAzureNaming",
                Version: "1.0.0",
                ApplicationDescription: "This tool will help you name Azure Resources.",
                CommandDescription: null,
                UsageLines:
                [
                    "azure-naming --component-name Web --environment Development --project-name Titanic --resource-type \"Function app\"",
                    "azure-naming -c Web -e dev -p Titanic -r func",
                ],
                Arguments: [],
                Options:
                [
                    new Item("-p, --project-name", false, "Project Name"),
                ],
                Commands:
                [
                    new Item("MAY add custom environment specifiers to your", false, null),
                    new Item("MUST include a project that MAY be the application", false, null),
                ]),
        };

        var document = builder.Build(
            "azure-naming",
            "1.0.0",
            "CommandLineParser",
            staticCommands,
            helpDocuments);

        Assert.Null(document["commands"]);

        var options = Assert.IsType<JsonArray>(document["options"]);
        var projectName = Assert.IsType<JsonObject>(Assert.Single(options));
        Assert.Equal("--project-name", projectName["name"]!.GetValue<string>());
    }

    [Fact]
    public void Build_Does_Not_Import_Help_Child_Commands_For_Static_Verbs_That_Are_Not_Dispatchers()
    {
        var builder = new StaticAnalysisOpenCliBuilder();
        var staticCommands = new Dictionary<string, StaticCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["single-file"] = new(
                Name: "single-file",
                Description: "Combines multiple source code files (.cs) into a single one.",
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options:
                [
                    new StaticOptionDefinition(
                        LongName: "output",
                        ShortName: 'o',
                        IsRequired: false,
                        IsSequence: false,
                        IsBoolLike: false,
                        ClrType: "System.String",
                        Description: "Output path.",
                        DefaultValue: null,
                        MetaValue: "OUTPUT",
                        AcceptedValues: [],
                        PropertyName: "Output"),
                ]),
            ["zip"] = new(
                Name: "zip",
                Description: "Creates a zip file.",
                IsDefault: false,
                IsHidden: false,
                Values: [],
                Options: []),
        };
        var helpDocuments = new Dictionary<string, Document>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = CreateHelpDocument(
                title: "dotnet combine",
                version: "0.4.0",
                commands:
                [
                    new Item("single-file", false, "Combines multiple source code files (.cs) into a single one."),
                    new Item("zip", false, "Creates a zip file."),
                ]),
            ["single-file"] = CreateHelpDocument(
                description: "Combines multiple source code files (.cs) into a single one.",
                options:
                [
                    new Item("-o, --output <PATH>", false, "Output path."),
                ],
                commands:
                [
                    new Item("dir path is provided", false, null),
                    new Item("filename is provided", false, null),
                ]),
            ["zip"] = CreateHelpDocument(
                description: "Creates a zip file."),
        };

        var document = builder.Build(
            "dotnet-combine",
            "0.4.0",
            "CommandLineParser",
            staticCommands,
            helpDocuments);

        var commands = Assert.IsType<JsonArray>(document["commands"]);
        var singleFile = Assert.IsType<JsonObject>(commands.OfType<JsonObject>().Single(command => string.Equals(command["name"]?.GetValue<string>(), "single-file", StringComparison.Ordinal)));
        Assert.Null(singleFile["commands"]);
    }

    private static Document CreateHelpDocument(
        string? title = null,
        string? version = null,
        string? description = null,
        IReadOnlyList<Item>? options = null,
        IReadOnlyList<Item>? commands = null)
        => new(
            Title: title,
            Version: version,
            ApplicationDescription: null,
            CommandDescription: description,
            UsageLines: [],
            Arguments: [],
            Options: options ?? [],
            Commands: commands ?? []);
}
