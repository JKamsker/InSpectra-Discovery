using Xunit;

public sealed class ToolHelpUsageArgumentSupportTests
{
    [Fact]
    public void ExtractUsageArguments_Ignores_Dispatcher_Placeholder_When_Child_Commands_Exist()
    {
        var arguments = ToolHelpUsageArgumentSupport.ExtractUsageArguments(
            commandName: "tool",
            commandPath: "",
            usageLines: ["tool <command>"],
            hasChildCommands: true);

        Assert.Empty(arguments);
    }

    [Fact]
    public void ExtractUsageArguments_Normalizes_Bare_File_Tokens()
    {
        var arguments = ToolHelpUsageArgumentSupport.ExtractUsageArguments(
            commandName: "tool",
            commandPath: "merge",
            usageLines: ["tool merge input.csv"],
            hasChildCommands: false);

        var argument = Assert.Single(arguments);
        Assert.Equal("FILE", argument.Key);
        Assert.True(argument.IsRequired);
    }

    [Fact]
    public void SelectArguments_Prefers_Usage_When_Low_Signal_Explicit_Arguments_Do_Not_Match_Usage_Count()
    {
        var explicitArguments = new[]
        {
            new ToolHelpItem("ARG", true, null),
            new ToolHelpItem("ARGS", false, null),
        };
        var usageArguments = new[]
        {
            new ToolHelpItem("PATH", true, null),
        };

        var selected = ToolHelpUsageArgumentSupport.SelectArguments(explicitArguments, usageArguments);

        var argument = Assert.Single(selected);
        Assert.Equal("PATH", argument.Key);
    }

    [Fact]
    public void LooksLikeCommandInventoryEchoArguments_Matches_Command_Inventory()
    {
        var explicitArguments = new[]
        {
            new ToolHelpItem("<list>", true, "List items"),
            new ToolHelpItem("<show>", true, "Show one item"),
        };
        var commands = new[]
        {
            new ToolHelpItem("list", false, "List items"),
            new ToolHelpItem("show", false, "Show one item"),
        };

        var looksLikeInventory = ToolHelpUsageArgumentSupport.LooksLikeCommandInventoryEchoArguments(explicitArguments, commands);

        Assert.True(looksLikeInventory);
    }
}
