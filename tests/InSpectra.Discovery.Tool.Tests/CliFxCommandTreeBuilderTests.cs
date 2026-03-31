namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Analysis.CliFx.Metadata;
using InSpectra.Discovery.Tool.Analysis.CliFx.OpenCli;

using Xunit;

public sealed class CliFxCommandTreeBuilderTests
{
    [Fact]
    public void Merges_metadata_only_descendants_into_help_tree()
    {
        var builder = new CliFxCommandTreeBuilder();
        var staticCommands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["godot"] = new("godot", "Manage Godot installations", [], []),
            ["godot install"] = new("godot install", "Install a version of Godot", [], []),
            ["sync"] = new("sync", "Synchronize data", [], []),
        };

        var helpDocuments = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["demo [command] [...]"],
                Parameters: [],
                Options: [],
                Commands:
                [
                    new CliFxHelpItem("godot", false, "Manage Godot installations"),
                ]),
        };

        var tree = builder.Build(staticCommands, helpDocuments);

        Assert.Equal(new[] { "godot", "sync" }, tree.Select(node => node.DisplayName).ToArray());
        var godot = tree[0];
        Assert.Equal("godot", godot.FullName);
        Assert.Equal("install", Assert.Single(godot.Children).DisplayName);
    }

    [Fact]
    public void Synthesizes_missing_intermediate_parents_for_multi_segment_commands()
    {
        var builder = new CliFxCommandTreeBuilder();
        var staticCommands = new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["app build"] = new("app build", "Build the app", [], []),
            ["app run"] = new("app run", "Run the app", [], []),
        };

        var helpDocuments = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["demo [command] [...]"],
                Parameters: [],
                Options: [],
                Commands:
                [
                    new CliFxHelpItem("app build", false, "Build the app"),
                    new CliFxHelpItem("app run", false, "Run the app"),
                ]),
        };

        var tree = builder.Build(staticCommands, helpDocuments);

        var app = Assert.Single(tree);
        Assert.Equal("app", app.DisplayName);
        Assert.Equal(new[] { "build", "run" }, app.Children.Select(child => child.DisplayName).ToArray());
    }

    [Fact]
    public void Preserves_Help_Command_Order_When_Building_Siblings()
    {
        var builder = new CliFxCommandTreeBuilder();
        var helpDocuments = new Dictionary<string, CliFxHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "demo",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: ["demo [command] [...]"],
                Parameters: [],
                Options: [],
                Commands:
                [
                    new CliFxHelpItem("gen-docker", false, "Generate docker-compose file."),
                    new CliFxHelpItem("gen", false, "Generate a self-signed certificate."),
                    new CliFxHelpItem("gen-kubernetes", false, "Generate Kubernetes resources."),
                ]),
        };

        var tree = builder.Build(
            new Dictionary<string, CliFxCommandDefinition>(StringComparer.OrdinalIgnoreCase),
            helpDocuments);

        Assert.Equal(new[] { "gen-docker", "gen", "gen-kubernetes" }, tree.Select(node => node.DisplayName).ToArray());
    }
}

