namespace InSpectra.Discovery.Tool.Help;

internal static class UsageArgumentSupport
{
    public static IReadOnlyList<Item> ExtractUsageArguments(
        string commandName,
        string commandPath,
        IReadOnlyList<string> usageLines,
        bool hasChildCommands)
        => UsageArgumentExtractionSupport.Extract(commandName, commandPath, usageLines, hasChildCommands);

    public static IReadOnlyList<Item> SelectArguments(
        IReadOnlyList<Item> explicitArguments,
        IReadOnlyList<Item> usageArguments)
        => UsageArgumentSelectionSupport.Select(explicitArguments, usageArguments);

    public static bool LooksLikeCommandInventoryEchoArguments(
        IReadOnlyList<Item> explicitArguments,
        IReadOnlyList<Item> commands)
        => UsageArgumentInventorySupport.LooksLikeCommandInventoryEcho(explicitArguments, commands);

    public static bool LooksLikeAuxiliaryInventoryEchoArguments(
        IReadOnlyList<Item> explicitArguments,
        IReadOnlyList<string> usageLines)
        => UsageArgumentInventorySupport.LooksLikeAuxiliaryInventoryEcho(explicitArguments, usageLines);
}

