namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Inference.Usage;

using Xunit;

public sealed class UsageArgumentSelectionSupportTests
{
    [Fact]
    public void Select_Prefers_Richer_Usage_Placeholder_Over_Generic_Explicit_Position_Row()
    {
        var selected = UsageArgumentSelectionSupport.Select(
            [new Item("VALUE", true, null)],
            [new Item("STRING", true, null)]);

        var argument = Assert.Single(selected);
        Assert.Equal("STRING", argument.Key);
        Assert.True(argument.IsRequired);
    }

    [Fact]
    public void Select_Prefers_Bracketed_Usage_Argument_Over_Generic_Explicit_Position_Row()
    {
        var selected = UsageArgumentSelectionSupport.Select(
            [new Item("VALUE", false, null)],
            [new Item("INPUTFILE...", false, null)]);

        var argument = Assert.Single(selected);
        Assert.Equal("INPUTFILE...", argument.Key);
        Assert.False(argument.IsRequired);
    }
}
