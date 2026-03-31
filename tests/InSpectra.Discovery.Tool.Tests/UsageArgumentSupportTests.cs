namespace InSpectra.Discovery.Tool.Tests;

using InSpectra.Discovery.Tool.Help.Documents;
using InSpectra.Discovery.Tool.Help.Inference.Usage;

using Xunit;

public sealed class UsageArgumentSupportTests
{
    [Fact]
    public void ExtractUsageArguments_Ignores_Dispatcher_Placeholder_When_Child_Commands_Exist()
    {
        var arguments = UsageArgumentSupport.ExtractUsageArguments(
            commandName: "tool",
            commandPath: "",
            usageLines: ["tool <command>"],
            hasChildCommands: true);

        Assert.Empty(arguments);
    }

    [Fact]
    public void ExtractUsageArguments_Normalizes_Bare_File_Tokens()
    {
        var arguments = UsageArgumentSupport.ExtractUsageArguments(
            commandName: "tool",
            commandPath: "merge",
            usageLines: ["tool merge input.csv"],
            hasChildCommands: false);

        var argument = Assert.Single(arguments);
        Assert.Equal("FILE", argument.Key);
        Assert.True(argument.IsRequired);
    }

    [Fact]
    public void ExtractUsageArguments_Preserves_Group_Quantifier_As_Sequence()
    {
        var arguments = UsageArgumentSupport.ExtractUsageArguments(
            commandName: "tool",
            commandPath: "",
            usageLines: ["tool (<file>)+ [--verbose]"],
            hasChildCommands: false);

        var argument = Assert.Single(arguments);
        Assert.Equal("file...", argument.Key);
        Assert.True(argument.IsRequired);
    }

    [Fact]
    public void SelectArguments_Prefers_Usage_When_Low_Signal_Explicit_Arguments_Do_Not_Match_Usage_Count()
    {
        var explicitArguments = new[]
        {
            new Item("ARG", true, null),
            new Item("ARGS", false, null),
        };
        var usageArguments = new[]
        {
            new Item("PATH", true, null),
        };

        var selected = UsageArgumentSupport.SelectArguments(explicitArguments, usageArguments);

        var argument = Assert.Single(selected);
        Assert.Equal("PATH", argument.Key);
    }

    [Fact]
    public void LooksLikeCommandInventoryEchoArguments_Matches_Command_Inventory()
    {
        var explicitArguments = new[]
        {
            new Item("<list>", true, "List items"),
            new Item("<show>", true, "Show one item"),
        };
        var commands = new[]
        {
            new Item("list", false, "List items"),
            new Item("show", false, "Show one item"),
        };

        var looksLikeInventory = UsageArgumentSupport.LooksLikeCommandInventoryEchoArguments(explicitArguments, commands);

        Assert.True(looksLikeInventory);
    }
}

