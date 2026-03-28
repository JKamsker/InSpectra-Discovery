using Xunit;

public sealed class ToolHelpCommandTreeBuilderTests
{
    [Fact]
    public void Build_Relativizes_FullyQualified_Command_Rows()
    {
        var builder = new ToolHelpCommandTreeBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "spx",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new ToolHelpItem("spx batch", false, "Batch operations"),
                ]),
            ["batch"] = new(
                Title: "spx batch",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: "Batch operations",
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new ToolHelpItem("spx batch transcription create", false, "Create transcription jobs"),
                ]),
        };

        var nodes = builder.Build("spx", helpDocuments);

        var batch = Assert.Single(nodes);
        Assert.Equal("batch", batch.DisplayName);

        var transcription = Assert.Single(batch.Children);
        Assert.Equal("transcription", transcription.DisplayName);

        var create = Assert.Single(transcription.Children);
        Assert.Equal("create", create.DisplayName);
    }

    [Fact]
    public void Build_Keeps_RootQualified_Sibling_Command_Rows_At_The_Root()
    {
        var builder = new ToolHelpCommandTreeBuilder();
        var helpDocuments = new Dictionary<string, ToolHelpDocument>(StringComparer.OrdinalIgnoreCase)
        {
            [""] = new(
                Title: "spx",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: null,
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new ToolHelpItem("spx batch", false, "Batch operations"),
                    new ToolHelpItem("spx config", false, "Configuration"),
                ]),
            ["batch"] = new(
                Title: "spx batch",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: "Batch operations",
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new ToolHelpItem("spx config set", false, "Set a configuration value"),
                ]),
            ["config"] = new(
                Title: "spx config",
                Version: "1.0.0",
                ApplicationDescription: null,
                CommandDescription: "Configuration",
                UsageLines: [],
                Arguments: [],
                Options: [],
                Commands:
                [
                    new ToolHelpItem("spx config set", false, "Set a configuration value"),
                ]),
        };

        var nodes = builder.Build("spx", helpDocuments);

        Assert.Equal(2, nodes.Count);
        Assert.Equal("batch", nodes[0].DisplayName);

        var config = Assert.Single(nodes.Where(node => string.Equals(node.DisplayName, "config", StringComparison.Ordinal)));
        var set = Assert.Single(config.Children);
        Assert.Equal("set", set.DisplayName);

        var batch = Assert.Single(nodes.Where(node => string.Equals(node.DisplayName, "batch", StringComparison.Ordinal)));
        Assert.Empty(batch.Children);
    }
}
