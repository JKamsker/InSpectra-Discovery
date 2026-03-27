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
}
