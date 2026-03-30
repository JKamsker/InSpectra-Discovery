namespace InSpectra.Discovery.Tool.Tests;

using Xunit;

public sealed class ToolHelpRootCommandInventoryInferenceTests
{
    [Fact]
    public void InferLines_Prefers_Alias_Command_Inventory_Block()
    {
        var lines = ToolHelpRootCommandInventoryInference.InferLines(
            [
                "tool",
                "",
                "  b, build  Build the project",
                "  r, restore  Restore dependencies",
            ]);

        Assert.Equal(
            [
                "  b, build  Build the project",
                "  r, restore  Restore dependencies",
            ],
            lines);
    }

    [Fact]
    public void InferLines_Falls_Back_To_Indented_Command_Inventory()
    {
        var lines = ToolHelpRootCommandInventoryInference.InferLines(
            [
                "tool",
                "",
                "  build  Build the project",
                "    Uses the current solution",
                "  restore  Restore dependencies",
                "  --output  Write output",
            ]);

        Assert.Equal(
            [
                "  build  Build the project",
                "    Uses the current solution",
                "  restore  Restore dependencies",
            ],
            lines);
    }

    [Fact]
    public void LooksLikeAliasCommandInventoryBlock_Rejects_Option_Heavy_Alias_Rows()
    {
        var looksLikeAliasInventory = ToolHelpRootCommandInventoryInference.LooksLikeAliasCommandInventoryBlock(
            [
                "",
                "  p, path  Path to output folder",
                "  o, output  Optional. Write output file",
            ]);

        Assert.False(looksLikeAliasInventory);
    }
}

