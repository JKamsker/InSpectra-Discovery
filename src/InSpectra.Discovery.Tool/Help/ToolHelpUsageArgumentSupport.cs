internal static class ToolHelpUsageArgumentSupport
{
    public static IReadOnlyList<ToolHelpItem> ExtractUsageArguments(
        string commandName,
        string commandPath,
        IReadOnlyList<string> usageLines,
        bool hasChildCommands)
        => ToolHelpUsageArgumentExtractionSupport.Extract(commandName, commandPath, usageLines, hasChildCommands);

    public static IReadOnlyList<ToolHelpItem> SelectArguments(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<ToolHelpItem> usageArguments)
        => ToolHelpUsageArgumentSelectionSupport.Select(explicitArguments, usageArguments);

    public static bool LooksLikeCommandInventoryEchoArguments(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<ToolHelpItem> commands)
        => ToolHelpUsageArgumentInventorySupport.LooksLikeCommandInventoryEcho(explicitArguments, commands);

    public static bool LooksLikeAuxiliaryInventoryEchoArguments(
        IReadOnlyList<ToolHelpItem> explicitArguments,
        IReadOnlyList<string> usageLines)
        => ToolHelpUsageArgumentInventorySupport.LooksLikeAuxiliaryInventoryEcho(explicitArguments, usageLines);
}
