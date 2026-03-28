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
}
