namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Inference.Inventory;

using Xunit;

public sealed class RootCommandInventoryInferenceTests
{
    [Fact]
    public void InferLines_Prefers_Alias_Command_Inventory_Block()
    {
        var lines = RootCommandInventoryInference.InferLines(
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
        var lines = RootCommandInventoryInference.InferLines(
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
        var looksLikeAliasInventory = RootCommandInventoryInference.LooksLikeAliasCommandInventoryBlock(
            [
                "",
                "  p, path  Path to output folder",
                "  o, output  Optional. Write output file",
            ]);

        Assert.False(looksLikeAliasInventory);
    }

    [Fact]
    public void InferLines_Does_Not_Treat_Indented_Prose_As_Command_Inventory()
    {
        var lines = RootCommandInventoryInference.InferLines(
            [
                "The Integration Of Reciprocal Problem",
                "",
                " A persistent instability in any inductive test best-practice cannot always help us.  It is recognized that any fundamental dichotomies of the benchmark should not divert attention from The Integration Of Reciprocal Problem'",
                "",
                "Based on integral subsystems, the ball-park figures for the flexible manufacturing system provides the bridge between the key area of opportunity and the universe of contemplation.",
            ]);

        Assert.Empty(lines);
    }
}
